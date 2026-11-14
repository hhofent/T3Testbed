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
 if line%option('world.main_spacing_miles')==1: return 'paved'
 # Each removable edge belongs to one disjoint unit-square cycle. At most one
 # of its four edges is removed, so its other three edges are a guaranteed detour.
 # Both orientations participate equally; no query-order-dependent repair.
 if segment%2==0:
  bx,by=(segment//2,line//2) if axis=='h' else (line//2,segment//2)
  candidate=(line%2) if axis=='h' else 2+(line%2)
  if H(seed,'missing-cycle',bx,by)%10000<option('world.missing_pct')*800 and H(seed,'missing-edge',bx,by)%4==candidate: return None
 main=line%option('world.main_spacing_miles')==1
 if main: return 'paved'
 return 'dirt' if H(seed,'surface',axis,line,segment)%10000 < option('world.level_b_pct')*100 else 'gravel'

def rail_lanes(seed,rect):
 spacing=option('world.rail_spacing_miles')*MILE
 for lane in range(math.floor(rect[0]/spacing)-1,math.ceil((rect[0]+rect[2])/spacing)+1):
  if H(seed,'rail-history',lane)%100>=option('world.rail_enabled_pct'): continue
  along=lane*spacing+MILE
  x=along+option('region.row')//2+option('region.rail_gap')+option('region.rail_row')//2
  yield lane,x,along

def rail_points(seed,lane,x,row):
 # The corridor has one fixed offset: every section and chunk shares this line.
 return [(x,row*MILE),(x,(row+1)*MILE)]

@lru_cache(maxsize=DEFAULTS['world.tile_cache_size'])
def _tile(seed,tx,ty,serialized,detail=True):
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
  buffer=FT(max(option('region.creek_buf')))
  water=[f for f in rivers(seed,(ox-buffer,oy-buffer,extent+2*buffer,extent+2*buffer)) if intersects(f['bounds'],(ox-buffer,oy-buffer,extent+2*buffer,extent+2*buffer))]
  def water_hits(local):
   absolute=(local[0]+ox-buffer,local[1]+oy-buffer,local[2]+2*buffer,local[3]+2*buffer)
   return any(_seg_rect(a,b,(absolute[0]-stream['width']/2,absolute[1]-stream['width']/2,absolute[2]+stream['width'],absolute[3]+stream['width'])) for stream in water if intersects(stream['bounds'],absolute) for a,b in zip(stream['points'],stream['points'][1:]))
  tile_seed=H(seed,'settlement',tx,ty)
  reg=make_region(tile_seed,road_factory=roads,rail_factory=rail)
  # Keep ownership disjoint: reject towns whose envelope would invade another tile.
  if reg['town']:
   b=reg['town']['box']
   if b[0]<0 or b[1]<0 or b[0]+b[2]>extent or b[1]+b[3]>extent or water_hits(b):
    off=Settings(settings.values).set('stages.towns',False)
    reg=make_region(tile_seed,road_factory=roads,rail_factory=rail,settings=off)
  results=spawn_region(reg) if detail and option('stages.houses') else {}
  out=[]; prefix=f'{tx}:{ty}:'
  def rect(r): return (r[0]+ox,r[1]+oy,r[2],r[3])
  def add(kind,id,r,**kw): out.append(feature(kind,prefix+id,rect=rect(r),**kw))
  if reg['town']: add('towns','town',reg['town']['box'],size=reg['town']['size'])
  for r in reg['roads']:
   if r in reg['grid_roads']: continue
   add('roads','street:'+r['id'],road_rect(r),surface='paved',axis=r['axis'],history=['town plat','paved'])
  for lot in reg['lots']:
   spec=lot['spec']; id=spec['id']; pl=lot['place']
   if water_hits(world_rect(pl,spec['width'],spec['depth'])): continue
   # A rural driveway must terminate on an extant historical section.
   if spec['kind']!='town':
    r=next(r for r in reg['roads'] if r['id']==lot['road_id']); along=int(lot['along']//MILE)
    line=(ty if r['axis']=='h' else tx)*option('world.tile_miles')+r['c']//MILE
    segment=(tx if r['axis']=='h' else ty)*option('world.tile_miles')+along
    if road_type(seed,r['axis'],line,segment) is None: continue
   if detail: add('lots',id,world_rect(pl,spec['width'],spec['depth']),use=spec['kind'])
   elif spec['kind']!='town': add('farms',id,world_rect(pl,spec['width'],spec['depth']),use=spec['kind'])
   plan=results.get(id,{}).get('plan')
   if not plan: continue
   def transform(r):
    a=to_world(pl,spec['width'],spec['depth'],r[0],r[1]); b=to_world(pl,spec['width'],spec['depth'],r[0]+r[2],r[1]+r[3])
    return (min(a[0],b[0]),min(a[1],b[1]),abs(a[0]-b[0]),abs(a[1]-b[1]))
   rural=None
   if spec['kind']!='town':
    from rural import layout
    rural=layout(seed,spec,plan)
    for f in out:
     if f['id']==prefix+id and f['kind']=='lots': f['shape']=[to_world(pl,spec['width'],spec['depth'],a,b) for a,b in rural['shape']]; f['shape']=[(a+ox,b+oy) for a,b in f['shape']]
   for i,room in enumerate(plan.rooms):
    r=room.rect()
    if rural: r=(r[0]+rural['offset'][0],r[1]+rural['offset'][1],r[2],r[3])
    add('buildings',id+':room:'+str(i),transform(r),use=room.kind)
   farms=rural['buildings'] if rural else plan.site.get('farm',[])
   for i,(use,r) in enumerate(farms): add('buildings',id+':farm:'+str(i),transform(r),use=use)
   for i,r in enumerate(rural['driveways'] if rural else plan.site.get('drive',[])): add('driveways',id+':drive:'+str(i),transform(r))
   if option('stages.trees'):
    from trees import local_markers
    for i,(x,y) in enumerate(rural['trees'] if rural else local_markers(plan)):
     x,y=to_world(pl,spec['width'],spec['depth'],x,y)
     add('trees',id+':tree:'+str(i),(x,y,0,0))
  if detail and reg['town']:
   for shop in reg['town']['commercial']:
    if shop.get('bldg') and not water_hits(shop['lot']):
     r=shop['bldg']
     if r[0]>=0 and r[1]>=0 and r[0]+r[2]<=extent and r[1]+r[3]<=extent: add('buildings','business:'+shop['id'],r,use=shop['type'])
  return out

RIVER_SAMPLES=64

def drainage_phase(seed):
 return H(seed,'river-watershed')%10000/10000*2*math.pi

def drainage_omega():
 return 2*math.pi/(option('world.tributary_spacing_miles')*MILE*option('world.river_wave_steps')/4)

def drainage_y(seed,s):
 # Compress the neck of each hairpin without closing or crossing the channel.
 # dy/ds >= .1: downstream order is strict even at the tightest bend.
 omega=drainage_omega()
 return s+.45/omega*math.sin(2*(s*omega+drainage_phase(seed)))

def drainage_station(seed,y):
 omega=drainage_omega(); lo=y-.45/omega; hi=y+.45/omega
 for _ in range(40):
  mid=(lo+hi)/2
  if drainage_y(seed,mid)<y: lo=mid
  else: hi=mid
 return (lo+hi)/2

def drainage_x(seed,y,lane=0):
 return drainage_offset(seed,drainage_station(seed,y),lane)

def drainage_offset(seed,station,lane):
 phase=station*drainage_omega()+drainage_phase(H(seed,'channel',lane))
 # Incommensurate long waves change bend phase and amplitude without tile resets.
 phase+=.7*math.sin(station/MILE*.173+lane*1.91)
 amplitude=.8+.2*math.sin(station/MILE*.311+lane*2.37)
 return math.sin(phase)*amplitude*MILE*option('world.river_wander_miles')

def elevation(seed,x,y):
 # A south-sloping datum plus watershed ridges. The same valley centers own rivers.
 spacing=option('world.river_spacing_miles')*MILE
 lane=round(x/spacing)
 distance=min(abs(x-k*spacing-drainage_x(seed,y,k)) for k in (lane-1,lane,lane+1))
 return -y/MILE*option('world.south_drop_ft')+option('world.relief_ft')*(1-math.cos(distance/spacing*2*math.pi))/2

def terrain(seed,rect):
 cell=FT(option('world.contour_cell_ft')); interval=option('world.contour_interval_ft')
 for ix in range(math.floor(rect[0]/cell)-1,math.ceil((rect[0]+rect[2])/cell)+1):
  for iy in range(math.floor(rect[1]/cell)-1,math.ceil((rect[1]+rect[3])/cell)+1):
   corners=[(ix*cell,iy*cell),((ix+1)*cell,iy*cell),((ix+1)*cell,(iy+1)*cell),(ix*cell,(iy+1)*cell)]
   heights=[elevation(seed,*p) for p in corners]
   for level in range(math.floor(min(heights)/interval)*interval,math.ceil(max(heights)/interval)*interval+interval,interval):
    points=[]
    for a,b,ha,hb in zip(corners,corners[1:]+corners[:1],heights,heights[1:]+heights[:1]):
     if (ha<=level<hb) or (hb<=level<ha):
      t=(level-ha)/(hb-ha); points.append((a[0]+t*(b[0]-a[0]),a[1]+t*(b[1]-a[1])))
    for i in range(0,len(points)-1,2): yield feature('contours',f'contour:{ix}:{iy}:{level}:{i}',points=points[i:i+2],height=level)

def rivers(seed,rect):
 spacing=option('world.river_spacing_miles')*MILE; step=option('world.tributary_spacing_miles')*MILE; width=FT(option('world.river_width_ft')); sample=step/RIVER_SAMPLES
 wander=option('world.river_wander_miles')*MILE
 lo=drainage_station(seed,rect[1]); hi=drainage_station(seed,rect[1]+rect[3])
 for lane in range(math.floor((rect[0]-wander-spacing/2)/spacing),math.ceil((rect[0]+rect[2]+wander+spacing/2)/spacing)+1):
  def point(station,offset=0):
   return (lane*spacing+drainage_offset(seed,station,lane)+offset,drainage_y(seed,station))
  for row in range(math.floor(lo/sample)-1,math.ceil(hi/sample)+1):
   yield feature('rivers',f'river:{lane}:{row}',points=[point(row*sample),point((row+1)*sample)],width=width,flow='south',source=False,downstream=f'river:{lane}:{row+1}')
  for row in range(math.floor(lo/step)-1,math.ceil(hi/step)+1):
   for side in (-1,1):
    # Most of a tributary interval: several miles and many display chunks.
    reach=(.85+H(seed,'source-reach',lane,row,side)%10/100)*step
    join_index=row*RIVER_SAMPLES+(RIVER_SAMPLES if side==1 else RIVER_SAMPLES*3//4)
    join=join_index*sample; count=round(reach/sample); start=(join_index-count)*sample; reach=count*sample
    offset=side*spacing*(.22+H(seed,'source-ridge',lane,row,side)%22/100)
    # Own the branch's bends independently of the receiving channel. Keep the
    # excursion inside its bank of the basin so it cannot cross the trunk.
    branch_seed=H(seed,'basin',lane,row,'branch',side)
    phase=branch_seed%10000/10000*2*math.pi
    cycles=2+(branch_seed//10000)%3
    points=[]
    for i in range(count+1):
     t=i/count
     d=offset*(1-t)**2*(1+.025*math.sin(t*cycles*2*math.pi+phase)*math.sin(math.pi*t))
     points.append(point((join_index-count+i)*sample,d))
    points[-1]=point(join)
    # Widths are sampled at vertices. The scalar remains the conservative
    # broad-phase collision width used by lots and settlement exclusion.
    widths=[width*(.06+.44*(i/count)**.7) for i in range(count+1)]
    yield feature('rivers',f'tributary:{lane}:{row}:{side}',points=points,width=width/2,widths=widths,stream_order=1,flow='south',source=True,downstream=f'river:{lane}:{join_index}')

def window(seed,rect,settings=None,detail=True):
 settings=settings or Settings()
 if len(rect)!=4 or any(not math.isfinite(v) for v in rect) or rect[2]<=0 or rect[3]<=0: raise ValueError('Window requires finite x,y and positive width,height')
 with applied(settings):
  out=(list(terrain(seed,rect)) if detail else [])+list(rivers(seed,rect))
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
     out.append(feature('rail',f'rail:{lane}:{row}',points=rail_points(seed,lane,x,row),width=option('region.rail_row'),history=['road corridor','rail']))
  extent=option('world.tile_miles')*MILE; halo=option('world.halo_tiles') if detail else 0; serialized=json.dumps(document(settings),sort_keys=True)
  for tx in range(math.floor(rect[0]/extent)-halo,math.floor((rect[0]+rect[2])/extent)+halo+1):
   for ty in range(math.floor(rect[1]/extent)-halo,math.floor((rect[1]+rect[3])/extent)+halo+1): out.extend(_tile(seed,tx,ty,serialized,detail))
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

COLORS={'contours':'#b5ba9b','rivers':'#509bc6','creeks':'#67a9cc','rail':'#685553','roads':'#b9a17a','buildings':'#d6dfc4','driveways':'#c8bea9','trees':'#3c6946','towns':'none','lots':'none','farms':'#8b7655'}
def svg(scene,local=False):
 if local:
  # Chromium/Skia clips large absolute coordinates even with a distant viewBox.
  # Rebase geometry itself, rather than applying a large cancelling transform.
  x,y,w,h=scene["window"]; scene=copy.deepcopy(scene); scene["window"]=[0,0,w,h]
  for f in scene["features"]:
   if "rect" in f:
    a,b,c,d=f["rect"]; f["rect"]=[a-x,b-y,c,d]
   for key in ("points","shape"):
    if key in f: f[key]=[(a-x,b-y) for a,b in f[key]]
 from html import escape
 x,y,w,h=scene['window']
 out=[f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{x} {y} {w} {h}"><rect x="{x}" y="{y}" width="{w}" height="{h}" fill="#f2f1e6"/>']
 order=['contours','rivers','creeks','towns','lots','roads','rail','driveways','buildings','trees','farms']
 for kind in order:
  for f in scene['features']:
   if f['kind']!=kind: continue
   color=COLORS[kind]
   if kind=='roads': color={'paved':'#525954','gravel':'#b9a17a','dirt':'#9a784c'}[f['surface']]
   title=escape(f['id']+' '+str(f.get('use','')))
   out.append('<g><title>'+title+'</title>')
   if kind=='farms':
    a,b,c,d=f['rect']; out.append(f'<circle cx="{a+c/2}" cy="{b+d/2}" r="{FT(45)}" fill="{color}"/>')
   elif kind=='trees':
    a,b,_,_=f['rect']; out.append(f'<circle cx="{a}" cy="{b}" r="{FT(option("world.tree_radius_ft"))}" fill="{color}"/>')
   elif 'shape' in f:
    points=' '.join(f'{a},{b}' for a,b in f['shape']); out.append(f'<polygon points="{points}" fill="none" stroke="#b5c0ab" stroke-width="0.3" vector-effect="non-scaling-stroke"/>')
   elif 'rect' in f:
    a,b,c,d=f['rect']; stroke='#b5c0ab' if kind in ('towns','lots') else '#687060'
    out.append(f'<rect x="{a}" y="{b}" width="{c}" height="{d}" fill="{color}" stroke="{stroke}" stroke-width="{0.9 if kind=="roads" else 0.3}" vector-effect="non-scaling-stroke"/>')
   else:
    points=' '.join(f'{a},{b}' for a,b in f['points'])
    width=f.get('width',FT(1)) if kind in ('rivers','creeks') else FT(option('world.rail_draw_ft')) if kind=='rail' else FT(1)
    if 'widths' in f:
     for a,b,wa,wb in zip(f['points'],f['points'][1:],f['widths'],f['widths'][1:]):
      out.append(f'<polyline points="{a[0]},{a[1]} {b[0]},{b[1]}" fill="none" stroke="{color}" stroke-width="{(wa+wb)/2}" stroke-linecap="round"/>')
    else:
     out.append(f'<polyline points="{points}" fill="none" stroke="{color}" stroke-width="{width}"/>')
   out.append('</g>')
 out.append('</svg>'); return ''.join(out).encode()
