"""Segment-exact clearance on finished channels; distances in miles.

Counts violating reach pairs (and violating nonadjacent segment pairs within
each reach). Shared junction pockets are excluded for 0.5 mi of arc length.
"""
import math
import numpy as np
from a2_common import SEEDS, run_check, main
from river_meander import _resample


def local_wavelengths(points, arc, mile):
    step=.005
    p=np.asarray(_resample(points,step*mile))/mile
    if len(p)<4: return np.full(len(arc)-1,arc[-1])
    turns=np.diff(np.unwrap(np.arctan2(np.diff(p[:,1]),np.diff(p[:,0]))))
    changes=[0.0]; previous=0
    for i,turn in enumerate(turns):
        sign=previous if abs(turn)<math.radians(.4) else (1 if turn>0 else -1)
        if previous and sign!=previous: changes.append((i+1)*step)
        previous=sign
    changes.append(arc[-1])
    spans=np.diff(changes); centres=(np.asarray(changes[:-1])+np.asarray(changes[1:]))/2
    return 2*np.interp((arc[:-1]+arc[1:])/2,centres,spans)


def closest(a,b,c,d):
    def project(p,q,r):
        v=r-q; t=float(np.clip(np.dot(p-q,v)/max(1e-30,np.dot(v,v)),0,1))
        return q+t*v
    pairs=[(a,project(a,c,d)),(b,project(b,c,d)),(project(c,a,b),c),(project(d,a,b),d)]
    v=b-a; w=d-c; q=c-a
    cross=lambda p,r:float(p[0]*r[1]-p[1]*r[0])
    den=cross(v,w)
    if abs(den)>1e-20:
        t=cross(q,w)/den; u=cross(q,v)/den
        if 0<=t<=1 and 0<=u<=1: return [(a+t*v).tolist()]*2
    p,q=min(pairs,key=lambda pair:float(np.sum((pair[0]-pair[1])**2)))
    return [p.tolist(),q.tolist()]


def arrays(f, mile):
    p = np.asarray(f['points'], dtype=float)/mile
    a, b = p[:-1], p[1:]
    lengths = np.linalg.norm(b-a, axis=1)
    arc = np.r_[0, np.cumsum(lengths)]
    # Clip segments to the actual window without changing original arc stations.
    delta=b-a
    lo=np.zeros(len(a)); hi=np.ones(len(a)); valid=np.ones(len(a),dtype=bool)
    for axis in (0,1):
        moving=abs(delta[:,axis])>1e-20
        valid &= moving | ((a[:,axis]>=-20)&(a[:,axis]<=20))
        den=np.where(moving,delta[:,axis],1)
        t0=(-20-a[:,axis])/den; t1=(20-a[:,axis])/den
        lo=np.maximum(lo,np.where(moving,np.minimum(t0,t1),0))
        hi=np.minimum(hi,np.where(moving,np.maximum(t0,t1),1))
    valid &= hi>lo
    return a+lo[:,None]*delta,a+hi[:,None]*delta,arc,valid


def distances(a,b,c,d):
    """Squared segment distances, including interior intersections."""
    a,b,c,d = a[:,None,:],b[:,None,:],c[None,:,:],d[None,:,:]
    def point_seg(p,q,r):
        v=r-q
        t=np.clip(np.sum((p-q)*v,axis=-1)/np.maximum(1e-30,np.sum(v*v,axis=-1)),0,1)
        return np.sum((p-q-t[...,None]*v)**2,axis=-1)
    ds=np.minimum.reduce([point_seg(a,c,d),point_seg(b,c,d),point_seg(c,a,b),point_seg(d,a,b)])
    v=b-a; w=d-c; q=c-a
    cross=lambda x,y:x[...,0]*y[...,1]-x[...,1]*y[...,0]
    den=cross(v,w); safe=np.where(abs(den)>1e-20,den,1)
    t=cross(q,w)/safe; u=cross(q,v)/safe
    ds=np.where((abs(den)>1e-20)&(t>=0)&(t<=1)&(u>=0)&(u<=1),0,ds)
    return ds


