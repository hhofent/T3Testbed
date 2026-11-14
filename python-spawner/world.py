"""Coordinate-owned deterministic endless world. Units: integer reference ticks.
World y increases southward. Queries return full canonical features, never clipped
geometry; SVG clips at the viewport. Settings and seed define the entire world.
"""
import math,json,copy
from functools import lru_cache
from settings import Settings,applied,option,DEFAULTS
from presets import document,apply_document
from spawner import H,FT
from region import make_region,spawn_region,road_rect,world_rect,to_world,MILE,ROW,RAIL_ROW,RAIL_GAP,_seg_rect

def box(points,pad=0):
 xs,ys=zip(*points); return (min(xs)-pad,min(ys)-pad,max(xs)-min(xs)+2*pad,max(ys)-min(ys)+2*pad)
def intersects(a,b): return a[0]<=b[0]+b[2] and b[0]<=a[0]+a[2] and a[1]<=b[1]+b[3] and b[1]<=a[1]+a[3]
def feature(kind,id,**data):
 result=dict(kind=kind,id=id,**data)
 result['bounds']=data.get('rect') or box(data['points'],data.get('width',0)/2)
 return result

def road_type(seed,axis,line,segment):
 main=line%option('world.main_spacing_miles')==1
 if main: return 'paved'
 if axis=='h' and H(seed,'missing',line,segment)%10000 < option('world.missing_pct')*200: return None
 return 'dirt' if H(seed,'surface',axis,line,segment)%10000 < option('world.level_b_pct')*100 else 'gravel'

def rail_lanes(seed,rect):
 spacing=option('world.rail_spacing_miles')*MILE
 for lane in range(math.floor(rect[0]/spacing)-1,math.ceil((rect[0]+rect[2])/spacing)+1):
  if H(seed,'rail-history',lane)%100>=option('world.rail_enabled_pct'): continue
  along=lane*spacing+MILE
  x=along+option('region.row')//2+option('region.rail_gap')+option('region.rail_row')//2
  yield lane,x,along

