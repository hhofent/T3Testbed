import time
import chunks
from settings import Settings
from presets import document
from region import MILE

def test_cache_priority_disk_and_fast_reuse(tmp_path,monkeypatch):
 monkeypatch.setattr(chunks,'CACHE',tmp_path)
 # This test is about the cache protocol; geometry generation has dedicated
 # tests and the extended river checks. Keep the normal suite independent of
 # the expensive renderer by returning a stable payload here.
 def fake_generate(seed,x,y,settings,path,detail=True):
  result='<svg cache-smoke="true"/>'
  path.parent.mkdir(parents=True,exist_ok=True)
  path.write_text(result,encoding='utf-8')
  return result
 monkeypatch.setattr(chunks,'generate',fake_generate)
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
 def forbidden(*args,**kwargs): raise AssertionError('overview generated house plans')
 monkeypatch.setattr(world,'spawn_region',forbidden)
 monkeypatch.setattr(world,'rivers',lambda seed,rect:())
 monkeypatch.setattr(world,'_route_offset',lambda *args:0)
 monkeypatch.setattr(world,'terrain',lambda seed,rect:())
 def tile(seed,tx,ty,*args,**kwargs):
  x,y=tx*3*MILE,ty*3*MILE
  return [world.feature('roads',f'smoke-road:{tx}:{ty}',rect=(x,y+MILE,3*MILE,100),surface='gravel'),
          world.feature('farms',f'smoke-farm:{tx}:{ty}',rect=(x+MILE//4,y+MILE//4,MILE//2,MILE//2),use='farmstead')]
 monkeypatch.setattr(world,'_tile',tile)
 monkeypatch.setattr(world,'settlements',lambda *args,**kwargs:[world.feature('towns','smoke-town',rect=(MILE,MILE,MILE,MILE),size='tiny')])
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

def test_far_chunk_svg_uses_local_geometry():
 import xml.etree.ElementTree as ET
 import world
 distance=-198*MILE
 rect=(distance,distance,3*MILE,3*MILE)
 road=world.feature('roads','far-road',rect=(distance+MILE,distance+2*MILE,MILE,22*192),surface='gravel')
 scene={'seed':14,'window':rect,'features':[road]}
 root=ET.fromstring(world.svg(scene,local=True))
 assert root.attrib['viewBox']==f'0 0 {3*MILE} {3*MILE}'
 assert len(root)>=2 # background plus the synthetic road feature
 assert root.find('.//{*}g[@data-kind="roads"]') is not None
 for item in root.findall('.//{*}rect'):
  assert abs(float(item.attrib['x']))<10*MILE
  assert abs(float(item.attrib['y']))<10*MILE
