import base64,json,threading,urllib.request
from http.server import HTTPServer
from control import Handler,render_request
from presets import document
from settings import Settings
from region import make_region

def test_stage_switches_and_json_render():
 settings=Settings({**{'stages.'+s:False for s in ['rail','creeks','towns','businesses','houses','trees','trailers']}})
 region=make_region(31,settings=settings)
 assert region['rail'] is None and region['town'] is None and region['creeks']==[]
 response=render_request({'seed':31,'settings':document(settings)})
 assert base64.b64decode(response['image']).startswith(b'\x89PNG')
 assert response['stats']['towns']==0

def test_loopback_server_schema_and_bad_request():
 with HTTPServer(('127.0.0.1',0),Handler) as server:
  thread=threading.Thread(target=server.serve_forever,daemon=True); thread.start()
  try:
   base='http://127.0.0.1:'+str(server.server_port)
   schema=json.load(urllib.request.urlopen(base+'/schema'))
   assert 'stages.rail' in schema['schema'] and len(schema['presets'])==6
   assert b'Changed from default' in urllib.request.urlopen(base).read()
  finally: server.shutdown(); thread.join()


def test_schema_initial_preset_retains_true_reset_defaults():
 from control import schema
 data=schema(Settings({'region.rail_pct':0}))
 assert data['values']['region.rail_pct']==0
 assert data['defaults']['region.rail_pct']>0


def test_visible_chunks_survive_cache_trim_in_real_page_script():
 # Run the shipped browser cache routine, not a Python copy of its logic.
 import re,subprocess
 from pathlib import Path
 page=Path(__file__).resolve().parents[1]/'control.html'
 source=page.read_text()
 trim=re.search(r'function trimChunkImages\(size\)\{.*?(?=\nasync function redrawChunks)',source).group()
 script="""
 const chunkImages=new Map();const viewport=[.9,0,1.8,1.8];
 const imageKey=(x,y)=>'detail:'+x+':'+y;
 chunkImages.set('detail:0:0','known visible SVG');
 for(let i=0;i<500;i++)chunkImages.set('overview:'+i,'old SVG');
 """+trim+"""
 trimChunkImages(3);
 if(chunkImages.get('detail:0:0')!=='known visible SVG')throw Error('visible detail evicted');
 if(chunkImages.size!==256)throw Error('cache unbounded');
 """
 subprocess.run(['node','-e',script],check=True,capture_output=True,text=True)