@lru_cache(maxsize=DEFAULTS['world.tile_cache_size'])
def _tile(seed,tx,ty,serialized):
 settings=apply_document(Settings(),json.loads(serialized))
 settings.set('region.n_mi',settings.values['world.tile_miles'])
 with applied(settings):
  extent=option('world.tile_miles')*MILE; ox,oy=tx*extent,ty*extent
  def roads(local_seed,force):
   output=[]
   for axis in ('h','v'):
    for i in range(option('world.tile_miles')+1):
     global_line=(ty if axis=='h' else tx)*option('world.tile_miles')+i
     surface='paved' if global_line%option('world.main_spacing_miles')==1 else 'gravel'
     output.append(dict(id=f'{axis}{i}',name=f'{axis.upper()} {global_line}',axis=axis,type=surface,c=i*MILE,lo=0,hi=extent,w=option('region.row'),surface_ft=22))
   return output,25
  def rail(local_seed,rs,force):
   if not option('stages.rail'): return None
   ew=next((r for r in rs if r['axis']=='h' and r['type']=='paved'),None)
   if ew is None: return None
   for lane,x,along in rail_lanes(seed,(ox,oy,extent,extent)):
    local=along-ox
    if not MILE//2<=local<=extent-MILE//2: continue
    x-=ox
    return dict(pts=[(x,0),(x,extent)],w=option('region.rail_row'),along=f'v{local//MILE}',along_c=local,x=x,side=1,parallel=(0,extent),crossings=[],site=(local,ew['c']))
   return None
  water=list(rivers(seed,(ox,oy,extent,extent)))
  def water_hits(local):
   absolute=(local[0]+ox,local[1]+oy,local[2],local[3])
   return any(_seg_rect(a,b,(absolute[0]-stream['width']/2,absolute[1]-stream['width']/2,absolute[2]+stream['width'],absolute[3]+stream['width'])) for stream in water for a,b in zip(stream['points'],stream['points'][1:]))
  tile_seed=H(seed,'settlement',tx,ty)
  reg=make_region(tile_seed,road_factory=roads,rail_factory=rail)
  # Keep ownership disjoint: reject towns whose envelope would invade another tile.
  if reg['town']:
   b=reg['town']['box']
   if b[0]<0 or b[1]<0 or b[0]+b[2]>extent or b[1]+b[3]>extent or water_hits(b):
    off=Settings(settings.values).set('stages.towns',False)
    reg=make_region(tile_seed,road_factory=roads,rail_factory=rail,settings=off)
  results=spawn_region(reg) if option('stages.houses') else {}
  out=[]; prefix=f'{tx}:{ty}:'
  def rect(r): return (r[0]+ox,r[1]+oy,r[2],r[3])
  def add(kind,id,r,**kw): out.append(feature(kind,prefix+id,rect=rect(r),**kw))
  if reg['town']: add('towns','town',reg['town']['box'],size=reg['town']['size'])
  for r in reg['roads']:
   if r in reg['grid_roads']: continue
   add('roads','street:'+r['id'],road_rect(r),surface='paved',axis=r['axis'],history=['town plat','paved'])
  for creek in reg['creeks']:
   pts=[(x+ox,y+oy) for x,y in creek['pts']]
   out.append(feature('creeks',prefix+creek['id'],points=pts,width=creek['chan_w']))
  for lot in reg['lots']:
   spec=lot['spec']; id=spec['id']; pl=lot['place']
   if water_hits(world_rect(pl,spec['width'],spec['depth'])): continue
   # A rural driveway must terminate on an extant historical section.
   if spec['kind']!='town':
    r=next(r for r in reg['roads'] if r['id']==lot['road_id']); along=int(lot['along']//MILE)
    line=(ty if r['axis']=='h' else tx)*option('world.tile_miles')+r['c']//MILE
    segment=(tx if r['axis']=='h' else ty)*option('world.tile_miles')+along
    if road_type(seed,r['axis'],line,segment) is None: continue
   add('lots',id,world_rect(pl,spec['width'],spec['depth']),use=spec['kind'])
   plan=results.get(id,{}).get('plan')
   if not plan: continue
   def transform(r):
    a=to_world(pl,spec['width'],spec['depth'],r[0],r[1]); b=to_world(pl,spec['width'],spec['depth'],r[0]+r[2],r[1]+r[3])
    return (min(a[0],b[0]),min(a[1],b[1]),abs(a[0]-b[0]),abs(a[1]-b[1]))
   for i,room in enumerate(plan.rooms): add('buildings',id+':room:'+str(i),transform(room.rect()),use=room.kind)
   for i,(_,r) in enumerate(plan.site.get('farm',[])): add('buildings',id+':farm:'+str(i),transform(r),use='farm')
   for i,r in enumerate(plan.site.get('drive',[])): add('driveways',id+':drive:'+str(i),transform(r))
   if option('stages.trees'):
    from trees import local_markers
    for i,(x,y) in enumerate(local_markers(plan)):
     x,y=to_world(pl,spec['width'],spec['depth'],x,y)
     add('trees',id+':tree:'+str(i),(x,y,0,0))
  if reg['town']:
   for shop in reg['town']['commercial']:
    if shop.get('bldg') and not water_hits(shop['lot']):
     r=shop['bldg']
     if r[0]>=0 and r[1]>=0 and r[0]+r[2]<=extent and r[1]+r[3]<=extent: add('buildings','business:'+shop['id'],r,use=shop['type'])
  return out

def elevation(seed,x,y):
 wave=option('world.hill_wavelength_miles')*MILE
 phase=(H(seed,'hills')%10000)/10000*2*math.pi
 return option('world.relief_ft')*(2+math.sin(x/wave*2*math.pi+phase)+math.cos(y/wave*2*math.pi-phase))/4

def terrain(seed,rect):
 cell=FT(option('world.contour_cell_ft')); interval=option('world.contour_interval_ft')
 for ix in range(math.floor(rect[0]/cell)-1,math.ceil((rect[0]+rect[2])/cell)+1):
  for iy in range(math.floor(rect[1]/cell)-1,math.ceil((rect[1]+rect[3])/cell)+1):
   corners=[(ix*cell,iy*cell),((ix+1)*cell,iy*cell),((ix+1)*cell,(iy+1)*cell),(ix*cell,(iy+1)*cell)]
   heights=[elevation(seed,*p) for p in corners]
   for level in range(interval,int(option('world.relief_ft'))+interval,interval):
    points=[]
    for a,b,ha,hb in zip(corners,corners[1:]+corners[:1],heights,heights[1:]+heights[:1]):
     if (ha<=level<hb) or (hb<=level<ha):
      t=(level-ha)/(hb-ha); points.append((a[0]+t*(b[0]-a[0]),a[1]+t*(b[1]-a[1])))
    for i in range(0,len(points)-1,2): yield feature('contours',f'contour:{ix}:{iy}:{level}:{i}',points=points[i:i+2],height=level)

def rivers(seed,rect):
 spacing=option('world.river_spacing_miles')*MILE; step=option('world.tributary_spacing_miles')*MILE; width=FT(option('world.river_width_ft'))
 for lane in range(math.floor(rect[0]/spacing)-1,math.ceil((rect[0]+rect[2])/spacing)+1):
  phase=H(seed,'river',lane)%100/100*2*math.pi
  def x(y): return lane*spacing+math.sin(y/(step*option('world.river_wave_steps'))+phase)*MILE*option('world.river_wander_miles')
  for row in range(math.floor(rect[1]/step)-1,math.ceil((rect[1]+rect[3])/step)+1):
   y=row*step; end=(x(y+step),y+step)
   yield feature('rivers',f'river:{lane}:{row}',points=[(x(y),y),end],width=width,flow='south',downstream=f'river:{lane}:{row+1}')
   for side in (-1,1):
    yield feature('rivers',f'tributary:{lane}:{row}:{side}',points=[(lane*spacing+side*spacing/2,y),end],width=width/2,flow='south',downstream=f'river:{lane}:{row+1}')

def window(seed,rect,settings=None):
 settings=settings or Settings()
 if len(rect)!=4 or any(not math.isfinite(v) for v in rect) or rect[2]<=0 or rect[3]<=0: raise ValueError('Window requires finite x,y and positive width,height')
 with applied(settings):
  out=list(terrain(seed,rect))+list(rivers(seed,rect))
  # Historical section roads are global edges, independent of query or settlement tiles.
  for axis in ('h','v'):
   c0,c1=(rect[1],rect[1]+rect[3]) if axis=='h' else (rect[0],rect[0]+rect[2])
   s0,s1=(rect[0],rect[0]+rect[2]) if axis=='h' else (rect[1],rect[1]+rect[3])
   for line in range(math.floor(c0/MILE)-1,math.ceil(c1/MILE)+1):
    for segment in range(math.floor(s0/MILE)-1,math.ceil(s1/MILE)+1):
     surface=road_type(seed,axis,line,segment)
     if surface is None: continue
     c,lo=line*MILE,segment*MILE
     r=(lo,c-option('region.row')/2,MILE,option('region.row')) if axis=='h' else (c-option('region.row')/2,lo,option('region.row'),MILE)
     out.append(feature('roads',f'road:{axis}:{line}:{segment}',rect=r,surface=surface,history=['wagon trail','upgraded','paved'] if surface=='paved' else ['survey grid','dirt',surface],axis=axis))
  if option('stages.rail'):
   for lane,x,_ in rail_lanes(seed,rect):
    for row in range(math.floor(rect[1]/MILE)-1,math.ceil((rect[1]+rect[3])/MILE)+1):
     out.append(feature('rail',f'rail:{lane}:{row}',points=[(x,row*MILE),(x,(row+1)*MILE)],width=option('region.rail_row'),history=['road corridor','rail']))
  extent=option('world.tile_miles')*MILE; halo=option('world.halo_tiles'); serialized=json.dumps(document(settings),sort_keys=True)
  for tx in range(math.floor(rect[0]/extent)-halo,math.floor((rect[0]+rect[2])/extent)+halo+1):
   for ty in range(math.floor(rect[1]/extent)-halo,math.floor((rect[1]+rect[3])/extent)+halo+1): out.extend(_tile(seed,tx,ty,serialized))
  filtered={f['id']:f for f in out if intersects(f['bounds'],rect)}
  # A marker is rejected against geometry from every owner, including shared borders.
  blockers=[f for f in out if f['kind'] in ('roads','rail','buildings','driveways')]
  cell=FT(option('verify.bin_ft')); bins={}
  def keys(bounds):
   for x in range(math.floor(bounds[0]/cell),math.floor((bounds[0]+bounds[2])/cell)+1):
    for y in range(math.floor(bounds[1]/cell),math.floor((bounds[1]+bounds[3])/cell)+1): yield x,y
  for blocker in blockers:
   for key in keys(blocker['bounds']): bins.setdefault(key,[]).append(blocker)
  for id,f in list(filtered.items()):
   if f['kind']=='trees':
    radius=FT(option('world.tree_radius_ft')); r=(f['bounds'][0]-radius,f['bounds'][1]-radius,2*radius,2*radius)
    candidates={b['id']:b for key in keys(r) for b in bins.get(key,[])}
    if any(intersects(r,b['bounds']) for b in candidates.values()): del filtered[id]
  return {'seed':seed,'window':rect,'features':copy.deepcopy(sorted(filtered.values(),key=lambda f:(f['kind'],f['id'])))}

COLORS={'contours':'#b5ba9b','rivers':'#509bc6','creeks':'#67a9cc','rail':'#685553','roads':'#b9a17a','buildings':'#d6dfc4','driveways':'#c8bea9','trees':'#3c6946','towns':'none','lots':'none'}
def svg(scene):
 from html import escape
 x,y,w,h=scene['window']
 out=[f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{x} {y} {w} {h}"><rect x="{x}" y="{y}" width="{w}" height="{h}" fill="#f2f1e6"/>']
 order=['contours','rivers','creeks','towns','lots','roads','rail','driveways','buildings','trees']
 for kind in order:
  for f in scene['features']:
   if f['kind']!=kind: continue
   color=COLORS[kind]
   if kind=='roads': color={'paved':'#525954','gravel':'#b9a17a','dirt':'#9a784c'}[f['surface']]
   title=escape(f['id']+' '+str(f.get('use','')))
   out.append('<g><title>'+title+'</title>')
   if kind=='trees':
    a,b,_,_=f['rect']; out.append(f'<circle cx="{a}" cy="{b}" r="{FT(option("world.tree_radius_ft"))}" fill="{color}"/>')
   elif 'rect' in f:
    a,b,c,d=f['rect']; stroke='#b5c0ab' if kind in ('towns','lots') else '#687060'
    out.append(f'<rect x="{a}" y="{b}" width="{c}" height="{d}" fill="{color}" stroke="{stroke}" stroke-width="0.3" vector-effect="non-scaling-stroke"/>')
   else:
    points=' '.join(f'{a},{b}' for a,b in f['points'])
    width=f.get('width',FT(1)) if kind in ('rivers','creeks') else FT(option('world.rail_draw_ft')) if kind=='rail' else FT(1)
    out.append(f'<polyline points="{points}" fill="none" stroke="{color}" stroke-width="{width}"/>')
   out.append('</g>')
 out.append('</svg>'); return ''.join(out).encode()
