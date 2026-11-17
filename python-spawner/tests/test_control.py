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


def test_world_png_and_browser_share_styles_for_the_same_rectangle():
 import io,xml.etree.ElementTree as ET
 from PIL import Image
 from world import WORLD_STYLE,feature,svg,feature_style,MILE,FT
 from map_render import world_png
 from control import schema
 from settings import applied
 assert schema()['world_style']==WORLD_STYLE
 with applied(Settings()):
  fs=[feature('rail','rail:test',points=[(MILE/4,0),(MILE/4,MILE)],width=FT(100)),
      feature('rivers','river:test',points=[(MILE*3/4,0),(MILE*3/4,MILE)],width=FT(1),stream_order=3)]
  scene=dict(window=(0,0,MILE,MILE),features=fs)
  root=ET.fromstring(svg(scene)); groups=root.findall('{*}g')
  raster=Image.open(io.BytesIO(world_png(scene,512))).convert('RGB')
  bg=tuple(bytes.fromhex(WORLD_STYLE['background'][1:]))
  for f in fs:
   g=next(g for g in groups if g.find('{*}title').text.strip()==f['id'])
   style=feature_style(f); color=tuple(bytes.fromhex(style['color'][1:]))
   assert g.find('{*}polyline').attrib['stroke']==style['color']
   assert float(g.attrib['data-minimum-px'])==style['minimum_px']
   column=128 if f['kind']=='rail' else 384
   channel=max(range(3),key=lambda i:abs(bg[i]-color[i]))
   coverage=sum((bg[channel]-raster.getpixel((x,256))[channel])/(bg[channel]-color[channel]) for x in range(column-5,column+6))
   assert abs(coverage-style['minimum_px'])<.15,(f['kind'],coverage)


def test_rail_has_distinct_narrow_dashed_rendering():
 from world import WORLD_STYLE,feature,svg,MILE,FT
 rail=feature('rail','rail:sample',points=[(0,0),(MILE,MILE)],width=FT(100))
 markup=svg({'window':(0,0,MILE,MILE),'features':[rail]}).decode()
 assert WORLD_STYLE['minimum_px']['rail'] < WORLD_STYLE['minimum_px']['paved']
 assert '#685553' in markup and 'stroke-dasharray=' in markup

def test_bridge_deck_is_distinct_visible_and_below_rail():
 from world import WORLD_STYLE,feature,feature_style,MILE,FT
 bridge=feature('bridges','bridge:sample',points=[(MILE/2,MILE*.45),(MILE/2,MILE*.55)],width=FT(22),surface='gravel',carrier='roads')
 style=feature_style(bridge)
 assert style['color']==WORLD_STYLE['colors']['bridges']
 assert style['color'] not in WORLD_STYLE['roads'].values()
 assert style['minimum_px']>=1.2
 order=WORLD_STYLE['order']
 assert order.index('roads')<order.index('bridges')<order.index('rail')

def test_control_page_quiet_defaults_and_loaded_town_navigation():
 from control import page_bytes
 page=page_bytes().decode()
 assert page.count('class="version"')==1
 assert "group.open=title==='Endless world and navigation'" in page
 assert 'detail.open=false' in page
 assert 'function collectTowns()' in page and 'towns.keys()' in page
 assert "className='town-marker'" in page and "viewport[2]>=6*mile" in page
 assert 'for(let width=12' not in page
 assert 'No accepted town in loaded chunks' in page
 assert 'flex-wrap:nowrap' in page

def test_town_distance_status_survives_async_chunk_redraw():
 from control import page_bytes
 page=page_bytes().decode()
 assert '<div id="townDistance" role="status" aria-live="polite"></div>' in page
 assert 'function updateTownDistance()' in page
 assert "$('townDistance').textContent=`Town ${townIndex+1} of ${townList.length}; next town ${distance.toFixed(1)} mi away`" in page
 assert "$('status').textContent=`Town ${townIndex+1}" not in page


def test_overview_town_labels_are_readable_and_stay_inside_map():
 import re,subprocess
 from control import page_bytes
 page=page_bytes().decode()
 assert "group.getAttribute('data-town-name')" in page
 assert 'marker.textContent=label' in page
 assert "marker.textContent='? '" not in page
 helper=re.search(r'^function townMarkerPosition[^\n]+',page,re.M).group()
 script=helper+"""
 const p=townMarkerPosition(0,0,160,28,500,400);
 if(p.left<8||p.top<8||p.left+160>492||p.top+28>392)throw Error(JSON.stringify(p));
 const q=townMarkerPosition(500,400,160,28,500,400);
 if(q.left<8||q.top<8||q.left+160>492||q.top+28>392)throw Error(JSON.stringify(q));
 """
 subprocess.run(['node','-e',script],check=True,capture_output=True,text=True)


def test_town_selection_survives_detail_chunk_replacing_rectangle():
 import re,subprocess
 from control import page_bytes
 page=page_bytes().decode()
 collect=re.search(r'function collectTowns\(\)\{[^\n]+\}',page).group()
 script="""
 const towns=new Map([['town:site',[0,0,100,100,'Mason Falls']]]);
 let townList=['town:site'],townIndex=0;
 const controls={prevTown:{},nextTown:{}};const $=id=>controls[id];const updateTownDistance=()=>{};
 """+collect+"""
 towns.set('town:site',[5,5,100,100,'Mason Falls']);
 collectTowns();
 if(townIndex!==0||townList.length!==1)throw Error(JSON.stringify({townIndex,townList}));
 """
 subprocess.run(['node','-e',script],check=True,capture_output=True,text=True)


def test_control_reports_basin_chunk_progress_without_blank_status():
 from control import page_bytes
 page=page_bytes().decode()
 assert 'Temporary grid;' in page and 'basin for chunk (${data.active.x}, ${data.active.y})' in page
 assert '${data.completed||0} of ${data.total||data.chunks.length} visible chunks ready' in page
 assert "host.style.background='#deded2'" in page