def measure(features, seed):
    from world import MILE
    features=sorted(features,key=lambda f:f['id'])
    data=[arrays(f,MILE) for f in features]
    violations=0; worst=None; different=0; same=0
    for i,f in enumerate(features):
        a,b,arc,visible=data[i]
        width=f['width']/MILE
        wavelengths=local_wavelengths(f['points'],arc,MILE)
        buckets={}; cell=max(.02,2*width)
        def cells(p,q,pad=0):
            return ((x,y) for x in range(math.floor((min(p[0],q[0])-pad)/cell),math.floor((max(p[0],q[0])+pad)/cell)+1)
                    for y in range(math.floor((min(p[1],q[1])-pad)/cell),math.floor((max(p[1],q[1])+pad)/cell)+1))
        for k in np.flatnonzero(visible):
            possible=set()
            for key in cells(a[k],b[k],2*width): possible.update(buckets.get(key,()))
            indices=[j for j in sorted(possible) if arc[k]-arc[j+1]>max(wavelengths[k],wavelengths[j])]
            if indices:
                ds=distances(a[k:k+1],b[k:k+1],a[indices],b[indices])[0]
                bad=ds<(2*width)**2-1e-14
                count=int(np.count_nonzero(bad)); same+=count; violations+=count
                if count:
                    pos=int(np.argmin(ds)); j=indices[pos]; value=float(math.sqrt(ds[pos]))
                    row=dict(ids=[f['id'],f['id']],coordinates_mi=closest(a[k],b[k],a[j],b[j]),distance_mi=value,required_mi=2*width)
                    if worst is None or value-2*width < worst['distance_mi']-worst['required_mi']: worst=row
            for key in cells(a[k],b[k]): buckets.setdefault(key,[]).append(k)
        if f.get('stream_order',0)<2: continue
        for j in range(i+1,len(features)):
            g=features[j]
            if g.get('stream_order',0)<2: continue
            c,d,other_arc,other_visible=data[j]
            if np.any(np.maximum(np.minimum(a.min(axis=0),b.min(axis=0)),np.minimum(c.min(axis=0),d.min(axis=0)))-np.minimum(np.maximum(a.max(axis=0),b.max(axis=0)),np.maximum(c.max(axis=0),d.max(axis=0))) > .4): continue
            # A direct connection is exempt only in its half-mile junction pocket.
            valid_a=np.ones(len(a),dtype=bool); valid_c=np.ones(len(c),dtype=bool)
            if f.get('downstream')==g['id']:
                valid_a=arc[-1]-arc[1:]>=.5
                p=np.asarray(f['points'][-1])/MILE
                idx=int(np.argmin(np.linalg.norm(c-p,axis=1)))
                valid_c=np.minimum(abs(other_arc[:-1]-other_arc[idx]),abs(other_arc[1:]-other_arc[idx]))>=.5
            elif g.get('downstream')==f['id']:
                valid_c=other_arc[-1]-other_arc[1:]>=.5
                p=np.asarray(g['points'][-1])/MILE
                idx=int(np.argmin(np.linalg.norm(a-p,axis=1)))
                valid_a=np.minimum(abs(arc[:-1]-arc[idx]),abs(arc[1:]-arc[idx]))>=.5
            elif f.get('downstream')==g.get('downstream') and math.dist(f['points'][-1],g['points'][-1])<1e-6:
                valid_a=arc[-1]-arc[1:]>=.5
                valid_c=other_arc[-1]-other_arc[1:]>=.5
            valid_a &= visible; valid_c &= other_visible
            aa,bb,cc,dd=a[valid_a],b[valid_a],c[valid_c],d[valid_c]
            if not len(aa) or not len(cc): continue
            ds=distances(aa,bb,cc,dd)
            inds=np.unravel_index(np.argmin(ds),ds.shape); value=float(math.sqrt(ds[inds]))
            if value < .4-1e-10:
                different+=1; violations+=1
                row=dict(ids=[f['id'],g['id']],coordinates_mi=closest(aa[inds[0]],bb[inds[0]],cc[inds[1]],dd[inds[1]]),distance_mi=value,required_mi=.4)
                if worst is None or value-.4 < worst['distance_mi']-worst['required_mi']: worst=row
    return dict(violations=violations,different_channel_pairs=different,same_reach_segment_pairs=same,worst_pair=worst)


def run(seeds=SEEDS, quick=False, progress=print):
    return run_check('river_spacing',measure,lambda m:m['violations']==0,seeds,progress)


if __name__=='__main__': main(run)
