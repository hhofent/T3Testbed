"""Nonblocking, persistent world chunk service. Generation stays off HTTP threads."""
import hashlib,json,math,threading
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from settings import applied
from presets import document,plain
from region import MILE
from world import window,svg

POOL=ThreadPoolExecutor(max_workers=1,thread_name_prefix='world-chunks')
LOCK=threading.Lock()
JOBS={}
CACHE=Path(__file__).parent/'out'/'cache'

def identity(settings):
 values={k:v for k,v in settings.values.items() if not k.startswith(('controls.','verify.')) and k not in ('world.window_miles','world.pan_fraction','world.zoom_factor')}
 return hashlib.sha256(json.dumps(plain(values),sort_keys=True).encode()).hexdigest()[:24]

def generate(seed,x,y,settings,path):
 size=settings.values['world.chunk_miles']*MILE
 with applied(settings):
  result=svg(window(seed,[x*size,y*size,size,size],settings)).decode()
 path.parent.mkdir(parents=True,exist_ok=True)
 temporary=path.with_suffix('.tmp'); temporary.write_text(result,encoding='utf-8'); temporary.replace(path)
 return result

def request(data,settings):
 seed=int(data['seed']); size=settings.values['world.chunk_miles']*MILE
 x,y,w,h=data['window']
 if not all(math.isfinite(v) for v in (x,y,w,h)) or min(w,h)<=0 or max(w,h)>12*MILE: raise ValueError('Invalid window')
 visible=[(a,b) for a in range(math.floor(x/size),math.ceil((x+w)/size)) for b in range(math.floor(y/size),math.ceil((y+h)/size))]
 ring=set((a+dx,b+dy) for a,b in visible for dx in (-1,0,1) for dy in (-1,0,1))-set(visible)
 direction=data.get('direction',[0,0]); dx,dy=[(v>0)-(v<0) for v in direction]
 ahead={(a+dx,b+dy) for a,b in ring} if dx or dy else set()
 key=identity(settings); output=[]
 with LOCK:
  for a,b in visible+sorted(ring)+sorted(ahead-set(visible)-ring):
   jobkey=(seed,key,a,b); path=CACHE/str(seed)/key/f'{a}_{b}.svg'
   if jobkey not in JOBS:
    if path.exists(): JOBS[jobkey]=path.read_text(encoding='utf-8')
    else: JOBS[jobkey]=POOL.submit(generate,seed,a,b,settings,path)
   job=JOBS[jobkey]
   if not isinstance(job,str) and job.done(): job=JOBS[jobkey]=job.result()
   if (a,b) in visible: output.append(dict(x=a,y=b,svg=job if isinstance(job,str) else None))
   # Bound completed in-memory entries; evicted chunks remain available on disk.
  protected={(seed,key,a,b) for a,b in visible}
  for old in list(JOBS):
   if len(JOBS)<=settings.values['world.tile_cache_size']: break
   job=JOBS[old]
   if old not in protected and (isinstance(job,str) or job.done()): del JOBS[old]
 return dict(key=f'{seed}:{key}',size=size,chunks=output,pending=any(c['svg'] is None for c in output))
