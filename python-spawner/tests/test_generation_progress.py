import threading
import xml.etree.ElementTree as ET


def test_preview_replaces_basin_with_partial_rivers_without_mutating_source():
 from generation_preview import Preview
 from world import feature,MILE
 preview=Preview()
 preview.update('Basins',.05,basins=[[(0,0),(MILE,0),(MILE,MILE),(0,MILE)]])
 first=preview.svg((0,0,MILE,MILE))
 assert b'data-kind="basins"' in first
 river=feature('rivers','river:test',points=[(0,0),(MILE,MILE)],width=100)
 preview.update('River channels',.4,features=[river])
 second=preview.svg((0,0,MILE,MILE))
 assert b'data-kind="rivers"' in second
 river['points'][0]=(999,999)
 assert preview.svg((0,0,MILE,MILE))==second
 preview.update('Refining rivers',.3)
 assert preview.percent==40  # A later basin/phase must not move the meter backwards.
 preview.update('Roads',.8,features=[feature('roads','road:test',rect=(0,0,MILE,22*192),surface='gravel')])
 assert b'data-kind="roads"' in preview.svg((0,0,MILE,MILE))


def test_real_window_output_is_identical_with_observer(monkeypatch):
 import world
 from generation_progress import listen
 from settings import Settings
 from world import MILE
 monkeypatch.setattr(world,'rivers',lambda *args:iter([world.feature('rivers','river:test',points=[(0,0),(MILE,MILE)],width=100)]))
 monkeypatch.setattr(world,'terrain',lambda *args:())
 monkeypatch.setattr(world,'paved_routes',lambda *args:())
 monkeypatch.setattr(world,'_tile',lambda *args:())
 monkeypatch.setattr(world,'settlements',lambda *args:())
 monkeypatch.setattr(world,'rail_lanes',lambda *args:())
 monkeypatch.setattr(world,'_route_offset',lambda *args:0)
 settings=Settings();rect=(0,0,MILE,MILE)
 expected=world.svg(world.window(31,rect,settings,detail=False))
 events=[]
 with listen(lambda *args,**kwargs:events.append((args,kwargs))):
  actual=world.svg(world.window(31,rect,settings,detail=False))
 assert actual==expected
 assert {'Rivers','Roads','Rail','Towns','Countryside'} <= {a[0] for a,k in events}


def test_pending_chunk_streams_then_final_replaces_preview(monkeypatch,tmp_path):
 import chunks
 from chunk_worker import ChunkWorker
 from generation_progress import publish
 from settings import Settings
 from world import MILE,feature
 entered=threading.Event();release=threading.Event();pool=ChunkWorker()
 scene=dict(window=(0,0,3*MILE,3*MILE),features=[feature('roads','road:final',rect=(0,0,MILE,22*192),surface='gravel')])
 def staged(*args,**kwargs):
  publish('Basins',.05,basins=[[(0,0),(MILE,0),(MILE,MILE)]])
  publish('River channels',.4,features=[feature('rivers','river:temporary',points=[(0,0),(MILE,MILE)],width=100)])
  entered.set();release.wait(3)
  return scene
 monkeypatch.setattr(chunks,'POOL',pool);monkeypatch.setattr(chunks,'CACHE',tmp_path)
 for name in ('JOBS','WAITING','PREVIEWS','LIVE'):monkeypatch.setattr(chunks,name,{})
 monkeypatch.setattr(chunks,'ACTIVE',None);monkeypatch.setattr(chunks,'window',staged)
 data=dict(seed=31,window=[0,0,MILE,MILE],detail=False)
 try:
  chunks.request(data,Settings());assert entered.wait(2)
  pending=chunks.request(data,Settings());tile=pending['chunks'][0]
  assert pending['pending'] and not tile['ready']
  assert 0<pending['progress']['percent']<100
  assert 'River' in pending['progress']['stage']
  assert 'river:temporary' in tile['preview']
  same=chunks.request(dict(data,known_previews={'0:0':tile['preview_version']}),Settings())
  assert same['chunks'][0]['preview'] is None
  release.set()
  import time
  deadline=time.monotonic()+3
  while True:
   done=chunks.request(data,Settings())
   if not done['pending']:break
   assert time.monotonic()<deadline;time.sleep(.01)
  assert done['progress']['percent']==100
  assert done['chunks'][0]['svg']==chunks.svg(scene,local=True).decode()
  assert 'river:temporary' not in done['chunks'][0]['svg']
 finally:
  release.set();pool.shutdown(wait=True)


def test_page_has_accessible_meter_and_versioned_partial_previews():
 from pathlib import Path
 page=(Path(__file__).resolve().parents[1]/'control.html').read_text(encoding='utf-8')
 assert '<progress id="generationProgress"' in page
 assert 'Estimated generation progress' in page
 assert 'known_previews' in page and 'preview_version' in page


def test_browser_meter_displays_stage_estimate_then_complete():
 import re,subprocess
 from pathlib import Path
 source=(Path(__file__).resolve().parents[1]/'control.html').read_text(encoding='utf-8')
 helper=re.search(r'function updateGenerationProgress\(data\).*?(?=\nfunction trimChunkImages)',source).group()
 script="const nodes={generationProgress:{setAttribute(k,v){this[k]=v}},generationPercent:{}};const $=id=>nodes[id];\n"+helper+"""
 updateGenerationProgress({pending:true,progress:{percent:34.5}});
 if(nodes.generationProgress.value!==34.5)throw Error('wrong meter value');
 if(!nodes.generationPercent.textContent.includes('34.5%'))throw Error('missing percentage');
 if(!nodes.generationProgress['aria-valuetext'].includes('temporary'))throw Error('missing temporary label');
 updateGenerationProgress({pending:false,progress:{percent:100}});
 if(nodes.generationProgress.value!==100||!nodes.generationPercent.textContent.includes('Ready'))throw Error('not complete');
 """
 subprocess.run(['node','-e',script],check=True,capture_output=True,text=True)
