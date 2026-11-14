"""Deterministic parameter sweeps and a shared statistics vocabulary."""
import csv,math
from pathlib import Path
from settings import Settings
from region import make_region,spawn_region,MILE

def statistics(region,results=None):
 lots=region['lots']; town=region['town']; shops=town['commercial'] if town else []
 built=[v['plan'] for v in (results or {}).values() if v['plan']]
 trailers=sum(l['spec'].get('house_type')=='trailer' for l in lots)
 vacant=sum(bool(c.get('vacant')) for c in shops)
 return dict(houses=len(built) if results is not None else len(lots),planned_houses=len(lots),
   failed_houses=len(lots)-len(built) if results is not None else 0,towns=int(bool(town)),trailers=trailers,
   trailer_share=trailers/max(1,len(lots)),vacant_businesses=vacant,vacancy=vacant/max(1,len(shops)),
   businesses=len(shops),road_miles=region['road_miles'],paved_share=region['paved_share'],
   rail_miles=sum(math.dist(a,b) for a,b in zip(region['rail']['pts'],region['rail']['pts'][1:]))/MILE if region['rail'] else 0,
   creeks=len(region['creeks']),farms=sum(l['spec']['kind']=='farmstead' for l in lots),
   acreages=sum(l['spec']['kind']=='acreage' for l in lots),snapshot_year=town['year'] if town else '')

def get_value(settings,key):
 if key in settings.values: return settings.values[key]
 parent=next((k for k in sorted(settings.values,key=len,reverse=True) if key.startswith(k+'.')),None)
 if parent is None: raise KeyError(key)
 obj=settings.values[parent]
 for part in key[len(parent)+1:].split('.'):
  obj=obj[int(part) if isinstance(obj,(list,tuple)) or (part.isdigit() and int(part) in obj) else part]
 return obj

def sweep(settings,param,lo,hi,steps,seeds,path,first_seed=0):
 if steps<1 or seeds<1: raise ValueError('steps and seeds must be positive')
 if not math.isfinite(lo) or not math.isfinite(hi): raise ValueError('Sweep endpoints must be finite')
 if type(get_value(settings,param)) not in (int,float): raise ValueError('Sweep requires a numeric leaf setting')
 path=Path(path); path.parent.mkdir(parents=True,exist_ok=True)
 temp=path.with_suffix(path.suffix+'.tmp')
 try:
  with temp.open('w',newline='',encoding='utf-8') as file:
   writer=None
   for i in range(steps):
    requested=lo+(hi-lo)*i/(steps-1) if steps>1 else lo
    selected=Settings(settings.values).set(param,requested)
    for seed in range(first_seed,first_seed+seeds):
     reg=make_region(seed,settings=selected); results=spawn_region(reg,settings=selected)
     row=dict(parameter=param,requested=requested,value=get_value(selected,param),seed=seed,**statistics(reg,results))
     if writer is None: writer=csv.DictWriter(file,fieldnames=list(row)); writer.writeheader()
     writer.writerow(row)
  temp.replace(path)
 finally:
  if temp.exists(): temp.unlink()
 return path
