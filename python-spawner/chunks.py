"""Prioritized, nonblocking world chunks, with separate overview/detail caches."""
import hashlib,json,math,subprocess,threading,time
from pathlib import Path
from concurrent.futures import Future
from chunk_worker import ChunkWorker
from settings import applied
from presets import plain
from region import MILE
from world import window,svg,feature
from generation_progress import listen
from generation_preview import Preview

POOL=ChunkWorker()
LOCK=threading.RLock()
JOBS={}
PREVIEWS={}
LIVE={}
WAITING={}
ACTIVE=None
ACTIVE_STARTED=None
STOPPING=False

def shutdown():
 """Discard pending work; running generation cannot hold the server open."""
 global STOPPING
 with LOCK:
  STOPPING=True
  WAITING.clear()
  for job in JOBS.values():
   if isinstance(job,Future): job.cancel()
  POOL.shutdown(wait=False,cancel_futures=True)

def start():
 global POOL,STOPPING,ACTIVE,ACTIVE_STARTED
 with LOCK:
  if STOPPING:
   POOL=ChunkWorker(); STOPPING=False; ACTIVE=None; ACTIVE_STARTED=None
   JOBS.clear(); WAITING.clear(); PREVIEWS.clear(); LIVE.clear()

def git_short_hash():
 """Short HEAD hash, or nogit when git cannot be run."""
 try:
  text=subprocess.check_output(['git','rev-parse','--short','HEAD'],cwd=Path(__file__).resolve().parent,text=True,stderr=subprocess.DEVNULL,timeout=5).strip()
  return text or 'nogit'
 except Exception:
  return 'nogit'

def cache_version_tag():
 version=(Path(__file__).resolve().parent/'VERSION').read_text(encoding='utf-8').strip()
 return f'{version}-{git_short_hash()}'

CACHE=Path(__file__).parent/'out'/'cache'/cache_version_tag()

def identity(settings):
 values={k:v for k,v in settings.values.items() if not k.startswith(('controls.','verify.')) and k not in ('world.window_miles','world.pan_fraction','world.zoom_factor')}
 return hashlib.sha256(('lod-v30-river-road-town-narrow-lot'+json.dumps(plain(values),sort_keys=True)).encode()).hexdigest()[:24]

def preview_svg(seed,x,y,settings):
 """Cheap provisional section grid shown while basin-backed details compute."""
 size=settings.values['world.chunk_miles']*MILE; rect=(x*size,y*size,size,size); out=[]
 for line in range(math.floor(y*size/MILE),math.ceil((y+1)*size/MILE)+1):
  out.append(feature('roads',f'preview:h:{seed}:{line}:{x}',rect=(rect[0],line*MILE-11*192,size,22*192),surface='gravel',axis='h'))
 for line in range(math.floor(x*size/MILE),math.ceil((x+1)*size/MILE)+1):
  out.append(feature('roads',f'preview:v:{seed}:{line}:{y}',rect=(line*MILE-11*192,rect[1],22*192,size),surface='gravel',axis='v'))
 return svg({'window':rect,'features':out},local=True).decode()

def generate(seed,x,y,settings,path,detail=True):
 size=settings.values['world.chunk_miles']*MILE
 key=(seed,identity(settings)+('-detail' if detail else '-overview'))
 preview=Preview()
 with LOCK:
  previous=LIVE.get(key)
  if previous:
   preview.features=previous.features.copy();preview.basins=previous.basins
  LIVE[key]=preview
  for old in list(LIVE):
   if len(LIVE)>2 and old!=key:del LIVE[old]
 def update(*args,**kwargs):
  with LOCK:
   if not STOPPING and LIVE.get(key) is preview:preview.update(*args,**kwargs)
 with applied(settings),listen(update):
  result=svg(window(seed,[x*size,y*size,size,size],settings,detail=detail),local=True).decode()
 path.parent.mkdir(parents=True,exist_ok=True)
 temporary=path.with_suffix('.tmp'); temporary.write_text(result,encoding='utf-8'); temporary.replace(path)
 return result

def pump():
 global ACTIVE,ACTIVE_STARTED
 with LOCK:
  if STOPPING or ACTIVE is not None or not WAITING: return
  key=min(WAITING,key=lambda k:WAITING[k][0])
  _,args=WAITING.pop(key)
  ACTIVE=key
  ACTIVE_STARTED=time.monotonic()
  future=JOBS[key]
  pool=POOL
  try: work=POOL.submit(generate,*args)
  except Exception as error:
   ACTIVE=None; ACTIVE_STARTED=None
   future.set_exception(error)
   raise
  def complete(done):
   global ACTIVE,ACTIVE_STARTED
   with LOCK:
    if STOPPING or pool is not POOL: return
    try: future.set_result(done.result())
    except Exception as error: future.set_exception(error)
    ACTIVE=None; ACTIVE_STARTED=None
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
 known=set(data.get('known',[]));known_previews=data.get('known_previews',{})
 with LOCK:
  if STOPPING: raise RuntimeError('Chunk worker is stopped; restart the server')
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
    if (a,b) in visible:
     preview=None;version=None
     if not isinstance(job,str):
      live=LIVE.get((seed,key))
      version=f'{id(live)}:{live.version}' if live else 'grid'
      if known_previews.get(f'{a}:{b}')!=version:
       cached=PREVIEWS.get(jobkey)
       if cached and cached[0]==version: preview=cached[1]
       else:
        rect=(a*size,b*size,size,size)
        preview=live.svg(rect).decode() if live else preview_svg(seed,a,b,settings)
        PREVIEWS[jobkey]=(version,preview)
     else:PREVIEWS.pop(jobkey,None)
     output.append(dict(x=a,y=b,ready=isinstance(job,str),svg=job if isinstance(job,str) and f'{a}:{b}' not in known else None,preview=preview,preview_version=version))
  protected={(seed,key,a,b) for a,b in visible}
  for old in list(JOBS):
   if len(JOBS)<=settings.values['world.tile_cache_size']: break
   job=JOBS[old]
   if old not in protected and (isinstance(job,str) or job.done()):
    del JOBS[old];PREVIEWS.pop(old,None)
  pump()
  active_chunk={'x':ACTIVE[2],'y':ACTIVE[3]} if ACTIVE and ACTIVE[0]==seed and ACTIVE[1]==key else None
  worker=dict(seed=ACTIVE[0],x=ACTIVE[2],y=ACTIVE[3],elapsed_seconds=round(time.monotonic()-ACTIVE_STARTED,1)) if ACTIVE else None
  waiting=len(WAITING)
  completed=sum(c['ready'] for c in output);pending_chunks=completed<len(visible)
  live=LIVE.get((seed,key))
  portion=live.percent/100 if live and active_chunk and (ACTIVE[2],ACTIVE[3]) in visible else 0
  progress=dict(percent=round(100*(completed+portion)/len(visible),1) if pending_chunks else 100,stage=live.stage if live and active_chunk else 'Waiting for worker' if pending_chunks else 'Ready',estimated=pending_chunks)
 return dict(key=f'{seed}:{key}',size=size,chunks=output,pending=pending_chunks,total=len(visible),completed=completed,waiting=waiting,active=active_chunk,worker=worker,basin_miles=32,progress=progress)
