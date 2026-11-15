"""Segment-exact clearance on finished channels; distances in miles.

Counts violating reach pairs (and violating nonadjacent segment pairs within
each reach). Shared junction pockets are excluded for 0.5 mi of arc length.
"""
import math
import json
from pathlib import Path
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
    from world import MILE, _basin_polygon, _basin_angle, _rot, _point_in_polygon, elevation, _route_curve
    features=sorted(features,key=lambda f:f['id'])
    by_id={f['id']:f for f in features}
    incoming={}
    for f in features: incoming.setdefault(tuple(f['points'][-1]),[]).append(f)
    data=[arrays(f,MILE) for f in features]
    violations=0; worst=None; different=0; same=0; pairs=[]
    for i,f in enumerate(features):
        a,b,arc,visible=data[i]
        width=f['width']/MILE
        wavelengths=local_wavelengths(f['points'],arc,MILE)
        own_visible=visible.copy()
        if len(incoming.get(tuple(f['points'][0]),()))>1 or f.get('_confluence_inflows',1)>1:
            own_visible &= arc[:-1]>=.5
        receiver=by_id.get(f.get('downstream'))
        confluence=receiver is not None and (len(incoming.get(tuple(f['points'][-1]),()))>1 or math.dist(receiver['points'][0],f['points'][-1])>.01)
        if confluence: own_visible &= arc[-1]-arc[1:]>=.5
        buckets={}; cell=max(.02,2*width)
        def cells(p,q,pad=0):
            return ((x,y) for x in range(math.floor((min(p[0],q[0])-pad)/cell),math.floor((max(p[0],q[0])+pad)/cell)+1)
                    for y in range(math.floor((min(p[1],q[1])-pad)/cell),math.floor((max(p[1],q[1])+pad)/cell)+1))
        for k in np.flatnonzero(own_visible):
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
                pairs.append(row)
                if worst is None or value-.4 < worst['distance_mi']-worst['required_mi']: worst=row
    basin_stats={tuple(f['basin']):f['_relocation_basin_stats'] for f in features if '_relocation_basin_stats' in f}
    in_window=lambda row: all(-20<=v<=20 for v in row['original_mi'])
    moved=[dict(basin=list(cell),**row) for cell,stats in sorted(basin_stats.items())
           for row in stats.get('moved_nodes',[]) if in_window(row)]
    blocked=[dict(basin=list(cell),**row) for cell,stats in sorted(basin_stats.items())
             for row in stats['blocked_nodes'] if in_window(row)]
    guard_failures=[]
    for f in features:
        if f['id'].startswith('river:outlet:'):
            cx,cy=map(int,f['id'].split(':')[-2:])
            _,canonical=_route_curve(seed,cx,cy)
            if tuple(f['points'][0])!=tuple(canonical[0]) or tuple(f['points'][-1])!=tuple(canonical[-1]):
                guard_failures.append(dict(id=f['id'],reason='outlet node moved'))
        before=f.get('_spacing_graph_before')
        if before and any(f[key]!=value for key,value in before.items()): guard_failures.append(dict(id=f['id'],reason='graph or width changed'))
        ends=f.get('_spacing_width_ends_before')
        if ends and [f['widths'][0],f['widths'][-1]]!=ends: guard_failures.append(dict(id=f['id'],reason='endpoint widths changed'))
        row=f.get('_node_relocation')
        if row and row['status']=='moved':
            p=tuple(v*MILE for v in row['position_mi']); cell=tuple(f['basin'])
            if row['movement_mi']>.5+1e-9: guard_failures.append(dict(id=f['id'],reason='movement exceeds 0.5 mi'))
            if not _point_in_polygon(_rot(*p,_basin_angle(seed)),_basin_polygon(seed,*cell)):
                guard_failures.append(dict(id=f['id'],reason='moved outside basin'))
            for ident,end in row['incident']:
                incident=by_id.get(ident)
                if incident is None: continue
                if math.dist(incident['points'][end],p)>.001:
                    guard_failures.append(dict(id=ident,reason='incident endpoint not moved with node'))
                if elevation(seed,*incident['points'][0])<=elevation(seed,*incident['points'][-1]):
                    guard_failures.append(dict(id=ident,reason='moved edge not strictly downhill'))
    from river_geometry import finished_crossings
    from river_meander import measure as meander_measure
    crossings=finished_crossings(seed,features)
    geometry=meander_measure(features,MILE)
    if crossings: guard_failures.append(dict(reason='finished crossings',pairs=crossings[:20]))
    for name in ('self_intersections','reversals','kinks'):
        if geometry[name]: guard_failures.append(dict(reason=name,count=geometry[name]))
    baseline=json.loads(Path(__file__).with_name('a2-baseline-spacing-v2.json').read_text(encoding='utf-8'))
    before=baseline['per_seed'].get(str(seed),{}).get('violations')
    return dict(violations=violations,different_channel_pairs=different,same_reach_segment_pairs=same,worst_pair=worst,pairs=pairs,
                violations_before_db78343=before,finished_crossings=len(crossings),
                self_intersections=geometry['self_intersections'],reversals=geometry['reversals'],kinks=geometry['kinks'],
                nodes_moved=len(moved),node_move_mean_mi=sum(r['movement_mi'] for r in moved)/max(1,len(moved)),
                node_move_max_mi=max((r['movement_mi'] for r in moved),default=0),
                moved_nodes=moved,remaining_fixed_nodes=blocked,relocation_guard_failures=guard_failures,
                relocation_scope='nodes originally inside the 40-mi window',
                reseeded_reaches=sum(bool(f.get('_spacing_reseeded')) for f in features),
                straight_fallbacks=sum(f.get('_spacing_fallback')=='straight' for f in features),
                valley_fallbacks=sum(f.get('_spacing_fallback')=='valley' for f in features))


def run(seeds=SEEDS, quick=False, progress=print):
    results,metrics,failures=run_check('river_spacing',measure,lambda m:m['violations']==0 and not m['relocation_guard_failures'],seeds,progress)
    for seed,m in metrics['river_spacing']['per_seed'].items():
        results.append(dict(name=f'relocation and geometry limits seed {seed}',passed=not m['relocation_guard_failures'],
                            measured=f"{m['nodes_moved']} moved, mean/max {m['node_move_mean_mi']:.6f}/{m['node_move_max_mi']:.6f} mi; {len(m['remaining_fixed_nodes'])} constrained nodes",
                            target='same graph/widths, <=0.5 mi, in basin, downhill, fixed outlets, no crossings/reversals/kinks'))
    return results,metrics,failures


if __name__=='__main__': main(run)
