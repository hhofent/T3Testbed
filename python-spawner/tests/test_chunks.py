import time
import chunks
from settings import Settings
from presets import document
from region import MILE

def test_cache_priority_disk_and_fast_reuse(tmp_path,monkeypatch):
 monkeypatch.setattr(chunks,'CACHE',tmp_path)
 chunks.JOBS.clear()
 settings=Settings({'stages.houses':False,'stages.trees':False})
 data=dict(seed=13,settings=document(settings),window=[0,0,MILE,MILE])
 result=chunks.request(data,settings)
 deadline=time.monotonic()+30
 while result['pending']:
  assert time.monotonic()<deadline
  time.sleep(.02); result=chunks.request(data,settings)
 start=time.perf_counter()
 again=chunks.request(data,settings)
 assert time.perf_counter()-start<.2
 assert again==result
 assert len(result['chunks'])==1
 assert (tmp_path/'13'/chunks.identity(settings)/'0_0.svg').exists()
 assert chunks.identity(settings)==chunks.identity(Settings({**settings.values,'world.window_miles':6}))
 assert chunks.identity(settings)!=chunks.identity(Settings({**settings.values,'world.river_width_ft':110}))
 chunks.POOL.submit(lambda:None).result(timeout=30)
 chunks.JOBS.clear()
 assert chunks.request(data,settings)['chunks']==result['chunks']
