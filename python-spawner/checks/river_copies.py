"""Arc-length normalized curvature and turning-function copy detector.

All order-1/2 features, including split pieces, are compared within each basin.
Epsilon is calibrated from the synthetic noisy-copy control (its 95th percentile
turning distance), never from the network's desired passing rate.
"""
import math
import subprocess
import types
import numpy as np
from a2_common import SEEDS, WINDOW_MI, run_check, main
from river_junctions import station


def signature(points):
    length=sum(math.dist(a,b) for a,b in zip(points,points[1:]))
    p=np.asarray([station(points,length*i/63) for i in range(64)])
    p=(p-p[0])/max(length,1e-30)
    chord=p[-1]; angle=math.atan2(chord[1],chord[0])
    c,s=math.cos(angle),math.sin(angle)
    p=p @ np.array([[c,-s],[s,c]])
    headings=np.unwrap(np.arctan2(np.diff(p[:,1]),np.diff(p[:,0])))
    curvature=np.diff(headings)*63
    return curvature,headings


def compare(features, eps):
    groups={}
    for f in features:
        if f.get('stream_order',99)<=2:
            groups.setdefault(tuple(f.get('basin',(0,0))),[]).append(f)
    pairs=flagged=0; distances=[]; correlations=[]
    for group in groups.values():
        if len(group)<2: continue
        signatures=[signature(f['points']) for f in group]
        curves=np.asarray([v[0] for v in signatures]); turns=np.asarray([v[1] for v in signatures])
        curves-=curves.mean(axis=1,keepdims=True)
        norms=np.linalg.norm(curves,axis=1,keepdims=True)
        curves/=np.maximum(norms,1e-30)
        corr=curves@curves.T
        for i in range(len(group)-1):
            ds=np.sqrt(np.mean((turns[i+1:]-turns[i])**2,axis=1))
            rs=corr[i,i+1:]
            pairs+=len(ds); flagged+=int(np.count_nonzero((rs>=.9)|(ds<eps)))
            distances.extend(ds.tolist()); correlations.extend(rs.tolist())
    return dict(pairs=pairs,flagged=flagged,rate=flagged/max(1,pairs),
                turning_distance=dict(zip(('p1','p5','median','p95'),map(float,np.percentile(distances,[1,5,50,95])))) if distances else {},
                correlation=dict(zip(('p1','p5','median','p95'),map(float,np.percentile(correlations,[1,5,50,95])))) if correlations else {})


def synthetic():
    from world import _edge_curve,MILE,FT,H
    base=_edge_curve(14,'copy-control',(0,0),(MILE,0),FT(14))
    out=[]
    for i in range(20):
        angle=2*math.pi*(H(14,'copy-rotation',i)%100000)/100000
        scale=.5+2*(H(14,'copy-scale',i)%100000)/100000
        c,s=math.cos(angle),math.sin(angle)
        points=[]
        for j,(x,y) in enumerate(base):
            # Smooth small noise, rather than vertex jitter that dominates curvature.
            noise=.0005*MILE*math.sin(math.pi*j/(len(base)-1))*(2*(H(14,'copy-noise',i)%10000)/10000-1)
            y+=noise
            points.append((scale*(c*x-s*y)+i*MILE,scale*(s*x+c*y)))
        out.append(dict(id=f'control:{i}',points=points,stream_order=1,basin=(0,0)))
    return out


def calibration():
    return compare(synthetic(),0)['turning_distance']['p95']


def measure(features, seed):
    eps=calibration()
    return dict(epsilon=eps,**compare(features,eps))


def controls():
    from settings import Settings,applied
    from world import MILE
    eps=calibration()
    old=types.ModuleType('a2_old_world')
    source=subprocess.check_output(['git','show','b2ee52f:python-spawner/world.py'],text=True,encoding='utf-8')
    exec(compile(source,'b2ee52f:world.py','exec'),old.__dict__)
    with applied(Settings()):
        features=list(old.rivers(14,tuple(v*MILE for v in WINDOW_MI)))
    result=dict(epsilon=eps,synthetic=compare(synthetic(),eps),old_template=compare(features,eps))
    print('negative controls: '+str(result),flush=True)
    return result


def run(seeds=SEEDS, quick=False, progress=print):
    results,metrics,failures=run_check('river_copies',measure,lambda m:m['pairs']>0 and m['rate']<.05,seeds,progress)
    control=controls(); metrics['river_copies']['controls']=control
    for label in ('synthetic','old_template'):
        passed=control[label]['rate']>=.05
        results.append(dict(name='copy negative control '+label,passed=passed,measured=str(control[label]),target='network acceptance must FAIL: >=5% copies'))
        if not passed: failures.append(dict(check='copy control '+label,seed=14,coordinates=[0,0],feature_ids=[],message=str(control[label])))
    return results,metrics,failures


if __name__=='__main__': main(run)
