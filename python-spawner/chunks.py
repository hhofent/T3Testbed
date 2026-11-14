"""Prioritized, nonblocking world chunks, with separate overview/detail caches."""
import hashlib,json,math,threading
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor,Future
from settings import applied
from presets import plain
from region import MILE
from world import window,svg

POOL=ThreadPoolExecutor(max_workers=1,thread_name_prefix='world-chunks')
LOCK=threading.RLock()
JOBS={}
WAITING={}
ACTIVE=None
CACHE=Path(__file__).parent/'out'/'cache'

def identity(settings):
 values={k:v for k,v in settings.values.items() if not k.startswith(('controls.','verify.')) and k not in ('world.window_miles','world.pan_fraction','world.zoom_factor')}
 return hashlib.sha256(('lod-v7-water'+json.dumps(plain(values),sort_keys=True)).encode()).hexdigest()[:24]

def generate(seed,x,y,settings,path,detail=True):
 size=settings.values['world.chunk_miles']*MILE
 with applied(settings):
  result=svg(window(seed,[x*size,y*size,size,size],settings,detail=detail)).decode()
 path.parent.mkdir(parents=True,exist_ok=True)
 temporary=path.with_suffix('.tmp'); temporary.write_text(result,encoding='utf-8'); temporary.replace(path)
 return result

def pump():
 global ACTIVE
 with LOCK:
  if ACTIVE is not None or not WAITING: return
  key=min(WAITING,key=lambda k:WAITING[k][0])
  _,args=WAITING.pop(key)
  ACTIVE=key
  future=JOBS[key]
  work=POOL.submit(generate,*args)
  def complete(done):
   global ACTIVE
   with LOCK:
    try: future.set_result(done.result())
    except Exception as error: future.set_exception(error)
    ACTIVE=None
    pump()
  work.add_done_callback(complete)

def request(data,settings):
 seed=int(data['seed']); size=settings.values['world.chunk_miles']*MILE
 x,y,w,h=data['window']
 if not all(math.isfinite(v) for v in (x,y,w,h)) or min(w,h)<=0 or max(w,h)>96*MILE: raise ValueError('Invalid window')
 detail=data.get('detail',max(w,h)<=2*MILE)
 visible=[(a,b) for a in range(math.floor(x/size),math.ceil((x+w)/size)) for b in range(math.floor(y/size),math.ceil((y+h)/size))]
 ring=set((a+dx,b+dy) for a,b in visible for dx in (-1,0,1) for dy in (-1,0,1))-set(visible)
 direction=data.get('direction',[0,0]); dx,dy=[(v>0)-(v<0) for v in direction]
 ahead={(a+dx,b+dy) for a,b in ring} if dx or dy else set()
 key=identity(settings)+('-detail' if detail else '-overview'); output=[]
 known=set(data.get('known',[]))
 with LOCK:
  # Drop obsolete queued work; at most the current chunk precedes a new view.
  wanted={(seed,key,a,b) for a,b in visible+list(ring)+list(ahead)}
  for old in list(WAITING):
   if old not in wanted:
    WAITING.pop(old); JOBS.pop(old,None)
  for priority,group in enumerate((visible,sorted(ring),sorted(ahead-set(visible)-ring))):
   for a,b in group:
    jobkey=(seed,key,a,b); path=CACHE/str(seed)/key/f'{a}_{b}.svg'
    if jobkey not in JOBS:
     if path.exists(): JOBS[jobkey]=path.read_text(encoding='utf-8')
     else: JOBS[jobkey]=Future(); WAITING[jobkey]=(priority,(seed,a,b,settings,path,detail))
    elif jobkey in WAITING: WAITING[jobkey]=(priority,WAITING[jobkey][1])
    job=JOBS[jobkey]
    if not isinstance(job,str) and job.done(): job=JOBS[jobkey]=job.result()
    if (a,b) in visible: output.append(dict(x=a,y=b,ready=isinstance(job,str),svg=job if isinstance(job,str) and f'{a}:{b}' not in known else None))
  protected={(seed,key,a,b) for a,b in visible}
  for old in list(JOBS):
   if len(JOBS)<=settings.values['world.tile_cache_size']: break
   job=JOBS[old]
   if old not in protected and (isinstance(job,str) or job.done()): del JOBS[old]
  pump()
 return dict(key=f'{seed}:{key}',size=size,chunks=output,pending=any(not c['ready'] for c in output))
