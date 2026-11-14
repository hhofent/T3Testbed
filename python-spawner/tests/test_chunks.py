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
 assert (tmp_path/'13'/(chunks.identity(settings)+'-detail')/'0_0.svg').exists()
 assert chunks.identity(settings)==chunks.identity(Settings({**settings.values,'world.window_miles':6}))
 assert chunks.identity(settings)!=chunks.identity(Settings({**settings.values,'world.river_width_ft':110}))
 chunks.POOL.submit(lambda:None).result(timeout=30)
 chunks.JOBS.clear()
 assert chunks.request(data,settings)['chunks']==result['chunks']


def test_overview_skips_house_plans_and_matches_towns(monkeypatch):
 import world,json
 from presets import document
 settings=Settings()
 world._tile.cache_clear()
 def forbidden(*args,**kwargs): raise AssertionError('overview generated house plans')
 monkeypatch.setattr(world,'spawn_region',forbidden)
 scene=world.window(31,[0,0,3*MILE,3*MILE],settings,detail=False)
 assert not any(f['kind'] in ('buildings','trees','driveways','lots') for f in scene['features'])
 assert any(f['kind']=='roads' for f in scene['features'])
 assert any(f['kind']=='farms' for f in scene['features'])
 assert b'<svg' in world.svg(scene)


def test_twenty_miles_negative_coordinates_and_known_payload(tmp_path,monkeypatch):
 monkeypatch.setattr(chunks,'CACHE',tmp_path)
 monkeypatch.setattr(chunks,'generate',lambda *args:'<svg/>')
 settings=Settings()
 data=dict(seed=1313,window=[-10*MILE,-10*MILE,20*MILE,20*MILE],detail=False)
 deadline=time.monotonic()+5
 result=chunks.request(data,settings)
 while result['pending']:
  assert time.monotonic()<deadline
  time.sleep(.01); result=chunks.request(data,settings)
 assert len(result['chunks'])==64
 known=[f"{c['x']}:{c['y']}" for c in result['chunks']]
 reused=chunks.request(dict(data,known=known),settings)
 assert not reused['pending'] and all(c['svg'] is None and c['ready'] for c in reused['chunks'])
 assert reused['key'].endswith('-overview')
