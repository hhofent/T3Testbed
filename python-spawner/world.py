"""Coordinate-owned deterministic endless world. Units: integer reference ticks.
World y increases southward. Queries return full canonical features, never clipped
geometry; SVG clips at the viewport. Settings and seed define the entire world.
"""
import math,json,copy,itertools
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

def correction_shift(seed,band):
 # Survey correction parallels every 24 miles. Adjacent bands shift to opposite
 # sides of the meridian, avoiding an accidental zero jog at any correction.
 magnitude=round((.04+(H(seed,'correction',band)%4001)/100000)*MILE)
 return magnitude if band%2 else -magnitude

def route_offset(seed,axis,lane,band):
 return _route_offset(seed,axis,lane,band,option('world.main_spacing_miles'),_drainage_options())

@lru_cache(maxsize=8192)
def _route_offset(seed,axis,lane,band,spacing,options):
 # Alternate survey lines: every jog is one or two miles, never a broken edge.
 if band%2==0: return 0
 # Water predates highways. Prefer the approach farther from a drainage junction.
 base=1  # shared corridor shift preserves 9-mile route spacing
 def score(offset):
  x,y=((band*24,base+offset) if axis=='h' else (base+offset,band*24))
  spacing,step=options[0]*MILE,options[1]*MILE
  rx=round((x*MILE+.12*y*MILE)/spacing); ry=math.floor(y*MILE/step)
  joins=[f['points'][-1] for f in basin(seed,rx,ry,options) if f['source']]
  return min(math.dist((x*MILE,y*MILE),p) for p in joins)
 return max((-1,-2),key=lambda offset:(score(offset),H(seed,'paved',axis,lane,band,offset)))

def paved_points(seed,axis,lane,band):
 base=lane*option('world.main_spacing_miles')+1
 a=(base+route_offset(seed,axis,lane,band))*MILE
 b=(base+route_offset(seed,axis,lane,band+1))*MILE
 if axis=='v': a+=correction_shift(seed,band); b+=correction_shift(seed,band+1)
 start=band*24*MILE; end=start+24*MILE; radius=.18*MILE
 direction=1 if b>a else -1
 pts=[(a,start+radius),(a,end-radius)]
 def curve(p,q,r):
  for i in range(1,9):
   t=i/8; pts.append(((1-t)**2*p[0]+2*(1-t)*t*q[0]+t*t*r[0],(1-t)**2*p[1]+2*(1-t)*t*q[1]+t*t*r[1]))
 curve(pts[-1],(a,end),(a+direction*radius,end))
 pts.append((b-direction*radius,end))
 curve(pts[-1],(b,end),(b,end+radius))
 return [(y,x) for x,y in pts] if axis=='h' else pts

def paved_routes(seed,rect):
 spacing=option('world.main_spacing_miles')*MILE
 for axis in ('h','v'):
  c0,c1,s0,s1=(rect[1],rect[1]+rect[3],rect[0],rect[0]+rect[2]) if axis=='h' else (rect[0],rect[0]+rect[2],rect[1],rect[1]+rect[3])
  for lane in range(math.floor(c0/spacing)-1,math.ceil(c1/spacing)+1):
   for band in range(math.floor(s0/(24*MILE))-1,math.ceil(s1/(24*MILE))+1):
    yield feature('roads',f'paved:{axis}:{lane}:{band}',points=paved_points(seed,axis,lane,band),width=option('region.row'),surface='paved',seed=seed,axis=axis,route_id=f'paved:{axis}:{lane}',history=['drainage','survey mile line','continuous highway'])

def paved_edge(seed,axis,line,segment):
 spacing=option('world.main_spacing_miles')
 band=math.floor(segment/24)
 for lane in range(math.floor((line-3)/spacing)-1,math.ceil((line+1)/spacing)+1):
  base=lane*spacing+1
  if line==base+route_offset(seed,axis,lane,band): return True
 # Transverse portions of a route belong to the opposite orientation.
 if line%24==0:
  opposite='v' if axis=='h' else 'h'; band=line//24
  for lane in range(math.floor((segment-3)/spacing)-1,math.ceil((segment+1)/spacing)+1):
   base=lane*spacing+1
   a=base+route_offset(seed,opposite,lane,band-1); b=base+route_offset(seed,opposite,lane,band)
   if min(a,b)<=segment<max(a,b): return True
 return False

def road_type(seed,axis,line,segment):
 if paved_edge(seed,axis,line,segment): return 'paved'
 # Each removable edge belongs to one disjoint cycle. The other three edges
 # guarantee a detour, even if an edge is later upgraded to paved.
 if segment%2==0 and not (axis=='h' and line%24==0):
  bx,by=(segment//2,line//2) if axis=='h' else (line//2,segment//2)
  candidate=(line%2) if axis=='h' else 2+(line%2)
  if H(seed,'missing-cycle',bx,by)%10000<option('world.missing_pct')*800 and H(seed,'missing-edge',bx,by)%4==candidate: return None
 return 'dirt' if H(seed,'surface',axis,line,segment)%10000 < option('world.level_b_pct')*100 else 'gravel'

def rail_lanes(seed,rect,axis='v'):
 spacing=option('world.rail_spacing_miles')*MILE
 c0,c1=(rect[0],rect[0]+rect[2]) if axis=='v' else (rect[1],rect[1]+rect[3])
 for lane in range(math.floor(c0/spacing)-1,math.ceil(c1/spacing)+1):
  history=H(seed,'rail-history',lane) if axis=='v' else H(seed,'rail-history',axis,lane)
  if history%100>=option('world.rail_enabled_pct'): continue
  along=lane*spacing+MILE
  # Keep gentle curves clear of parallel roads and paved correction corners.
  yield lane,along+.25*MILE,along

def rail_points(seed,lane,x,row,axis='v'):
 shift=correction_shift(seed,math.floor(x/(24*MILE))) if axis=='h' else 0
 amplitude=option('world.rail_bend_miles')*.1*MILE
 sign=1 if H(seed,'rail-curve',axis,lane,row)%2 else -1
 pts=[]
 for i in range(17):
  t=i/16; along=(row+t)*MILE+shift
  bend=math.sin(math.pi*(t-.2)/.6)**4 if .2<t<.8 else 0
  lateral=x+sign*amplitude*bend
  pts.append((along,lateral) if axis=='h' else (lateral,along))
 return pts

def town_candidates(seed,rect):
 # Stations follow a line's history rather than a two-dimensional town lattice.
 for axis in ('v','h'):
  lo,hi=(rect[1],rect[1]+rect[3]) if axis=='v' else (rect[0],rect[0]+rect[2])
  for lane,c,along in rail_lanes(seed,rect,axis):
   for k in range(math.floor(lo/(8*MILE))-1,math.ceil(hi/(8*MILE))+1):
    station=(8*k+H(seed,'town-station',axis,lane,k)%3-1)*MILE
    shift=correction_shift(seed,math.floor(station/(24*MILE))) if axis=='v' else 0
    cross=station if axis=='v' else station+correction_shift(seed,math.floor(c/(24*MILE)))
    u=along+shift
    x,y=(u,cross) if axis=='v' else (cross,u)
    yield (f'town:rail:{axis}:{lane}:{k}',axis,x,y,c,True)
 # Some highway towns grew without a railroad. Stable thinning avoids a lattice.
 spacing=option('world.main_spacing_miles')*MILE
 for lane in range(math.floor(rect[0]/spacing)-1,math.ceil((rect[0]+rect[2])/spacing)+1):
  for k in range(math.floor(rect[1]/(8*MILE))-1,math.ceil((rect[1]+rect[3])/(8*MILE))+1):
   if H(seed,'off-rail-town',lane,k)%100>=16: continue
   y=(8*k+H(seed,'off-rail-station',lane,k)%3-1)*MILE
   band=math.floor(y/(24*MILE))
   x=(lane*option('world.main_spacing_miles')+1+route_offset(seed,'v',lane,band))*MILE+correction_shift(seed,band)
   yield (f'town:highway:{lane}:{k}','v',x,y,0,False)

@lru_cache(maxsize=256)
def _town_scene(seed,site,serialized,detail):
 settings=apply_document(Settings(),json.loads(serialized)); settings.set('region.n_mi',3)
 id,axis,x,y,rail_c,on_rail=site
 u,v=(x,y) if axis=='v' else (y,x); ox,oy=u-MILE,v-MILE; extent=3*MILE
 def absolute(a,b): return (a+ox,b+oy) if axis=='v' else (b+oy,a+ox)
 def local(a,b): return (a-ox,b-oy) if axis=='v' else (b-ox,a-oy)
 with applied(settings):
  world_box=(x-MILE,y-MILE,extent,extent)
  barriers=[dict(pts=[local(*p) for p in f['points']],w=f['width']+2*FT(max(option('region.creek_buf')))) for f in rivers(seed,world_box) if intersects(f['bounds'],world_box)]
  def roads(*args):
   return [dict(id=f'{a}{i}',name='Main St' if i==1 else f'{i} St',axis=a,type='paved' if i==1 else 'gravel',c=i*MILE,lo=0,hi=extent,w=option('region.row'),surface_ft=22) for a in ('h','v') for i in range(4)],25
  def rail(*args):
   if not on_rail: return None
   c=round(rail_c-ox)
   return dict(pts=[(c,0),(c,extent)],w=option('region.rail_row'),along='v1',along_c=MILE,x=c,side=1,parallel=(0,extent),crossings=[],site=(MILE,MILE))
  reg=make_region(H(seed,id),force_town=True,road_factory=roads,rail_factory=rail,water_barriers=barriers)
  if not reg['town']: return []
  if on_rail and not any(p['kind']=='elevator' for p in reg['parcels']): return []
  reg['lots']=[lot for lot in reg['lots'] if lot['spec']['kind']=='town']
  results=spawn_region(reg) if detail and option('stages.houses') else {}
  out=[]
  def add(kind,suffix,r,**kw):
   a,b=absolute(r[0],r[1]); c,d=absolute(r[0]+r[2],r[1]+r[3])
   out.append(feature(kind,id+':'+suffix,rect=(min(a,c),min(b,d),abs(c-a),abs(d-b)),**kw))
  town=reg['town']
  highway_axis = axis if not on_rail else ('h' if axis == 'v' else 'v')
  curb_start = []
  outline = []
  if not option('generation.parity'):
   # Find edge of commercial area along highway axis
   comm = [c['bldg'] for c in town.get('commercial', []) if c.get('bldg')]
   if comm:
    if highway_axis == 'v':
     ys_ = [r[1] for r in comm] + [r[1] + r[3] for r in comm]
     curb_start = [(x, min(ys_)), (x, max(ys_))]
    else:
     xs_ = [r[0] for r in comm] + [r[0] + r[2] for r in comm]
     curb_start = [(min(xs_), y), (max(xs_), y)]
   # Generate blobby outline
   from town_outline import generate_town_outline
   # Content is in LOCAL coordinates (relative to ox, oy). Convert to world coords for outline.
   # The outline will be added to the feature which is in world coords.
   ox, oy = (x - MILE, y - MILE) if axis == 'v' else (y - MILE, x - MILE)
   def local_to_world_rect(rect):
       rx, ry, rw, rh = rect
       if axis == 'v':
           return (rx + ox, ry + oy, rw, rh)
       else:
           return (ry + oy, rx + ox, rh, rw)
   
   world_blocks = [local_to_world_rect(b['rect']) for b in town['blocks']]
   world_streets = []
   for s in town['streets']:
       if s.get('rect'):
           world_streets.append(local_to_world_rect(s['rect']))
       elif s.get('points') and s.get('width'):
           # Polyline: convert each point
           pts = [(local_to_world_rect((p[0], p[1], 0, 0))[0], local_to_world_rect((p[0], p[1], 0, 0))[1]) for p in s['points']]
           world_streets.append({'points': pts, 'width': s['width']})
       elif s.get('c') is not None and s.get('lo') is not None and s.get('hi') is not None and s.get('w') is not None:
           # Road definition: convert to rect
           hw = s['w'] // 2
           if s['axis'] == 'h':
               rect = (s['lo'], s['c'] - hw, s['hi'] - s['lo'], 2 * hw)
           else:
               rect = (s['c'] - hw, s['lo'], 2 * hw, s['hi'] - s['lo'])
           world_streets.append(local_to_world_rect(rect))
   world_streets = [s for s in world_streets if s]
   world_alleys = [local_to_world_rect(a) for a in town.get('alleys', [])]
   world_commercial = []
   for c in town.get('commercial', []):
       if c.get('bldg'):
           world_commercial.append({'bldg': local_to_world_rect(c['bldg'])})
       if c.get('lot'):
           world_commercial.append({'lot': local_to_world_rect(c['lot'])})
   
   # Parcels and depot are already in world coords? Check...
   # In _town_scene, parcels come from reg['parcels'] which are in local coords.
   world_parcels = [local_to_world_rect(p['rect']) for p in reg['parcels']]
   world_depot = local_to_world_rect(reg['depot']) if reg['depot'] else None
   
   outline = generate_town_outline(
       H(seed, 'town_outline', id),
       town,
       world_blocks,
       town.get('faces', []),
       world_alleys,
       world_streets,
       world_commercial,
       world_parcels,
       world_depot
   )
  add('towns','town',town['box'],size=town['size'],rail_town=on_rail,site_id=id,anchor=(x,y),route_axis=axis,highway_axis=highway_axis,curb_start=curb_start,outline=outline)
  reserved=[p['rect'] for p in reg['parcels'] if p['kind'] in ('elevator','industry')]+([reg['depot']] if reg['depot'] else [])
  out[-1]['reserved']=[]
  for a,b,c,d in reserved:
   p,q=absolute(a,b),absolute(a+c,b+d)
   out[-1]['reserved'].append((min(p[0],q[0]),min(p[1],q[1]),abs(q[0]-p[0]),abs(q[1]-p[1])))
  for road in reg['roads']:
   if road in reg['grid_roads']: continue
   add('roads','street:'+road['id'],road_rect(road),surface=road.get('surface','paved'),axis=road['axis'] if axis=='v' else ('h' if road['axis']=='v' else 'v'),road_class=road.get('road_class','town_street'),row_ft=road.get('row_ft',road['w']/FT(1)),history=['town plat','paved'])
  if option('generation.parity'):
   bx,by,bw,bh=town['box']
   for a in ('h','v'):
    r=(bx,MILE-option('region.row')/2,bw,option('region.row')) if a=='h' else (MILE-option('region.row')/2,by,option('region.row'),bh)
    add('roads','street:main-'+a,r,surface='paved',axis=a if axis=='v' else ('h' if a=='v' else 'v'),history=['town main street'])
  else:
   for i, a_rect in enumerate(town.get('alleys', [])):
    add('roads', f'alley:{i}', a_rect, road_class='alley', row_ft=20.0, surface='gravel')
  for lot in reg['lots']:
   spec=lot['spec']; pl=lot['place']; lid=spec['id']
   if detail: add('lots',lid,world_rect(pl,spec['width'],spec['depth']),use='town')
   plan=results.get(lid,{}).get('plan')
   if plan:
    for i,room in enumerate(plan.rooms):
     a,b,c,d=room.rect(); p=to_world(pl,spec['width'],spec['depth'],a,b); q=to_world(pl,spec['width'],spec['depth'],a+c,b+d)
     add('buildings',lid+':room:'+str(i),(min(p[0],q[0]),min(p[1],q[1]),abs(q[0]-p[0]),abs(q[1]-p[1])),use=room.kind)
    for i,r in enumerate(plan.site.get('drive',[])):
     a,b,c,d=r; p=to_world(pl,spec['width'],spec['depth'],a,b); q=to_world(pl,spec['width'],spec['depth'],a+c,b+d)
     add('driveways',lid+':drive:'+str(i),(min(p[0],q[0]),min(p[1],q[1]),abs(q[0]-p[0]),abs(q[1]-p[1])))
    if option('stages.trees'):
     if not option('generation.parity'):
      from town_trees import town_markers
      # Compute distance from town center for center_bonus
      lot_world = world_rect(pl, spec['width'], spec['depth'])
      lot_cx = lot_world[0] + lot_world[2] / 2
      lot_cy = lot_world[1] + lot_world[3] / 2
      town_cx = town['box'][0] + town['box'][2] / 2
      town_cy = town['box'][1] + town['box'][3] / 2
      center_dist = math.hypot(lot_cx - town_cx, lot_cy - town_cy) / FT(1)
      # Check if corner lot (fronts two streets)
      is_corner = False
      for road in reg['roads']:
       if road.get('id') == lot.get('road_id'):
        # Simplified: corner if lot touches two perpendicular streets
        pass
      trees_list = town_markers(plan, H(seed, 'town_trees', lid), lid, center_dist, is_corner)
      for i, t in enumerate(trees_list):
       a, b = to_world(pl, spec['width'], spec['depth'], t['x'], t['y'])
       add('trees', lid+':tree:'+str(i), (a, b, 0, 0),
           species=t['species'], setting=t['setting'], condition=t['condition'], label=t['label'])
     else:
      from trees import local_markers
      for i,p in enumerate(local_markers(plan)):
       a,b=to_world(pl,spec['width'],spec['depth'],*p); add('trees',lid+':tree:'+str(i),(a,b,0,0))
  if detail:
   for shop in town['commercial']:
    if shop.get('bldg'): add('buildings','business:'+shop['id'],shop['bldg'],use=shop['type'])
    if not option('generation.parity') and shop.get('lot') and not shop.get('house_lot'):
     add('lots', 'business:'+shop['id'], shop['lot'], use='business:'+shop['type'])
   for i,parcel in enumerate(reg['parcels']):
    if parcel['kind'] in ('elevator','industry'):
     p_rect = parcel['rect']
     if not option('generation.parity') and parcel.get('office'):
      o = parcel['office']
      # Truncate elevator building rect in y so it does not overlap co-op office
      near_top = abs(p_rect[1] + p_rect[3] - town['Yc']) < abs(p_rect[1] - town['Yc'])
      if near_top:
       p_rect = (p_rect[0], p_rect[1], p_rect[2], o[1] - p_rect[1] - FT(5))
      else:
       p_rect = (p_rect[0], o[1] + o[3] + FT(5), p_rect[2], p_rect[1] + p_rect[3] - (o[1] + o[3] + FT(5)))
      add('buildings', 'business:coop-office', o, use='co-op office')
     add('buildings','industry:'+str(i),p_rect,use=parcel['kind'])
   if reg['depot']: add('buildings','depot',reg['depot'],use='depot')
  return out

def settlements(seed,rect,serialized,detail):
 if not option('stages.towns'): return []
 halo=8*MILE; expanded=(rect[0]-halo,rect[1]-halo,rect[2]+2*halo,rect[3]+2*halo)
 candidates=list(town_candidates(seed,expanded)); out=[]
 def priority(site): return (site[-1],H(seed,'town-priority',site[0]))
 for site in candidates:
  id,axis,x,y,c,on_rail=site
  if not intersects((x-MILE,y-MILE,2*MILE,2*MILE),rect): continue
  if any(other[0]!=id and math.dist((x,y),(other[2],other[3]))<6*MILE and priority(other)>priority(site) for other in candidates): continue
  out.extend(_town_scene(seed,site,serialized,detail))
 return out

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
     surface=road_type(seed,axis,global_line,(tx if axis=='h' else ty)*option('world.tile_miles')+1) or 'gravel'
     output.append(dict(id=f'{axis}{i}',name=f'{axis.upper()} {global_line}',axis=axis,type=surface,c=i*MILE+(correction_shift(seed,math.floor(oy/(24*MILE))) if axis=='v' else 0),lo=0,hi=extent,w=option('region.row'),surface_ft=22))
   return output,25
  def rail(local_seed,rs,force):
   if not option('stages.rail'): return None
   ew=next((r for r in rs if r['axis']=='h' and r['type']=='paved'),None)
   if ew is None: return None
   for lane,x,along in rail_lanes(seed,(ox,oy,extent,extent)):
    local=along-ox+correction_shift(seed,math.floor(oy/(24*MILE)))
    if not MILE//2<=local<=extent-MILE//2: continue
    x=round(x-ox)
    return dict(pts=[(x,0),(x,extent)],w=option('region.rail_row'),along=f'v{local//MILE}',along_c=local,x=x,side=1,parallel=(0,extent),crossings=[],site=(local,ew['c']))
   return None
  buffer=FT(max(option('region.creek_buf')))
  water=[f for f in rivers(seed,(ox-buffer,oy-buffer,extent+2*buffer,extent+2*buffer)) if intersects(f['bounds'],(ox-buffer,oy-buffer,extent+2*buffer,extent+2*buffer))]
  def water_hits(local):
   absolute=(local[0]+ox-buffer,local[1]+oy-buffer,local[2]+2*buffer,local[3]+2*buffer)
   return any(_seg_rect(a,b,(absolute[0]-stream['width']/2,absolute[1]-stream['width']/2,absolute[2]+stream['width'],absolute[3]+stream['width'])) for stream in water if intersects(stream['bounds'],absolute) for a,b in zip(stream['points'],stream['points'][1:]))
  tile_seed=H(seed,'settlement',tx,ty)
  barriers=[dict(pts=[(x-ox,y-oy) for x,y in f['points']],w=f['width']+2*buffer) for f in water]
  rural_settings=Settings(settings.values).set('stages.towns',False)
  reg=make_region(tile_seed,road_factory=roads,rail_factory=rail,water_barriers=barriers,settings=rural_settings)
  # Keep ownership disjoint: reject towns whose envelope would invade another tile.
  if reg['town']:
   b=reg['town']['box']
   if b[0]<0 or b[1]<0 or b[0]+b[2]>extent or b[1]+b[3]>extent:
    off=Settings(settings.values).set('stages.towns',False)
    reg=make_region(tile_seed,road_factory=roads,rail_factory=rail,water_barriers=barriers,settings=off)
  results=spawn_region(reg) if detail and option('stages.houses') else {}
  out=[]; farm_sites=[]; prefix=f'{tx}:{ty}:'
  def rect(r): return (r[0]+ox,r[1]+oy,r[2],r[3])
  def add(kind,id,r,**kw): out.append(feature(kind,prefix+id,rect=rect(r),**kw))
  if reg['town']: add('towns','town',reg['town']['box'],size=reg['town']['size'])
  for r in reg['roads']:
   if r in reg['grid_roads']: continue
   add('roads','street:'+r['id'],road_rect(r),surface='paved',axis=r['axis'],history=['town plat','paved'])
  for lot in reg['lots']:
   spec=lot['spec']; id=spec['id']; pl=lot['place']
   if spec['kind']=='farmstead' and H(seed,'farm-density',prefix+id)%10000>=5500: continue
   if water_hits(world_rect(pl,spec['width'],spec['depth'])): continue
   # A rural driveway must terminate on an extant historical section.
   if spec['kind']!='town':
    r=next(r for r in reg['roads'] if r['id']==lot['road_id']); along=int(lot['along']//MILE)
    line=(ty if r['axis']=='h' else tx)*option('world.tile_miles')+round(r['c']/MILE)
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
    rural=layout(seed,spec,plan,access_fraction=(H(seed,'farm-lane',prefix+id)%10000/10000 if spec['kind']=='farmstead' else None))
    if spec['kind']=='farmstead':
     access=[to_world(pl,spec['width'],spec['depth'],a,b) for a,b in rural['access']]
     access=[(a+ox,b+oy) for a,b in access]
     farm_sites.append(dict(id=prefix+id,road=lot['road_id'],access=access,length=rural['access_length'],entry_id=prefix+id+':drive:'+str(rural['entry_index'])))
     for f in out:
      if f['id']==prefix+id: f.update(lane_length_ft=rural['access_length']/FT(1),access=access)
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
  share_farm_lanes(seed,out,farm_sites,water_hits,ox,oy)
  return out

BASIN_MILES=32
BASIN_TICKS=BASIN_MILES*MILE
BASIN_NODES=23
NODE_STEP=BASIN_TICKS/BASIN_NODES

def _drainage_options():
 # Kept as a stable cache key for callers; river spacing/width now comes from
 # the basin network, not the old parallel trunk settings.
 return tuple(option('world.'+k) for k in ('river_spacing_miles','tributary_spacing_miles','river_width_ft','river_wander_miles'))

def _basin_angle(seed):
 return math.radians(10+(H(seed,'basin-angle')%25001)/1000)

def _rot(x,y,a):
 return x*math.cos(a)+y*math.sin(a),-x*math.sin(a)+y*math.cos(a)

def _unrot(x,y,a):
 return x*math.cos(a)-y*math.sin(a),x*math.sin(a)+y*math.cos(a)

def _bearing(seed,sx,sy):
 base=(H(seed,'bearing-base')%12001)/100-60
 key=H(seed,'bearing',sx,sy)
 sign=1 if (sx+sy+H(seed,'bearing-phase')%2)%2==0 else -1
 # Balanced alternating regional flow keeps a broad bearing distribution;
 # the magnitude is still independently keyed per super-region.
 perturb=sign*(27+(key%3001)/1000)
 return math.radians(base+perturb)

def _terrain_potential(seed,x,y):
 """Low-frequency terrain potential; x/y are world ticks."""
 angle=_basin_angle(seed); rx,ry=_rot(x,y,angle)
 super_span=4*BASIN_TICKS
 gx=rx/super_span; gy=ry/super_span; sx=math.floor(gx); sy=math.floor(gy); tx=gx-sx; ty=gy-sy
 # Blend neighboring super-region bearings so potential stays continuous at
 # the 4x4-cell boundaries while retaining each region's seeded flow direction.
 vectors=[]
 for ix,iy,weight in ((sx,sy,(1-tx)*(1-ty)),(sx+1,sy,tx*(1-ty)),(sx,sy+1,(1-tx)*ty),(sx+1,sy+1,tx*ty)):
  theta=_bearing(seed,ix,iy); vectors.append((math.sin(theta)*weight,math.cos(theta)*weight))
 ux=sum(v[0] for v in vectors); uy=sum(v[1] for v in vectors); norm=max(1e-9,math.hypot(ux,uy)); ux/=norm; uy/=norm
 # A gentle N/S and W/E grade dominates the bounded low-frequency noise, so
 # every outlet has a lower-potential neighbor and the coarse route graph is a DAG.
 trend=-(x*ux+y*uy)/MILE*8.0
 gx=rx/(64*MILE); gy=ry/(64*MILE); ix=math.floor(gx); iy=math.floor(gy); tx=gx-ix; ty=gy-iy
 def noise(a,b): return (H(seed,'terrain',a,b)%20001)/1000-10
 n0=noise(ix,iy)*(1-tx)+noise(ix+1,iy)*tx
 n1=noise(ix,iy+1)*(1-tx)+noise(ix+1,iy+1)*tx
 return trend+n0*(1-ty)+n1*ty

def _outlet(seed,cx,cy):
 angle=_basin_angle(seed); key=H(seed,'basin',cx,cy)
 jx=((key%10001)/10000-.5)*.2*BASIN_TICKS
 jy=(((key//10001)%10001)/10000-.5)*.2*BASIN_TICKS
 theta=_bearing(seed,math.floor(cx/4),math.floor(cy/4)); along=_rot(math.sin(theta),math.cos(theta),angle)
 jx+=along[0]*.28*BASIN_TICKS; jy+=along[1]*.28*BASIN_TICKS
 return _unrot(cx*BASIN_TICKS+jx,cy*BASIN_TICKS+jy,angle)

def _cell_owner(seed,x,y,cx,cy,outlets=None):
 outlets=outlets or {(a,b):_outlet(seed,a,b) for a in range(cx-1,cx+2) for b in range(cy-1,cy+2)}
 return min(outlets,key=lambda k:(math.dist((x,y),outlets[k]),H(seed,'voronoi-tie',k[0],k[1])))

@lru_cache(maxsize=65536)
def _next_outlet(seed,cx,cy):
 x,y=_outlet(seed,cx,cy); p=_terrain_potential(seed,x,y)
 neighbors=[]
 for dx in (-1,0,1):
  for dy in (-1,0,1):
   if dx==dy==0: continue
   nx,ny=cx+dx,cy+dy; q=_outlet(seed,nx,ny)
   neighbors.append((_terrain_potential(seed,*q),H(seed,'outlet-route',cx,cy,nx,ny),nx,ny,q))
 lowest=min(neighbors,key=lambda v:(v[0],v[1]))
 return lowest[2],lowest[3],lowest[4]

@lru_cache(maxsize=65536)
def _upstream_outlets(seed,cx,cy):
 return tuple((x,y) for x in range(cx-1,cx+2) for y in range(cy-1,cy+2)
  if (x,y)!=(cx,cy) and _next_outlet(seed,x,y)[:2]==(cx,cy))[:3]

@lru_cache(maxsize=65536)
def _route_curve_raw(seed,cx,cy):
 ident=f'river:outlet:{cx}:{cy}'; start=_outlet(seed,cx,cy); nx,ny,end=_next_outlet(seed,cx,cy)
 pts=_edge_curve(seed,ident,start,end,FT(100),('outlet',cx,cy))
 return ident,pts

@lru_cache(maxsize=65536)
def _route_curve(seed,cx,cy):
 ident,pts=_route_curve_raw(seed,cx,cy); nx,ny,_=_next_outlet(seed,cx,cy)
 _,downstream=_route_curve_raw(seed,nx,ny); join=pts[-1]; a,b=join,downstream[1]
 vx,vy=b[0]-a[0],b[1]-a[1]; old=(join[0]-pts[-2][0],join[1]-pts[-2][1])
 length=min(math.dist(join,pts[-2]),FT(30))
 if length and math.hypot(vx,vy):
  side=1 if vx*old[1]-vy*old[0]>=0 else -1
  u=(H(seed,'junction-angle',ident)%10001)/10000
  theta=math.radians(25+60*u); direction=math.atan2(vy,vx)+side*theta
  pts=list(pts); pts[-2]=(join[0]-length*math.cos(direction),join[1]-length*math.sin(direction))
 return ident,pts

@lru_cache(maxsize=65536)
def _route_geometry(seed,cx,cy):
 ident,pts=_route_curve(seed,cx,cy); nx,ny,_=_next_outlet(seed,cx,cy)
 # Canonical <=200-ft samples support width interpolation, junction attachment,
 # and the same river shape at every display zoom.
 sampled=[]
 for a,b in zip(pts,pts[1:]):
  n=max(1,math.ceil(math.dist(a,b)/FT(200)))
  sampled.extend((a[0]+(b[0]-a[0])*i/n,a[1]+(b[1]-a[1])*i/n) for i in range(n))
 sampled.append(pts[-1])
 return feature('rivers',ident,points=sampled,width=FT(100),widths=[FT(100)]*len(sampled),stream_order=3,flow='regional',source=not _upstream_outlets(seed,cx,cy),downstream=f'river:outlet:{nx}:{ny}',basin=(cx,cy),area=3*MILE)

@lru_cache(maxsize=65536)
def _route_targets(seed,cx,cy):
 # Local headwater edges drain to their own basin outlet reach. Upstream
 # outlet reaches remain separate links which join at basin outlets. Reserve
 # the immediate outlet junction for those coarse links; local branches join
 # the trunk after a short channel-length buffer.
 ident,points=_route_curve(seed,cx,cy); cut=.75*NODE_STEP; distance=0; start=0
 for i,(a,b) in enumerate(zip(points,points[1:])):
  distance+=math.dist(a,b)
  if distance>=cut: start=i+1; break
 return ((ident,tuple(points[start:] or points[-2:])),)

@lru_cache(maxsize=256)
def _segment_index(points):
 """Bucket an outlet polyline so a nearest-point query can skip far spans.

 The buckets cover each segment's box. A query that has already found a point
 closer than the unsearched cells can stop. The (distance, station) pair
 matches the linear scan.
 """
 cell=MILE; segs=[]; buckets={}; before=0.0
 for a,b in zip(points,points[1:]):
  length=math.dist(a,b); segs.append((a,b,length,before)); before+=length
  ix0,ix1=math.floor(min(a[0],b[0])/cell),math.floor(max(a[0],b[0])/cell)
  iy0,iy1=math.floor(min(a[1],b[1])/cell),math.floor(max(a[1],b[1])/cell)
  for ix in range(ix0,ix1+1):
   for iy in range(iy0,iy1+1): buckets.setdefault((ix,iy),[]).append(len(segs)-1)
 return (cell,tuple(segs),buckets,before)

def _nearest_on_index(p,ident,index):
 cell,segs,buckets,total=index
 ix,iy=math.floor(p[0]/cell),math.floor(p[1]/cell)
 best=None; best_d=math.inf; seen=set()
 for radius in range(0,96):
  if radius==0: keys=((ix,iy),)
  else:
   keys=[(x,iy-radius) for x in range(ix-radius,ix+radius+1)]+[(x,iy+radius) for x in range(ix-radius,ix+radius+1)]
   keys+=[(ix-radius,y) for y in range(iy-radius+1,iy+radius)]+[(ix+radius,y) for y in range(iy-radius+1,iy+radius)]
  for key in keys:
   for i in buckets.get(key,()):
    if i in seen: continue
    seen.add(i)
    a,b,length,before=segs[i]; dx,dy=b[0]-a[0],b[1]-a[1]
    t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/(dx*dx+dy*dy)))
    q=(a[0]+t*dx,a[1]+t*dy); d=math.dist(p,q); sample=a if t<.5 else b
    station=(before+t*length)/total if total else 0
    candidate=(d,ident,station,sample)
    if best is None or candidate[:3]<best[:3]: best=candidate; best_d=d
  if best is not None and best_d<radius*cell: return best
 return best

def _nearest_route(p,routes,barriers=()):
 if not barriers and len(routes)==1:
  ident,points=routes[0]
  found=_nearest_on_index(p,ident,_segment_index(points))
  if found is not None: return found
 candidates=[]; best=None
 for ident,points in routes:
  lengths=[math.dist(a,b) for a,b in zip(points,points[1:])]; total=sum(lengths); before=0
  for i,(a,b,length) in enumerate(zip(points,points[1:],lengths)):
   dx,dy=b[0]-a[0],b[1]-a[1]; t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/(dx*dx+dy*dy)))
   q=(a[0]+t*dx,a[1]+t*dy); d=math.dist(p,q); sample=a if t<.5 else b
   station=(before+t*length)/total if total else 0
   candidate=(d,ident,station,sample)
   if barriers: candidates.append(candidate)
   elif best is None or candidate[:3]<best[:3]: best=candidate
   before+=length
 if barriers:
  local_barriers=tuple(r for r in barriers if r[0] not in {route[0] for route in routes})
  for candidate in sorted(candidates)[:32]:
   if not _crosses_owned_route(p,candidate[3],local_barriers): best=candidate; break
 if best is None:
  fallback=_nearest_route(p,routes)
  _,origin,_,end=fallback; hits=[]
  for rid,points,*_ in barriers:
   if rid==origin: continue
   lengths=[math.dist(a,b) for a,b in zip(points,points[1:])]; total=sum(lengths); before=0
   for a,b,length in zip(points,points[1:],lengths):
    hit=crossing_point(p,end,a,b)
    if hit is not None and 1e-6<hit[1]<1-1e-6:
     q=min(points,key=lambda v:math.dist(v,hit[0])); station=(before+hit[2]*length)/total if total else 0
     hits.append((hit[1],rid,station,q))
    before+=length
  if hits:
   t,ident,station,q=min(hits,key=lambda h:(h[0],h[1],h[2]))
   return math.dist(p,q),ident,station,q
  return fallback
 d,ident,station,q=best
 return d,ident,station,q

def _network_potential(seed,p,routes):
 d,ident,station,_=_nearest_route(p,routes)
 route_id=ident.split(':'); cx,cy=int(route_id[-2]),int(route_id[-1])
 nx,ny,_=_next_outlet(seed,cx,cy)
 a=_terrain_potential(seed,*_outlet(seed,cx,cy)); b=_terrain_potential(seed,*_outlet(seed,nx,ny))
 return d/MILE*3+(.02*((1-station)*a+station*b))

def _crosses_owned_route(a,b,routes):
 bounds=(min(a[0],b[0]),min(a[1],b[1]),abs(b[0]-a[0]),abs(b[1]-a[1]))
 for route in routes:
  _,points,*cached_bounds=route; route_bounds=cached_bounds[0] if cached_bounds else box(points)
  if not intersects(bounds,route_bounds): continue
  for c,d in zip(points,points[1:]):
   if max(a[0],b[0])<min(c[0],d[0]) or max(c[0],d[0])<min(a[0],b[0]) or max(a[1],b[1])<min(c[1],d[1]) or max(c[1],d[1])<min(a[1],b[1]): continue
   hit=crossing_point(a,b,c,d)
   if hit is None: continue
   _,t,s,_=hit
   if t<1e-6 or t>1-1e-6: continue
   return True
 return False

def _node_point(seed,gx,gy):
 jitter=.08*NODE_STEP
 ox=((H(seed,'node-x',gx,gy)%10001)/5000-1)*jitter
 oy=((H(seed,'node-y',gx,gy)%10001)/5000-1)*jitter
 return _unrot(gx*NODE_STEP+ox,gy*NODE_STEP+oy,_basin_angle(seed))

def _basin_polygon(seed,cx,cy):
 """Voronoi polygon clipped against this outlet's bounded 3x3 neighborhood."""
 own=_rot(*_outlet(seed,cx,cy),_basin_angle(seed))
 extent=1.4*BASIN_TICKS; mx=cx*BASIN_TICKS; my=cy*BASIN_TICKS
 poly=[(mx-extent,my-extent),(mx+extent,my-extent),(mx+extent,my+extent),(mx-extent,my+extent)]
 for (nx,ny),world in sorted({(x,y):_outlet(seed,x,y) for x in range(cx-1,cx+2) for y in range(cy-1,cy+2) if (x,y)!=(cx,cy)}.items()):
  other=_rot(*world,_basin_angle(seed)); vx=other[0]-own[0]; vy=other[1]-own[1]
  limit=(other[0]**2+other[1]**2-own[0]**2-own[1]**2)/2
  if not poly: break
  clipped=[]
  for a,b in zip(poly,poly[1:]+poly[:1]):
   da=vx*a[0]+vy*a[1]-limit; db=vx*b[0]+vy*b[1]-limit
   ina=da<=1e-6; inb=db<=1e-6
   if ina: clipped.append(a)
   if ina!=inb:
    t=da/(da-db); clipped.append((a[0]+t*(b[0]-a[0]),a[1]+t*(b[1]-a[1])))
  poly=clipped
 return poly

def _point_in_polygon(p,poly):
 inside=False; x,y=p
 for a,b in zip(poly,poly[1:]+poly[:1]):
  if (a[1]>y)!=(b[1]>y) and x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]: inside=not inside
 return inside

def _finished_receiver_tail(receiver,join,by_id):
 idx=min(range(len(receiver['points'])),key=lambda i:math.dist(receiver['points'][i],join))
 tail=[join]+receiver['points'][idx+1:]; seen={receiver['id']}; nxt=receiver
 while sum(math.dist(a,b) for a,b in zip(tail,tail[1:]))<.25*MILE:
  nxt=by_id.get(nxt.get('downstream'))
  if nxt is None or nxt['id'] in seen: break
  seen.add(nxt['id']); tail+=nxt['points'][1:]
 return tail

def _angle_reaches(seed,features):
 """Pin a confluence approach over the measured quarter mile, not one vertex."""
 by_id={f['id']:f for f in features}; incoming={}
 protected=[]; protected_local={}
 for route in sorted((f for f in features if f['id'].startswith('river:outlet:')),key=lambda f:f['id']):
  x,y=map(int,route['id'].split(':')[-2:]); rid,points=_route_curve(seed,x,y)
  protected.append((rid,_segment_index(tuple(points)),route['width']))
 def blocked_approach(point,heading,run,ident,width):
  point=tuple(point)
  if point not in protected_local:
   local=[]
   for rid,(cell,segs,buckets,total),rw in protected:
    ids=set()
    for ix in range(math.floor((point[0]-.301*MILE)/cell),math.floor((point[0]+.301*MILE)/cell)+1):
     for iy in range(math.floor((point[1]-.301*MILE)/cell),math.floor((point[1]+.301*MILE)/cell)+1): ids.update(buckets.get((ix,iy),()))
    for i in sorted(ids):
     a,b,_,_=segs[i]
     if _distance_to_segment(*point,a,b)<=.301*MILE: local.append((rid,a,b,rw))
   protected_local[point]=local
  anchor=(point[0]-run*math.cos(heading),point[1]-run*math.sin(heading))
  for rid,a,b,rw in protected_local[point]:
   if rid==ident: continue
   hit=crossing_point(anchor,point,a,b)
   if hit is not None and math.dist(hit[0],point)>max(width,rw)+FT(50): return True
  return False
 for f in features:
  incoming.setdefault((f.get('downstream'),tuple(f['points'][-1])),[]).append(f)
 for (rid,point),group in sorted(incoming.items(),key=lambda item:str(item[0])):
  receiver=by_id.get(rid)
  if receiver is None or len(group)<2 or math.dist(receiver['points'][0],point)>.01: continue
  end=receiver['points'][-1]; dx,dy=end[0]-point[0],end[1]-point[1]; length=math.hypot(dx,dy)
  if length<=1: continue
  tangent=(dx/length,dy/length)
  receiver['_junction_seed']=seed
  receiver['_confluence_inflows']=len(group)
  receiver['_junction_start']=tangent
  _aim_end(receiver,tangent,False,.3*MILE)
 ordered=[]; done=set()
 for first in sorted(features,key=lambda f:f['id']):
  path=[]; current=first; seen=set()
  while current is not None and current['id'] not in done and current['id'] not in seen:
   path.append(current); seen.add(current['id']); current=by_id.get(current.get('downstream'))
  for reach in reversed(path): ordered.append(reach); done.add(reach['id'])
 for f in ordered:
  receiver=by_id.get(f.get('downstream'))
  if receiver is None or len(f['points'])<3: continue
  join=f['points'][-1]
  group=incoming.get((receiver['id'],tuple(join)),())
  if len(group)==1 and math.dist(receiver['points'][0],join)<.01: continue
  tail=_finished_receiver_tail(receiver,join,by_id)
  if len(tail)<2: continue
  _,ahead=_cut_from_start(tail,.25*MILE)
  vx,vy=ahead[0]-join[0],ahead[1]-join[1]
  if not math.hypot(vx,vy): continue
  uneven=f.get('stream_order')!=receiver.get('stream_order')
  normal=sum((H(seed,'junction-normal',f['id'],receiver['id'],i)%100000)/100000 for i in range(12))-6
  angle_deg=65+35*normal
  angle_deg+=.25*((90 if uneven else 58)-angle_deg)
  old=(join[0]-f['points'][0][0],join[1]-f['points'][0][1])
  raw=math.degrees(math.acos(max(-1,min(1,(vx*old[0]+vy*old[1])/(math.hypot(vx,vy)*math.hypot(*old))))))
  if f.get('_spacing_angle_limited'): angle_deg=max(f.get('_spacing_angle_floor',26),min(160,raw))
  _,near=_cut_from_start(tail,.1*MILE)
  near_heading=math.atan2(near[1]-join[1],near[0]-join[0])
  base_heading=math.atan2(vy,vx); chord_heading=math.atan2(old[1],old[0])
  side=1 if vx*old[1]-vy*old[0]>=0 else -1
  choices=[]
  wrap=lambda x:(x+math.pi)%(2*math.pi)-math.pi
  run=min(.3*MILE,math.hypot(*old)*.45)
  ranges=((f.get('_spacing_angle_floor',26),160,f.get('_spacing_angle_floor',26)),(15,165,15)) if f.get('_spacing_angle_limited') else (((55,125,25) if f.get('stream_order',1)>=2 else (35,110,25)),(15,165,15))
  for low,high,near_minimum in ranges:
   for bank in (side,-side):
    for deg in range(low,high+1):
     heading=base_heading+bank*math.radians(deg)
     if abs(wrap(heading-chord_heading))>math.radians(f.get('_spacing_rotation_limit',82)): continue
     if abs(wrap(heading-near_heading))<math.radians(near_minimum): continue
     if blocked_approach(join,heading,run,f['id'],f['width']): continue
     choices.append((0 if bank==side else 1,abs(deg-angle_deg),deg,heading))
   if choices: break
  if choices:
   _,_,deg,direction=min(choices); angle=math.radians(deg)
  else:
   angle=math.radians(max(35,raw-82,min(110,raw+82,angle_deg)))
   direction=base_heading+side*angle
  tangent=(math.cos(direction),math.sin(direction))
  f['_junction_seed']=seed
  f['_junction_sample']=math.degrees(angle)
  f['_junction_direction']=tangent
  _aim_end(f,tangent,True,.3*MILE)

 # A planar junction must retain the angular order of its incoming chords.
 # Reassign the sampled set within the group rather than pinning crossed arms.
 for (rid,point),group in incoming.items():
  arms=[f for f in group if f.get('_junction_direction')]
  receiver=by_id.get(rid)
  if len(arms)<2 or receiver is None: continue
  tail=_finished_receiver_tail(receiver,point,by_id)
  if len(tail)<2: continue
  _,ahead=_cut_from_start(tail,.25*MILE); base=math.atan2(ahead[1]-point[1],ahead[0]-point[0])
  relative=lambda angle:(angle-base+math.pi)%(2*math.pi)-math.pi
  ordered_arms=sorted(arms,key=lambda f:(relative(math.atan2(point[1]-f['points'][0][1],point[0]-f['points'][0][0])),f['id']))
  proposals=sorted(((relative(math.atan2(f['_junction_direction'][1],f['_junction_direction'][0])),f['_junction_direction'],f['_junction_sample']) for f in arms),key=lambda item:item[0])
  # Two higher-order arms also need room beyond the shared 0.5-mi pocket.
  # Preserve their bank and angular order, but spread a cramped sampled pair.
  if len(arms)==2 and all(f.get('stream_order',0)>=2 for f in arms) and not any(f.get('_spacing_angle_limited') for f in arms):
   a0,a1=proposals[0][0],proposals[1][0]
   if a0*a1>0 and a1-a0<math.radians(65):
    raw_angles=[relative(math.atan2(point[1]-f['points'][0][1],point[0]-f['points'][0][0])) for f in ordered_arms]
    _,near=_cut_from_start(tail,.1*MILE); near_angle=relative(math.atan2(near[1]-point[1],near[0]-point[0]))
    lo,hi=(math.radians(26),math.radians(154)) if a0>0 else (math.radians(-154),math.radians(-26))
    for gap_deg in (65,60,55,50):
     gap=math.radians(gap_deg)
     lower=max(lo,raw_angles[0]-math.radians(82),raw_angles[1]-math.radians(82)-gap)
     upper=min(hi-gap,raw_angles[0]+math.radians(82),raw_angles[1]+math.radians(82)-gap)
     if lower>upper: continue
     ideal=max(lower,min(upper,(a0+a1-gap)/2))
     choices=[ideal]+[lower+(upper-lower)*i/64 for i in range(65)]
     valid=[first for first in choices if all(abs((theta-near_angle+math.pi)%(2*math.pi)-math.pi)>=math.radians(25) and not blocked_approach(point,base+theta,min(.3*MILE,math.dist(arm['points'][0],point)*.45),arm['id'],arm['width']) for theta,arm in zip((first,first+gap),ordered_arms))]
     if not valid: continue
     first=min(valid,key=lambda theta:(abs(theta-ideal),theta))
     proposals=[(theta,(math.cos(base+theta),math.sin(base+theta)),abs(math.degrees(theta))) for theta in (first,first+gap)]
     break
  # Distinct draws can round to the same degree. Keep their approaches
  # separate instead of allowing overlapping 0.3-mi stubs.
  _,near=_cut_from_start(tail,.1*MILE); near_angle=relative(math.atan2(near[1]-point[1],near[0]-point[0]))
  desired=[item[0] for item in proposals]; bounds=[]
  for arm,target in zip(ordered_arms,desired):
   raw=relative(math.atan2(point[1]-arm['points'][0][1],point[0]-arm['points'][0][0]))
   raw+=2*math.pi*round((target-raw)/(2*math.pi))
   floor=15 if arm.get('_spacing_angle_limited') else 26
   low,high=(math.radians(floor),math.radians(165)) if target>0 else (math.radians(-165),math.radians(-floor))
   rotation_limit=math.radians(arm.get('_spacing_rotation_limit',82))
   low=max(low,raw-rotation_limit); high=min(high,raw+rotation_limit)
   for pivot in (near_angle,near_angle-2*math.pi,near_angle+2*math.pi):
    if pivot+math.radians(15)<low or pivot-math.radians(15)>high: continue
    if target<=pivot: high=min(high,pivot-math.radians(15))
    else: low=max(low,pivot+math.radians(15))
   bounds.append((low,high))
  for gap_deg in (20,15,10,5,3,1):
   gap=math.radians(gap_deg); highs=[b[1] for b in bounds]
   for i in range(len(highs)-2,-1,-1): highs[i]=min(highs[i],highs[i+1]-gap)
   angles=[]
   for i,(low,high) in enumerate(bounds):
    value=max(low,desired[i],angles[-1]+gap if angles else -math.inf)
    if value>highs[i]+1e-12: break
    angles.append(value)
   if len(angles)!=len(arms): continue
   shift_lo=max(b[0]-theta for b,theta in zip(bounds,angles)); shift_hi=min(b[1]-theta for b,theta in zip(bounds,angles))
   shifts=[max(shift_lo,min(shift_hi,0))]+[math.radians(i) for i in range(-30,31) if shift_lo<=math.radians(i)<=shift_hi]
   for shift in sorted(set(shifts),key=lambda value:(abs(value),value)):
    if any(blocked_approach(point,base+theta+shift,min(.3*MILE,math.dist(arm['points'][0],point)*.45),arm['id'],arm['width']) for theta,arm in zip(angles,ordered_arms)): continue
    proposals=[(theta+shift,(math.cos(base+theta+shift),math.sin(base+theta+shift)),abs(math.degrees(theta+shift))) for theta in angles]
    break
   else: continue
   break
  for arm,(_,tangent,sample) in zip(ordered_arms,proposals):
   arm['_junction_direction']=tangent; arm['_junction_sample']=sample
   _aim_end(arm,tangent,True,.3*MILE)

def _curve_collision(a,b,allowed=()):
 allowed=tuple(allowed); cell=FT(200); buckets={}; segments=list(zip(b,b[1:]))
 for j,(r,s) in enumerate(segments):
  for ix in range(math.floor(min(r[0],s[0])/cell),math.floor(max(r[0],s[0])/cell)+1):
   for iy in range(math.floor(min(r[1],s[1])/cell),math.floor(max(r[1],s[1])/cell)+1): buckets.setdefault((ix,iy),[]).append(j)
 for i,(p,q) in enumerate(zip(a,a[1:])):
  possible=set()
  for ix in range(math.floor(min(p[0],q[0])/cell),math.floor(max(p[0],q[0])/cell)+1):
   for iy in range(math.floor(min(p[1],q[1])/cell),math.floor(max(p[1],q[1])/cell)+1): possible.update(buckets.get((ix,iy),()))
  for j in possible:
   r,s=segments[j]
   if a is b and abs(i-j)<=1: continue
   if max(p[0],q[0])<min(r[0],s[0]) or max(r[0],s[0])<min(p[0],q[0]) or max(p[1],q[1])<min(r[1],s[1]) or max(r[1],s[1])<min(p[1],q[1]): continue
   hit=crossing_point(p,q,r,s)
   if hit is None: continue
   point=hit[0]
   if any(math.dist(point,junction)<=tolerance for junction,tolerance in allowed): continue
   _,t,u,_=hit
   if (t<1e-7 or t>1-1e-7) and (u<1e-7 or u>1-1e-7): continue
   return True
 return False

def _untangle_reaches(seed,features):
 """Reject crossings and clearance violations using one id-sorted bucket grid."""
 from collections import defaultdict
 channels=sorted((f for f in features if not f['id'].startswith('river:outlet:')),key=lambda f:f['id'])
 cell=.1*MILE; buckets=defaultdict(set); wide_buckets=defaultdict(set); segments={}; data={}; crossing_blockers=set()
 by_id={f['id']:f for f in features}
 def cells(a,b,pad=0):
  return ((ix,iy) for ix in range(math.floor((min(a[0],b[0])-pad)/cell),math.floor((max(a[0],b[0])+pad)/cell)+1)
   for iy in range(math.floor((min(a[1],b[1])-pad)/cell),math.floor((max(a[1],b[1])+pad)/cell)+1))
 def geometry(f):
  pts=f['points']; arc=[0.0]; changes=[0.0]; previous=0
  headings=[]
  for a,b in zip(pts,pts[1:]):
   arc.append(arc[-1]+math.dist(a,b)); headings.append(math.atan2(b[1]-a[1],b[0]-a[0]))
  for i in range(1,len(headings)):
   turn=(headings[i]-headings[i-1]+math.pi)%(2*math.pi)-math.pi
   sign=previous if abs(turn)<math.radians(.4) else (1 if turn>0 else -1)
   if previous and sign!=previous: changes.append(arc[i])
   previous=sign
  changes.append(arc[-1]); waves=[]; j=0
  for i in range(len(pts)-1):
   while j<len(changes)-2 and arc[i]>changes[j+1]: j+=1
   waves.append(2*(changes[j+1]-changes[j]))
  return arc,waves
 def add(f):
  arc,waves=geometry(f); data[f['id']]=(arc,waves)
  for j,(a,b) in enumerate(zip(f['points'],f['points'][1:])):
   key=(f['id'],j); segments[key]=(f,a,b)
   for c in cells(a,b):
    buckets[c].add(key)
    if f.get('stream_order',0)>=2: wide_buckets[c].add(key)
 def remove(f):
  for j,(a,b) in enumerate(zip(f['points'],f['points'][1:])):
   key=(f['id'],j); segments.pop(key,None)
   for c in cells(a,b):
    bucket=buckets.get(c)
    if bucket is not None:
     bucket.discard(key)
     if not bucket: buckets.pop(c,None)
    wide=wide_buckets.get(c)
    if wide is not None:
     wide.discard(key)
     if not wide: wide_buckets.pop(c,None)
 def near(s0,s1,station): return min(abs(s0-station),abs(s1-station))<.5*MILE
 def collision(f,clearance=True):
  pts=f['points']; arc,waves=geometry(f); local=defaultdict(set); relations={}
  if _curve_collision(pts,pts): return True
  for i,(a,b) in enumerate(zip(pts,pts[1:])):
   possible=set()
   for c in cells(a,b): possible.update(buckets.get(c,()))
   if clearance and f.get('stream_order',0)>=2:
    for c in cells(a,b,.4*MILE): possible.update(wide_buckets.get(c,()))
   for key in sorted(possible):
    other,c,d=segments[key]; j=key[1]
    margin=.4*MILE if clearance and min(f.get('stream_order',0),other.get('stream_order',0))>=2 else 0
    if max(a[0],b[0])+margin<min(c[0],d[0]) or max(c[0],d[0])+margin<min(a[0],b[0]) or max(a[1],b[1])+margin<min(c[1],d[1]) or max(c[1],d[1])+margin<min(a[1],b[1]): continue
    hit=crossing_point(a,b,c,d)
    if hit is not None:
     point,t,u,_=hit
     allowed=(t<1e-7 or t>1-1e-7) and (u<1e-7 or u>1-1e-7)
     allowed=allowed or (f.get('downstream')==other['id'] and math.dist(point,pts[-1])<=max(f['width'],other['width'])+FT(20))
     allowed=allowed or (other.get('downstream')==f['id'] and math.dist(point,other['points'][-1])<=max(f['width'],other['width'])+FT(20))
     if not allowed:
      if not clearance: crossing_blockers.add(other['id'])
      return True
    if not margin: continue
    other_arc=data[other['id']][0]
    if other['id'] not in relations:
     left=right=None
     if f.get('downstream')==other['id']:
      left=arc[-1]; idx=min(range(len(other['points'])),key=lambda k:math.dist(other['points'][k],pts[-1])); right=other_arc[idx]
     elif other.get('downstream')==f['id']:
      right=other_arc[-1]; idx=min(range(len(pts)),key=lambda k:math.dist(pts[k],other['points'][-1])); left=arc[idx]
     elif f.get('downstream')==other.get('downstream') and math.dist(pts[-1],other['points'][-1])<.01:
      left=arc[-1]; right=other_arc[-1]
     relations[other['id']]=(left,right)
    left,right=relations[other['id']]
    if left is not None and (near(arc[i],arc[i+1],left) or near(other_arc[j],other_arc[j+1],right)): continue
    distance=0 if hit is not None else min(_distance_to_segment(*a,c,d),_distance_to_segment(*b,c,d),_distance_to_segment(*c,a,b),_distance_to_segment(*d,a,b))
    if distance<margin-1e-6: return True
   # Same-reach clearance uses a separate view of the same grid. Junction
   # pockets and adjacent waves are exempt, as in the finished-map check.
   own=set()
   for c in cells(a,b,2*f['width']): own.update(local.get(c,()))
   for j in sorted(own):
    if arc[i]-arc[j+1]<=max(waves[i],waves[j]): continue
    if f.get('_confluence_inflows',1)>1 and arc[j]<.5*MILE: continue
    if f.get('_junction_direction') and arc[-1]-arc[i+1]<.5*MILE: continue
    c,d=pts[j],pts[j+1]
    hit=crossing_point(a,b,c,d)
    distance=0 if hit is not None else min(_distance_to_segment(*a,c,d),_distance_to_segment(*b,c,d),_distance_to_segment(*c,a,b),_distance_to_segment(*d,a,b))
    if (clearance and distance<2*f['width']-1e-6) or hit is not None: return True
   for c in cells(a,b): local[c].add(i)
  return False
 for f in features: add(f)
 for f in channels:
  start,end=f['points'][0],f['points'][-1]; remove(f)
  f.setdefault('_pin_wave_limit',19); f.setdefault('_approach_blend',.5)
  original={key:f[key] for key in ('points','widths','width','bounds','_pin_wave_limit','_approach_blend')}
  original['_spacing_phase']=f.get('_spacing_phase',0)
  original['_spacing_wave_scale']=f.get('_spacing_wave_scale',1.0)
  candidates=[]
  reseeded=False; fallback=f.get('_spacing_fallback','none')
  needs_repair=collision(f)
  if not needs_repair and fallback=='unresolved': fallback='none'
  if needs_repair:
   reseeded=True; found=False; fallback='none'
   for k in range(8):
    phase=H(seed,f['id'],'space',k)
    f['_spacing_phase']=phase
    f['_spacing_wave_scale']=1-.12*k
    pts=_edge_curve(seed,f['id'],start,end,f['width'],phase,f.get('stream_order',1),meander_scale=1-.12*k)
    _resample_widths(f,pts)
    if f.get('_junction_start'): _aim_end(f,f['_junction_start'],False,.3*MILE)
    if f.get('_junction_direction'): _aim_end(f,f['_junction_direction'],True,.3*MILE)
    candidates.append({key:f[key] for key in ('points','widths','width','bounds','_spacing_phase','_spacing_wave_scale','_pin_wave_limit','_approach_blend')})
    if not collision(f): found=True; break
   if not found:
    for label,wave,bow in (('valley',0,1),('straight',0,0)):
     f['_spacing_phase']=H(seed,f['id'],'space',8)
     f['_spacing_wave_scale']=wave
     pts=_edge_curve(seed,f['id'],start,end,f['width'],H(seed,f['id'],'space',8),f.get('stream_order',1),meander_scale=wave,valley_scale=bow)
     _resample_widths(f,pts)
     if f.get('_junction_start'): _aim_end(f,f['_junction_start'],False,.3*MILE)
     if f.get('_junction_direction'): _aim_end(f,f['_junction_direction'],True,.3*MILE)
     fallback=label
     candidates.append({key:f[key] for key in ('points','widths','width','bounds','_spacing_phase','_spacing_wave_scale','_pin_wave_limit','_approach_blend')})
     if not collision(f): found=True; break
   if not found:
    # A straight candidate that still violates fixed constraints is not an
    # accepted fallback. Retain a crossing-free curve and report unresolved
    # clearance rather than flattening an impossible pocket gratuitously.
    crossing_blockers.clear(); crossing_safe=False
    for state in [original]+candidates:
     f.update(state)
     if not collision(f,clearance=False): crossing_safe=True; break
    if not crossing_safe:
     # A sampled tangent can force a crossing that no phase can remove.
     # Relax only the most rotated affected arm toward its lattice chord;
     # the next bounded sweep still enforces the measured angle floor.
     affected=[by_id[ident] for ident in ({f['id']}|crossing_blockers) if ident in by_id]
     affected=[g for g in affected if g.get('_junction_direction') and not g['id'].startswith('river:outlet:') and not g.get('_spacing_angle_limited')]
     def rotation(g):
      a,b=g['points'][0],g['points'][-1]; v=g['_junction_direction']
      return abs((math.atan2(v[1],v[0])-math.atan2(b[1]-a[1],b[0]-a[0])+math.pi)%(2*math.pi)-math.pi)
     if affected:
      max(affected,key=lambda g:(rotation(g),g['id']))['_spacing_angle_limited']=True
     else:
      for ident in ({f['id']}|crossing_blockers):
       g=by_id.get(ident)
       if g and g.get('_spacing_angle_limited'):
        g['_spacing_angle_floor']=16; g['_spacing_rotation_limit']=20
    fallback='unresolved'
  f['_spacing_reseeded']=bool(f.get('_spacing_reseeded')) or reseeded; f['_spacing_fallback']=fallback
  add(f)

def _hermite(p0,m0,p1,m1,n):
 pts=[]
 for i in range(n+1):
  t=i/n; t2=t*t; t3=t2*t
  h00=2*t3-3*t2+1; h10=t3-2*t2+t; h01=-2*t3+3*t2; h11=t3-t2
  pts.append((h00*p0[0]+h10*m0[0]+h01*p1[0]+h11*m1[0], h00*p0[1]+h10*m0[1]+h01*p1[1]+h11*m1[1]))
 return pts

def _scale_dir(dx,dy,length):
 n=math.hypot(dx,dy) or 1
 return dx/n*length, dy/n*length

def _cut_from_end(pts,dist):
 acc=0.0
 for i in range(len(pts)-1,0,-1):
  seg=math.dist(pts[i-1],pts[i])
  if acc+seg>=dist or i==1:
   back=min(seg,max(0.0,dist-acc)); t=back/seg if seg else 0
   return i,(pts[i][0]-t*(pts[i][0]-pts[i-1][0]), pts[i][1]-t*(pts[i][1]-pts[i-1][1]))
  acc+=seg
 return 1,pts[0]

def _cut_from_start(pts,dist):
 acc=0.0
 for i in range(len(pts)-1):
  seg=math.dist(pts[i],pts[i+1])
  if acc+seg>=dist or i==len(pts)-2:
   fore=min(seg,max(0.0,dist-acc)); t=fore/seg if seg else 0
   return i+1,(pts[i][0]+t*(pts[i+1][0]-pts[i][0]), pts[i][1]+t*(pts[i+1][1]-pts[i][1]))
  acc+=seg
 return len(pts)-1,pts[-1]

def _arc_samples(points,step):
 out=[points[0]]; travelled=0.0; target=step
 for a,b in zip(points,points[1:]):
  length=math.dist(a,b)
  if length<=0: continue
  while travelled+length>=target-1e-9:
   t=(target-travelled)/length
   out.append((a[0]+t*(b[0]-a[0]),a[1]+t*(b[1]-a[1]))); target+=step
  travelled+=length
 if math.dist(out[-1],points[-1])>step*.25: out.append(points[-1])
 return out

def _resample_widths(feature,points):
 clean=[points[0]]
 for point in points[1:]:
  if math.dist(clean[-1],point)>1e-6: clean.append(point)
 if len(clean)<2: clean=[points[0],points[-1]]
 else: clean[-1]=points[-1]
 points=clean
 widths=feature['widths'] or [feature['width']]
 feature['points']=points
 feature['widths']=[widths[min(len(widths)-1,round(i/(len(points)-1)*(len(widths)-1)))] for i in range(len(points))]
 feature['width']=max(feature['widths'])
 feature['bounds']=box(points,max(feature['widths'])/2)

def _pin_approach_graph(feature,tangent,at_end,run):
 """A monotone chord graph preserves the no-fold guarantee during pinning."""
 pts=list(feature['points']) if at_end else list(reversed(feature['points']))
 if not at_end: tangent=(-tangent[0],-tangent[1])
 a,b=pts[0],pts[-1]; chord=math.dist(a,b)
 if chord<=1: return
 ux,uy=(b[0]-a[0])/chord,(b[1]-a[1])/chord; nx,ny=-uy,ux
 tx=tangent[0]*ux+tangent[1]*uy; ty=tangent[0]*nx+tangent[1]*ny
 if tx<=.05: return
 run=min(run,chord*.45)
 ax=chord-run*tx; ay=-run*ty
 blend=.12*MILE if feature.get('stream_order',1)>=3 else feature.get('_approach_blend',.5)*MILE
 cx=max(0,ax-min(blend,chord*(.5 if feature.get('stream_order',1)>=3 else (.95 if feature.get('stream_order')==1 else .8)))); cut=None
 for i,(p,q) in enumerate(zip(pts,pts[1:])):
  px=(p[0]-a[0])*ux+(p[1]-a[1])*uy; qx=(q[0]-a[0])*ux+(q[1]-a[1])*uy
  if px<=cx<=qx and qx>px:
   py=(p[0]-a[0])*nx+(p[1]-a[1])*ny; qy=(q[0]-a[0])*nx+(q[1]-a[1])*ny
   y=py+(qy-py)*(cx-px)/(qx-px); slope=max(-1.5,min(1.5,(qy-py)/(qx-px)))
   cut=(i,y,slope); break
 if cut is None: return
 i,cy,slope=cut; span=ax-cx; n=max(2,math.ceil(span/FT(40)))
 def point(x,y): return (a[0]+ux*x+nx*y,a[1]+uy*x+ny*y)
 new=pts[:i+1]
 wave_seed=feature.get('_junction_seed',0); wave_key=feature.get('_spacing_phase',0)
 wave_on=feature.get('stream_order',1)<3
 wave_base=(.042+.032*(H(wave_seed,'pin-lambda',feature['id'],wave_key)%10001)/10000)*MILE
 wave_phase=H(wave_seed,'pin-phase',feature['id'],wave_key,int(at_end))%len(_WAVE_FACTORS)
 wave_starts=_wave_spans(span,wave_base,wave_phase); wave_index=0
 mean_width=sum(feature['widths'])/len(feature['widths'])
 wave_slope=feature.get('_spacing_wave_scale',1.0)*min(.60+.10*(H(wave_seed,'pin-amplitude',feature['id'],wave_key)%10001)/10000,feature.get('_pin_wave_limit',19)*mean_width/(1.35*wave_base))
 wave_sign=1 if H(wave_seed,'pin-side',feature['id'],wave_key,int(at_end))%2 else -1
 for j in range(n+1):
  t=j/n; h00=2*t**3-3*t*t+1; h10=t**3-2*t*t+t; h01=-2*t**3+3*t*t; h11=t**3-t*t
  y=h00*cy+h10*span*slope+h01*ay+h11*span*ty/tx
  if wave_on:
   distance=span*t
   while wave_index<len(wave_starts)-2 and distance>=wave_starts[wave_index+1]: wave_index+=1
   wavelength=wave_starts[wave_index+1]-wave_starts[wave_index]
   phase=(distance-wave_starts[wave_index])/wavelength
   y+=wave_sign*_meander_env(distance,span)*wave_slope*wavelength*math.sin(2*math.pi*phase)
  new.append(point(cx+span*t,y))
 n=max(2,math.ceil(run/FT(40)))
 for j in range(1,n+1): new.append(point(ax+(chord-ax)*j/n,ay*(1-j/n)))
 new[0]=a; new[-1]=b
 if not at_end: new.reverse()
 # Keep the existing width-scaled amplitude constraint when a thin creek's
 # valley bow meets its pinned tangent. A longer blend lowers curvature.
 if feature.get('stream_order')==1:
  step=.02*MILE; samples=_arc_samples(new,step)
  lat=[(q[0]-a[0])*nx+(q[1]-a[1])*ny for q in samples]
  radius=max(1,round(.11*MILE/step))
  width=sum(feature['widths'])/len(feature['widths'])
  peak=max((abs(lat[i]-sum(lat[max(0,i-radius):min(len(lat),i+radius+1)])/len(lat[max(0,i-radius):min(len(lat),i+radius+1)])) for i in range(int(len(lat)*.3),max(int(len(lat)*.3)+1,int(len(lat)*.7)))),default=0)
  if len(samples)>=8 and peak>19*width:
   if feature.get('_approach_blend',.5)<1.5:
    feature['_approach_blend']=feature.get('_approach_blend',.5)+.2
   elif feature.get('_pin_wave_limit',19)>10:
    feature['_pin_wave_limit']=feature.get('_pin_wave_limit',19)-.5
   else:
    _resample_widths(feature,new); return
   _pin_approach_graph(feature,tangent if at_end else (-tangent[0],-tangent[1]),at_end,run)
   return
 _resample_widths(feature,new)

def _aim_end(feature,tangent,at_end,approach=None,obstacles=()):
 """Replace one end so the last 0.14 mi runs straight along `tangent`."""
 if approach is not None:
  _pin_approach_graph(feature,tangent,at_end,approach); return
 pts=feature['points']
 if len(pts)<3: return
 length=sum(math.dist(p,q) for p,q in zip(pts,pts[1:]))
 run=min(.14*MILE,length*.24) if approach is None else min(approach,length*.49)
 fillet=min(.12*MILE,length*.2) if approach is None else min(.22*MILE,length*.25)
 if run<FT(60) or fillet<FT(40): return
 if at_end:
  end=pts[-1]; idx,cut=_cut_from_end(pts,run+fillet)
  anchor=(end[0]-tangent[0]*run, end[1]-tangent[1]*run)
  span=max(FT(1),math.dist(cut,anchor))
  prev=pts[max(0,idx-1)]
  p0,p1=cut,anchor
  m0,m1=_scale_dir(cut[0]-prev[0],cut[1]-prev[1],span),_scale_dir(tangent[0],tangent[1],span)
  n=max(2,math.ceil(run/FT(80)))
  straight=[(anchor[0]+(end[0]-anchor[0])*i/n, anchor[1]+(end[1]-anchor[1])*i/n) for i in range(1,n+1)]
  straight[-1]=end
  prefix,suffix=list(pts[:idx]),straight
 else:
  start=pts[0]; idx,cut=_cut_from_start(pts,run+fillet)
  anchor=(start[0]+tangent[0]*run, start[1]+tangent[1]*run)
  span=max(FT(1),math.dist(anchor,cut))
  nxt=pts[min(len(pts)-1,idx)]
  p0,p1=anchor,cut
  m0,m1=_scale_dir(tangent[0],tangent[1],span),_scale_dir(nxt[0]-cut[0],nxt[1]-cut[1],span)
  n=max(2,math.ceil(run/FT(80)))
  straight=[(start[0]+(anchor[0]-start[0])*i/n, start[1]+(anchor[1]-start[1])*i/n) for i in range(n)]
  prefix,suffix=straight,list(pts[idx:])
 # A short arc-length cut can sit behind its straight-run anchor. Full-length
 # Hermite handles can then make a tiny loop, after untangling has finished.
 # Shorten only those handles; both endpoint tangents and the 0.1-mi straight
 # continuation sample remain intact. No random draws or node moves occur.
 for scale in (1,.5,.25,.125,0):
  herm=_hermite(p0,(m0[0]*scale,m0[1]*scale),p1,(m1[0]*scale,m1[1]*scale),max(2,math.ceil(span/FT(80))))
  new=prefix+herm+suffix
  new[0]=pts[0]; new[-1]=pts[-1]
  bounds=box(new,feature['width']/2)
  blocked=_curve_collision(new,new)
  for other in obstacles:
   if blocked: break
   if other is feature or not intersects(bounds,other['bounds']): continue
   allowed=[]
   tolerance=max(feature['width'],other['width'])+FT(20)
   if feature.get('downstream')==other['id']: allowed.append((new[-1],tolerance))
   if other.get('downstream')==feature['id']: allowed.append((other['points'][-1],tolerance))
   if feature.get('downstream')==other.get('downstream') and math.dist(new[-1],other['points'][-1])<.01: allowed.append((new[-1],tolerance+FT(30)))
   blocked=_curve_collision(new,other['points'],allowed)
  if not blocked:
   if scale<1: feature['_continuation_fillet_scale']=min(feature.get('_continuation_fillet_scale',1),scale)
   _resample_widths(feature,new); return
 # Retain the already checked curve if every fillet is blocked.
 return

def _round_continuations(features):
 """Meet one-in, one-out joins on a shared tangent.

 Collinear splits already arrive along the chord. A lattice corner does not,
 so each side of the corner is rebuilt: a short fillet, then a straight run
 longer than the 0.1 mi kink sample. The incoming run sits 8 degrees off that
 tangent so a source reach is not collinear with its receiver.
 """
 by_end={}; by_start={}
 for f in features:
  if f.get('kind')!='rivers' or len(f.get('points',()))<2: continue
  by_end.setdefault(tuple(f['points'][-1]),[]).append(f)
  by_start.setdefault(tuple(f['points'][0]),[]).append(f)
 for point,incoming in by_end.items():
  outgoing=by_start.get(point,())
  if len(incoming)!=1 or len(outgoing)!=1: continue
  before,after=incoming[0],outgoing[0]
  if before is after or after.get('id')!=before.get('downstream'): continue
  a0,a1=before['points'][0],before['points'][-1]
  b0,b1=after['points'][0],after['points'][-1]
  dx,dy=a1[0]-a0[0],a1[1]-a0[1]; ex,ey=b1[0]-b0[0],b1[1]-b0[1]
  dn=math.hypot(dx,dy); en=math.hypot(ex,ey)
  if dn<1 or en<1: continue
  din=(dx/dn,dy/dn); dout=(ex/en,ey/en)
  # A reach shorter than the kink sample cannot change its own heading, so the
  # longer neighbour has to arrive along that short chord.
  if en<.15*MILE: bis=dout
  elif dn<.15*MILE: bis=din
  else:
   bx,by=din[0]+dout[0],din[1]+dout[1]; bn=math.hypot(bx,by)
   if bn<.25: bx,by=-din[1],din[0]; bn=1
   bis=(bx/bn,by/bn)
  side=1 if din[0]*dout[1]-din[1]*dout[0]>0 else -1
  kick=math.radians(8)*side; c=math.cos(kick); s=math.sin(kick)
  inn=(bis[0]*c-bis[1]*s, bis[0]*s+bis[1]*c)
  saved_before,saved_after=dict(before),dict(after)
  others=[f for f in features if f is not before and f is not after]
  for rotation in (0,5,-5,10,-10,20,-20,30,-30):
   before.update(saved_before); after.update(saved_after)
   theta=math.radians(rotation); rc,rs=math.cos(theta),math.sin(theta)
   trial_in=(inn[0]*rc-inn[1]*rs,inn[0]*rs+inn[1]*rc)
   trial_out=(bis[0]*rc-bis[1]*rs,bis[0]*rs+bis[1]*rc)
   planned_after=dict(after)
   if en>=.15*MILE: _aim_end(planned_after,trial_out,False)
   if dn>=.15*MILE: _aim_end(before,trial_in,True,obstacles=others+[planned_after])
   if en>=.15*MILE: _aim_end(after,trial_out,False,obstacles=others+[before])
   if _curve_collision(before['points'],after['points'],((point,max(before['width'],after['width'])+FT(20)),)): continue
   _,left=_cut_from_end(before['points'],.1*MILE)
   _,right=_cut_from_start(after['points'],.1*MILE)
   incoming_heading=math.atan2(point[1]-left[1],point[0]-left[0])
   outgoing_heading=math.atan2(right[1]-point[1],right[0]-point[0])
   turn=abs((incoming_heading-outgoing_heading+math.pi)%(2*math.pi)-math.pi)
   if turn<=math.radians(35): break
  else:
   before.update(saved_before); after.update(saved_after)


def _channel_pieces(seed,a,b,limit):
 """Split a long chord into pieces that each lose elevation.

 Equal cuts are kept when they already run downhill. A hollow under the
 chord can put the next piece's start below its end, so those reaches try
 other fractions. If no cut stays downhill inside the length band, the
 chord is left whole.
 """
 distance=math.dist(a,b)
 if distance<=limit or distance<=0: return [a,b]
 floor=.62*MILE
 ha=elevation(seed,*a); hb=elevation(seed,*b)
 count=max(2,math.ceil(distance/limit))
 def at(t):
  return (a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t)
 def accept(ts):
  pts=[a]+[at(t) for t in ts]+[b]
  prev,prev_h=a,ha
  for j,p in enumerate(pts[1:]):
   dist=math.dist(prev,p)
   if dist<floor or dist>limit: return None
   h=hb if j==len(pts)-2 else elevation(seed,*p)
   if h>prev_h+1e-7: return None
   prev,prev_h=p,h
  pts[0]=a; pts[-1]=b
  return pts
 if count==2:
  for t in (.5,.42,.58,.38,.62,.34,.66,.30,.70,.26,.74):
   d0=distance*t; d1=distance-d0
   if d0<floor or d1<floor or d0>limit or d1>limit: continue
   got=accept([t])
   if got: return got
  return [a,b]
 base=[i/count for i in range(1,count)]
 got=accept(base)
 if got: return got
 for shift in (-.08,.08,-.12,.12,-.04,.04):
  ts=[t+shift for t in base]
  if any(t<=0 or t>=1 for t in ts) or ts!=sorted(ts): continue
  gaps=[ts[0]]+[q-p for p,q in zip(ts,ts[1:])]+[1-ts[-1]]
  if any(distance*g<floor or distance*g>limit for g in gaps): continue
  got=accept(ts)
  if got: return got
 return [a,b]

def _valley_points(seed,ident,a,b,phase_id=None):
 """Smoothed seeded bow between two nodes, pinned at both ends.

 One wave per 1.25 miles keeps the displacement inside 0.10-0.25 of that
 wavelength, which is the whole edge when the edge itself is under 1.5 miles.
 """
 chord=math.dist(a,b)
 if chord<=1: return [a,b]
 waves=max(1,int(round(chord/(1.25*MILE))))
 key=ident if phase_id is None else (str(phase_id) if outlet else H(seed,'reach-shape',ident,str(phase_id)))
 frac=.19+.03*((H(seed,'valley-amp',key)%10001)/10000)
 amp=frac*chord/waves
 step=MILE/8
 n=max(waves*8,math.ceil(chord/step))
 ux=(b[0]-a[0])/chord; uy=(b[1]-a[1])/chord; nx,ny=-uy,ux
 h2=((H(seed,'valley-h2',key)%20001)/10000-1)
 h3=((H(seed,'valley-h3',key)%20001)/10000-1)
 pts=[]
 for i in range(n+1):
  t=i/n
  bow=math.sin(2*math.pi*waves*t)+.12*h2*math.sin(4*math.pi*waves*t)+.08*h3*math.sin(6*math.pi*waves*t)
  pts.append((a[0]+ux*chord*t+nx*amp*bow,a[1]+uy*chord*t+ny*amp*bow))
 for _pass in range(2):
  nxt=[a]
  for i in range(1,n):
   p,q,r=pts[i-1],pts[i],pts[i+1]
   nxt.append(((p[0]+2*q[0]+r[0])/4,(p[1]+2*q[1]+r[1])/4))
  nxt.append(b)
  pts=nxt
 # Smoothing shrinks the bow. Scale the lateral offset back up to `amp`.
 peak=0
 for i in range(1,n):
  t=i/n
  base=(a[0]+ux*chord*t,a[1]+uy*chord*t)
  peak=max(peak,abs((pts[i][0]-base[0])*nx+(pts[i][1]-base[1])*ny))
 if peak>1:
  gain=amp/peak
  scaled=[a]
  for i in range(1,n):
   t=i/n
   base=(a[0]+ux*chord*t,a[1]+uy*chord*t)
   lat=(pts[i][0]-base[0])*nx+(pts[i][1]-base[1])*ny
   scaled.append((base[0]+nx*lat*gain,base[1]+ny*lat*gain))
  scaled.append(b)
  pts=scaled
 pts[0]=a; pts[-1]=b
 return pts

def _meander_points(seed,ident,a,b,width,phase_id=None):
 # Four pinned midpoint-displacement rounds seed each reach's individual
 # shape. Langbein-style direction oscillation adds width-scaled meanders.
 pts=[a,b]
 for level in range(4):
  out=[pts[0]]
  for i,(p,q) in enumerate(zip(pts,pts[1:])):
   dx=q[0]-p[0]; dy=q[1]-p[1]; length=math.hypot(dx,dy)
   key=H(seed,'edge',ident,level,i)
   offset=((key%20001)/10000-1)*.0003*length
   out.extend([((p[0]+q[0])/2-dy/length*offset,(p[1]+q[1])/2+dx/length*offset),q])
  pts=out
 length=sum(math.dist(p,q) for p,q in zip(pts,pts[1:])); chord=math.dist(a,b)
 if length<=0 or chord<=0: return [a,b]
 k=1.6+(H(seed,'meander-k',ident)%9001)/10000
 lam=10.9*max(width,FT(8)); waves=max(1,round(length/(k*lam))); period=length/waves
 omega=min(math.radians(110),2.2*math.sqrt((k-1)/k)); step=min(FT(200),period/4)
 # The display samples are no farther apart than the meander half-wave. Use
 # finer internal integration so phase does not alias against the samples and
 # inflate the endpoint pinning scale on individual reaches.
 n=max(2,math.ceil(length/step)); ux=(b[0]-a[0])/chord; uy=(b[1]-a[1])/chord
 nx,ny=-uy,ux; phase=0.0 if phase_id=='outlet' else (H(seed,'meander-phase',ident if phase_id is None else phase_id)%100000/100000*2*math.pi)+(H(seed,'meander-offset',ident)%100000/100000*2*math.pi)
 walked=[a]; x=y=0; walks=[]
 for j in range(waves+1):
  walk=(walks[-1] if walks else 0)+((H(seed,'meander-jitter',ident,j)%10001)/10000-.5)*.004
  walks.append(max(-.04,min(.04,walk)))
 for i in range(1,n+1):
  end_s=length*i/n; start_s=length*(i-1)/n; ds=end_s-start_s
  pieces=max(1,math.ceil(ds/(period/8))); fine=ds/pieces
  for j in range(pieces):
   s=start_s+(j+.5)*fine
   walk=walks[min(waves,round(s/length*waves))]
   transition=.6*MILE if phase_id=='outlet' else min(.15*MILE,length/4)
   taper=min(1.0,s/transition,(length-s)/transition)
   phi=taper*(omega*math.sin(2*math.pi*waves*s/length+phase)+walk)
   x+=fine*math.cos(phi); y+=fine*math.sin(phi)
  walked.append((x,y))
 end=walked[-1]; scale=chord/max(1,math.hypot(*end)); rotation=math.atan2(b[1]-a[1],b[0]-a[0])-math.atan2(end[1],end[0])
 c=math.cos(rotation); sn=math.sin(rotation); output=[]
 for i,(x,y) in enumerate(walked):
  px=a[0]+scale*(x*c-y*sn); py=a[1]+scale*(x*sn+y*c)
  t=i/n; base=(a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t)
  # Retain a small trace of the recursively displaced seed curve after the
  # larger width-scaled meander is pinned to both endpoints.
  ref=min(len(pts)-1,round(t*(len(pts)-1)))
  disp=(pts[ref][0]-base[0],pts[ref][1]-base[1])
  output.append((px+.05*disp[0],py+.05*disp[1]))
 output[0]=a; output[-1]=b
 return output

def _valley_frame(valley,t):
 n=len(valley)-1
 pos=min(n,max(0.0,t*n))
 i=min(n-1,int(pos)); f=pos-i
 p,q=valley[i],valley[i+1]
 base=(p[0]+f*(q[0]-p[0]),p[1]+f*(q[1]-p[1]))
 dx,dy=q[0]-p[0],q[1]-p[1]; norm=math.hypot(dx,dy) or 1
 return base,(-dy/norm,dx/norm)

def _fade_end(s,length):
 """Valley fade: flat against the chord near each end, full through the middle."""
 d=min(s,length-s)
 zero=min(.135*MILE,.16*length)
 full=min(.26*MILE,.34*length)
 if full<zero+.03*MILE: full=min(length*.45,zero+.03*MILE)
 if d>=full: return 1.0
 if d<=zero: return 0.0
 u=(d-zero)/(full-zero)
 return u*u*(3-2*u)

def _meander_env(s,length):
 """Short end taper for the width-scaled wave.

 A sine that grows over the whole reach stretches short waves and long waves
 toward one length. This ramp is only the last few hundred feet, and it is
 flat at both ends of that ramp so the join stays on the chord.
 """
 d=min(s,length-s)
 zero=min(.04*MILE,.12*length)
 full=min(.09*MILE,.28*length)
 if full<zero+.01*MILE: full=min(length*.45,zero+.01*MILE)
 if d>=full: return 1.0
 if d<=zero: return 0.0
 u=(d-zero)/(full-zero)
 return u*u*(3-2*u)

# Adjacent factors differ by enough that any two neighbouring waves have a
# half-wave length CV above 0.25, and the upper median factor stays near 1.
_WAVE_FACTORS=(.42,1.05,.48,1.15,.54,1.25,.60,1.35)

def _wave_spans(length,base,phase):
 starts=[0.0]; s=0.0; i=0
 while s<length and i<20000:
  s+=base*_WAVE_FACTORS[(phase+i)%len(_WAVE_FACTORS)]; starts.append(s); i+=1
 if len(starts)<2: starts.append(max(length,1.0))
 return starts

def _is_outlet(phase_id):
 return phase_id=='outlet' or (type(phase_id) is tuple and phase_id[:1]==('outlet',))

def _edge_curve(seed,ident,a,b,width,phase_id=None,order=1,meander_scale=1.0,valley_scale=1.0):
 """Seeded bend drawn as a graph over the chord, pinned at both ends.

 Lateral offset is a single-valued function of distance along the chord, so a
 reach cannot fold back across itself. The meander tapers to the chord in
 the last few hundred feet, and the valley fade is flat there too. Split
 pieces therefore arrive and leave along the chord instead of turning back
 into the piece they just left. Trunks of order 3 and up keep a mild bow and
 a width-scaled wave. An outlet stays on that wave alone: a valley bow on the
 outlet moves the relief and changes which headwaters drain downhill. Creeks
 keep a deeper arch and a shorter, uneven wave.
 """
 chord=math.dist(a,b)
 if chord<=1: return [a,b]
 outlet=_is_outlet(phase_id)
 trunk=order>=3 or outlet
 key=ident if phase_id is None else (str(phase_id) if outlet else H(seed,'reach-shape',ident,str(phase_id)))
 width=max(width,FT(8))
 phase=H(seed,'a2-wave',key)%len(_WAVE_FACTORS)
 if trunk:
  base=(11.6 if outlet else 10.5)*width
  amp_frac=.060+.042*((H(seed,'a2-amp',key)%10001)/10000)
  # Outlet relief is the distance to this centreline. A mile-scale bow
  # lifts the downstream node above the upstream one.
  valley_amp=0.0 if outlet else (.11+.04*((H(seed,'a2-valley',key)%10001)/10000))*min(chord,2*MILE)
  bows=max(1,int(round(chord/(2*MILE))))
  slope=amp_frac
  # A partial train on a wide trunk is one bend the sampler splits into
  # similar halves. Wait until several width-scaled waves fit.
  if not outlet and chord<4.5*base: slope=0.0
 else:
  base=(.042+.032*((H(seed,'a2-lam',key)%10001)/10000))*MILE
  amp_frac=.30+.28*((H(seed,'a2-amp',key)%10001)/10000)
  valley_amp=(.328+.18*((H(seed,'a2-valley',key)%10001)/10000))*chord
  bows=1
  slope=min(amp_frac,(10*width)/(1.35*base))
 starts=_wave_spans(chord,base,phase)
 sign=1 if H(seed,'a2-side',key)%2==0 else -1
 h2=(H(seed,'a2-h2',key)%20001)/10000-1
 ux=(b[0]-a[0])/chord; uy=(b[1]-a[1])/chord; nx,ny=-uy,ux
 step=FT(80) if trunk else FT(40)
 n=max(2,math.ceil(chord/step))
 pts=[]; k=0
 for i in range(n+1):
  s=chord*i/n; t=s/chord
  while k<len(starts)-2 and s>=starts[k+1]: k+=1
  # A full sine on a short trunk, faded at both ends, breaks into four
  # similar bends. One arch stays under that count and still leaves the chord.
  if trunk and slope>0: bow=math.sin(2*math.pi*bows*t)+.10*h2*math.sin(4*math.pi*bows*t)
  else: bow=math.sin(math.pi*t)*(1+.22*h2*math.sin(2*math.pi*t))
  wave=max(1e-6,starts[k+1]-starts[k])
  u=min(1.0,max(0.0,(s-starts[k])/wave))
  lat=valley_scale*_fade_end(s,chord)*sign*valley_amp*bow+meander_scale*_meander_env(s,chord)*slope*wave*math.sin(2*math.pi*u)
  pts.append((a[0]+ux*s+nx*lat,a[1]+uy*s+ny*lat))
 pts[0]=a; pts[-1]=b
 return pts

def _diagonal_allowed(seed,gx,gy,nx,ny):
 if abs(nx-gx)!=1 or abs(ny-gy)!=1: return True
 lo_x=min(gx,nx); lo_y=min(gy,ny)
 rising=(nx-gx)==(ny-gy)
 return (H(seed,'diagonal',lo_x,lo_y)%2==0)==rising

@lru_cache(maxsize=256)
def basin(seed,cx,cy,options):
 """Generate one rotated Voronoi basin as a deterministic headward tree."""
 outlets={(x,y):_outlet(seed,x,y) for x in range(cx-1,cx+2) for y in range(cy-1,cy+2)}
 candidates={}; polygon=_basin_polygon(seed,cx,cy); angle=_basin_angle(seed)
 outlet=_outlet(seed,cx,cy); routes=_route_targets(seed,cx,cy)
 barriers=tuple((rid,points,box(points)) for rid,points in (_route_curve(seed,x,y) for x in range(cx-1,cx+2) for y in range(cy-1,cy+2)))
 distance_barriers=tuple((rid,tuple(points[i] for i in range(0,len(points),8))+(points[-1],)) for rid,points,_ in barriers)
 # Clip to the exact local Voronoi envelope before visiting lattice nodes.
 lo_x=math.floor(min(p[0] for p in polygon)/NODE_STEP)-1; hi_x=math.ceil(max(p[0] for p in polygon)/NODE_STEP)+1
 lo_y=math.floor(min(p[1] for p in polygon)/NODE_STEP)-1; hi_y=math.ceil(max(p[1] for p in polygon)/NODE_STEP)+1
 for gx in range(lo_x,hi_x+1):
  for gy in range(lo_y,hi_y+1):
   p=_node_point(seed,gx,gy)
   basin_point=_rot(*p,angle)
   if not _point_in_polygon(basin_point,polygon): continue
   if min((_distance_to_segment(*basin_point,a,b) for a,b in zip(polygon,polygon[1:]+polygon[:1])),default=math.inf)<.75*NODE_STEP: continue
   # Keep headwater nodes away from neighboring trunk reaches. Nodes may still
   # drain to their own outlet reach, which is the intended basin confluence.
   if any(rid!=f'river:outlet:{cx}:{cy}' and min((_distance_to_segment(*p,a,b) for a,b in zip(points,points[1:])),default=math.inf)<.75*NODE_STEP for rid,points in distance_barriers): continue
   candidates[(gx,gy)]=p
 potential={key:_network_potential(seed,p,routes) for key,p in candidates.items()}
 # Station can fall while distance-to-outlet rises. Relief then lifts the
 # downstream node, so a step has to lose elevation and not only potential.
 height={key:elevation(seed,*p) for key,p in candidates.items()}
 downstream={}; owned_edges=[]; growth_type={}
 for key in candidates:
  draw=H(seed,'headward-event',cx,cy,key[0],key[1])%1000
  if draw<200: growth_type[key]='continue'
  elif draw<900: growth_type[key]='symmetric'
  else: growth_type[key]='asymmetric'
 for key in sorted(candidates,key=lambda k:potential[k],reverse=True):
  gx,gy=key; p=candidates[key]; own=potential[key]; options2=[]
  for dx in (-1,0,1):
   for dy in (-1,0,1):
    if dx==dy==0: continue
    other=(gx+dx,gy+dy)
    if other not in candidates or not _diagonal_allowed(seed,gx,gy,*other): continue
    q=candidates[other]
    op=potential[other]
    if op<own and height[other]<=height[key]+1e-7: options2.append((op,H(seed,'path',gx,gy,gx+dx,gy+dy),other))
  target=None
  for _,_,other in sorted(options2):
   q=candidates[other]
   if _crosses_owned_route(p,q,barriers): continue
   crosses=False
   for a,b,src,dst in owned_edges:
    hit=crossing_point(p,q,a,b)
    if hit is not None and hit[0] not in (p,q,a,b): crosses=True; break
   if not crosses: target=other; break
  # A local Voronoi edge can end at a small sink where all eight immediate
  # lower neighbors are blocked by an already-owned channel. Search only a
  # bounded 2.2-edge neighborhood for a lower, clear receiver before falling
  # back to a long outlet connector across the basin.
  if target is None:
   distant=[]
   for other,q in candidates.items():
    if other==key or potential[other]>=own or height[other]>height[key]+1e-7: continue
    distance=math.dist(p,q)
    if distance>2.2*NODE_STEP: continue
    distant.append((potential[other],H(seed,'path-fallback',gx,gy,other[0],other[1]),distance,other))
   for _,_,_,other in sorted(distant):
    q=candidates[other]
    if _crosses_owned_route(p,q,barriers): continue
    crosses=False
    for a,b,src,dst in owned_edges:
     hit=crossing_point(p,q,a,b)
     if hit is not None and hit[0] not in (p,q,a,b): crosses=True; break
    if not crosses: target=other; break
  downstream[key]=target
  if target is not None:
   owned_edges.append((p,candidates[target],key,target))
 # Source-to-outlet paths form a directed acyclic graph because each local edge
 # strictly lowers its potential. Accumulation and Strahler order are computed
 # headward-first, then converted into channel widths.
 incoming={k:[] for k in candidates}
 for k,v in downstream.items():
  if v is not None: incoming[v].append(k)
 order={}; area={}; widths={}
 for key in sorted(candidates,key=lambda k:potential[k],reverse=True):
  ins=incoming[key]
  orders=sorted((order[v] for v in ins),reverse=True)
  order[key]=(orders[0]+1 if len(orders)>1 and orders[0]==orders[1] else (orders[0] if orders else 1))
  area[key]=NODE_STEP/MILE+sum(area[v] for v in ins)
  wref=FT(10.5)*math.sqrt(max(.01,area[key]))
  if order[key]==1: wref=min(FT(15),wref)
  iw=sorted((widths[v] for v in ins),reverse=True)
  widths[key]=max(iw[0] if iw else 0,min(FT(150),wref))
 # Width constraint at every actual confluence, applied downstream of its two
 # largest tributaries. The total upstream length supplies the area term.
 for key in sorted(candidates,key=lambda k:potential[k],reverse=True):
  iw=sorted((widths[v] for v in incoming[key]),reverse=True)
  if len(iw)>=2:
   widths[key]=min(max(widths[key],iw[0]),1.05*math.sqrt(iw[0]**2+iw[1]**2))
 out=[]
 # Keep each headward edge as its own reach. At this grid spacing an edge is
 # about 1.4 mi long, which preserves the intended growth scale and lets local
 # collision repair work without flattening a whole many-mile chain.
 edge_ids={key:f"river:{cx}:{cy}:edge:{key[0]}:{key[1]}" for key in candidates}
 route_points_by_id={rid:points for rid,points,_ in barriers}
 def connector_clear(a,b):
  if _crosses_owned_route(a,b,barriers): return False
  for c,d,_,_ in owned_edges:
   hit=crossing_point(a,b,c,d)
   if hit is None: continue
   _,t,u,_=hit
   if t<=1e-6 or t>=1-1e-6: continue
   return False
  return True
 for key in sorted(candidates):
  ident=edge_ids[key]; p=candidates[key]; target=downstream[key]
  if target in candidates:
   receiver=edge_ids[target]; end=candidates[target]
   next_node=downstream[target]
   if next_node in candidates:
    qnext=candidates[next_node]
   else:
    _,rid,_,rq=_nearest_route(end,routes,barriers)
    route_points=route_points_by_id[rid]
    qi=min(range(len(route_points)),key=lambda i:math.dist(route_points[i],rq))
    qnext=route_points[min(qi+1,len(route_points)-1)]
   end_width=widths[key] if len(incoming[target])>1 else widths[target]
  else:
   _,receiver,_,end=_nearest_route(p,routes,barriers)
   route_points=route_points_by_id[receiver]
   qi=min(range(len(route_points)),key=lambda i:math.dist(route_points[i],end))
   if not connector_clear(p,end):
    chosen=None
    for radius in (8,32,128):
     options3=[]
     for delta in range(-radius,radius+1):
      j=qi+delta
      if 0<=j<len(route_points) and connector_clear(p,route_points[j]):
       options3.append((abs(delta),H(seed,'route-contact',cx,cy,key[0],key[1],j),j))
     if options3:
      _,_,qi=min(options3); end=route_points[qi]; chosen=qi; break
    if chosen is None:
     # If every nearby route vertex is obstructed, terminate at the first
     # route contact on the candidate chord and keep its segment very short.
     for j,(a,b) in enumerate(zip(route_points,route_points[1:])):
      hit=crossing_point(p,end,a,b)
      if hit is not None and 1e-6<hit[1]<1-1e-6:
       qi=j if math.dist(hit[0],a)<=math.dist(hit[0],b) else j+1
       end=route_points[qi]; break
   qnext=route_points[min(qi+1,len(route_points)-1)]
   end_width=widths[key]
   distance=math.dist(p,end)
   if distance>NODE_STEP*1.05:
    count=math.ceil(distance/NODE_STEP)
    line=[(p[0]+(end[0]-p[0])*i/count,p[1]+(end[1]-p[1])*i/count) for i in range(count+1)]
    ids=[ident]+[f'river:{cx}:{cy}:link:{key[0]}:{key[1]}:{i}' for i in range(1,count)]
    for i,(a,b) in enumerate(zip(line,line[1:])):
     next_id=ids[i+1] if i+1<count else receiver
     c=line[i+2] if i+2<len(line) else qnext
     direction=(c[0]-b[0],c[1]-b[1])
     side=1 if direction[0]*(a[1]-b[1])-direction[1]*(a[0]-b[0])>=0 else -1
     pts=_edge_curve(seed,ids[i],a,b,widths[key],(receiver,side),order[key])
     profile=[widths[key]]*len(pts)
     out.append(feature('rivers',ids[i],points=pts,width=widths[key],widths=profile,
      stream_order=order[key],flow='regional',source=(i==0 and not incoming[key]),downstream=next_id,
      basin=(cx,cy),area=area[key],growth_events=[growth_type[key]]))
     owned_edges.append((a,b,key,None))
    continue
  v=(qnext[0]-end[0],qnext[1]-end[1])
  side=1 if v[0]*(p[1]-end[1])-v[1]*(p[0]-end[0])>=0 else -1
  pieces=_channel_pieces(seed,p,end,1.45*MILE)
  count=len(pieces)-1
  for i in range(count):
   pa,pb=pieces[i],pieces[i+1]
   piece_id=ident if i==0 else f'{ident}:part:{i}'
   next_id=f'{ident}:part:{i+1}' if i+1<count else receiver
   w0=widths[key]+(end_width-widths[key])*i/count
   w1=widths[key]+(end_width-widths[key])*(i+1)/count
   pts=_edge_curve(seed,piece_id,pa,pb,(w0+w1)/2,(receiver,side,i),order[key])
   span=max(1,len(pts)-1)
   profile=[w0+(w1-w0)*k/span for k in range(span+1)]
   out.append(feature('rivers',piece_id,points=pts,width=max(profile,default=w0),widths=profile,
    stream_order=order[key],flow='regional',source=(i==0 and not incoming[key]),downstream=next_id,
    basin=(cx,cy),area=area[key],growth_events=[growth_type[key]]))
 out.extend(_route_geometry(seed,x,y) for x in range(cx-1,cx+2) for y in range(cy-1,cy+2))
 route_features=[f for f in out if f['id'].startswith('river:outlet:')]
 for route in route_features:
  groups={}
  for reach in out:
   if reach is route or reach.get('downstream')!=route['id']: continue
   groups.setdefault(tuple(reach['points'][-1]),[]).append(reach)
  profile=route['widths']
  for point,group in groups.items():
   idx=min(range(len(route['points'])),key=lambda i:math.dist(route['points'][i],point))
   if len(group)<1: continue
   incoming=[reach.get('widths',[reach['width']])[-1] for reach in group]
   incoming.append(profile[max(0,idx-1)])
   upper_values=sorted(incoming,reverse=True)
   if len(upper_values)<2: continue
   lower=max(incoming); upper=1.05*math.sqrt(upper_values[0]**2+upper_values[1]**2)
   if profile[idx]<lower:
    profile[idx]=min(lower,upper)
    for j in range(idx+1,len(profile)): profile[j]=max(profile[j],profile[idx])
  route['width']=max(profile,default=route['width'])
 route_ids={f['id'] for f in route_features}; route_order={}
 for route in sorted(route_features,key=lambda f:_terrain_potential(seed,*_outlet(seed,*map(int,f['id'].split(':')[-2:])),),reverse=True):
  profile=route['widths']; upstream_routes=[g for g in route_features if g.get('downstream')==route['id'] and g['id'] in route_order]
  if upstream_routes:
   upstream=sorted((g.get('widths',[g['width']])[-1] for g in upstream_routes),reverse=True)
   target=max(profile[0],upstream[0])
   if len(upstream)>=2: target=min(target,1.05*math.sqrt(upstream[0]**2+upstream[1]**2))
   for i in range(len(profile)): profile[i]=max(profile[i],target)
   route['width']=max(profile)
  joins=[]
  for g in route_features:
   if g['id'] in route_order and g.get('downstream')==route['id']: joins.append((0,g['id'],route_order[g['id']]))
  for g in out:
   if g['id'].startswith('river:outlet:') or g.get('downstream')!=route['id']: continue
   j=min(range(len(route['points'])),key=lambda i:math.dist(route['points'][i],g['points'][-1]))
   joins.append((j,g['id'],g['stream_order']))
  order=3
  for _,group in itertools.groupby(sorted(joins),key=lambda item:item[0]):
   incoming=[item[2] for item in group]; highest=max(incoming)
   if highest>order: order=highest
   elif highest==order: order+=1
  route_order[route['id']]=order; route['stream_order']=order
 from river_relocation import relocate_nodes
 relocate_nodes(seed,cx,cy,out)
 for repair in range(4):
  _angle_reaches(seed,out)
  _untangle_reaches(seed,out)
 _round_continuations(out)
 return out

def _distance_to_segment(x,y,a,b):
 dx,dy=b[0]-a[0],b[1]-a[1]
 if dx==0 and dy==0: return math.hypot(x-a[0],y-a[1])
 t=max(0,min(1,((x-a[0])*dx+(y-a[1])*dy)/(dx*dx+dy*dy)))
 return math.hypot(x-a[0]-t*dx,y-a[1]-t*dy)

def elevation(seed,x,y):
 options=_drainage_options(); rx,ry=_rot(x,y,_basin_angle(seed))
 lane=math.floor(rx/BASIN_TICKS); row=math.floor(ry/BASIN_TICKS)
 cx,cy=_cell_owner(seed,x,y,lane,row); routes=_route_targets(seed,cx,cy)
 distance,_,_,_=_nearest_route((x,y),routes)
 relief=option('world.relief_ft')*(1-math.exp(-distance/(MILE*.8)))**2
 return _network_potential(seed,(x,y),routes)+relief

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
 options=_drainage_options(); angle=_basin_angle(seed)
 corners=[_rot(x,y,angle) for x,y in ((rect[0],rect[1]),(rect[0]+rect[2],rect[1]),(rect[0]+rect[2],rect[1]+rect[3]),(rect[0],rect[1]+rect[3]))]
 minx,maxx=min(p[0] for p in corners),max(p[0] for p in corners); miny,maxy=min(p[1] for p in corners),max(p[1] for p in corners)
 seen=set()
 for cx in range(math.floor((minx-1.5*BASIN_TICKS)/BASIN_TICKS),math.ceil((maxx+1.5*BASIN_TICKS)/BASIN_TICKS)+1):
  for cy in range(math.floor((miny-1.5*BASIN_TICKS)/BASIN_TICKS),math.ceil((maxy+1.5*BASIN_TICKS)/BASIN_TICKS)+1):
   poly=_basin_polygon(seed,cx,cy)
   if not poly or max(p[0] for p in poly)<minx or min(p[0] for p in poly)>maxx or max(p[1] for p in poly)<miny or min(p[1] for p in poly)>maxy: continue
   for f in basin(seed,cx,cy,options):
    if f['id'] not in seen and intersects(f['bounds'],rect):
     seen.add(f['id']); yield f

def share_farm_lanes(seed,features,sites,water_hits=lambda r:False,ox=0,oy=0):
 # A few adjacent yards share a single road entrance and a short front connector.
 used=set(); by_id={f['id']:f for f in features}; lane=FT(option('rural.lane_width_ft')); depth=FT(60)
 for a in sorted(sites,key=lambda s:s['id']):
  if a['id'] in used: continue
  for b in sorted(sites,key=lambda s:s['id']):
   if a['id']>=b['id'] or b['id'] in used or a['road']!=b['road']: continue
   pa,qa=a['access'][:2]; pb,qb=b['access'][:2]; span=math.dist(pa,pb)
   if not FT(100)<span<FT(600): continue
   if H(seed,'shared-lane',a['id'],b['id'])%100>=25: continue
   if min(math.dist(pa,qa),math.dist(pb,qb))<depth: continue
   if max(a['length'],b['length'])+span/2>FT(830): continue
   ua=((qa[0]-pa[0])/math.dist(pa,qa),(qa[1]-pa[1])/math.dist(pa,qa))
   ub=((qb[0]-pb[0])/math.dist(pb,qb),(qb[1]-pb[1])/math.dist(pb,qb))
   if ua!=ub: continue
   c=((pa[0]+pb[0])/2,(pa[1]+pb[1])/2); d=(c[0]+depth*ua[0],c[1]+depth*ua[1])
   ea=(pa[0]+depth*ua[0],pa[1]+depth*ua[1]); eb=(pb[0]+depth*ua[0],pb[1]+depth*ua[1])
   bounds=box([c,d,ea,eb],lane)
   if water_hits((bounds[0]-ox,bounds[1]-oy,bounds[2],bounds[3])): continue
   id='shared-lane:'+a['id']+'|'+b['id']
   features.append(feature('driveways',id,points=[c,d,ea,eb],width=lane,members=[a['id'],b['id']]))
   for site in (a,b):
    entry=by_id[site['entry_id']]; x,y,w,h=entry['rect']
    if ua[0]>0: x+=depth; w-=depth
    elif ua[0]<0: w-=depth
    elif ua[1]>0: y+=depth; h-=depth
    else: h-=depth
    entry['rect']=entry['bounds']=(x,y,w,h)
    if site['id'] in by_id:
     entry_point=ea if site is a else eb
     by_id[site['id']].update(shared_lane=id,lane_length_ft=(site['length']+span/2)/FT(1),access=[c,d,entry_point]+site['access'][1:])
    used.add(site['id'])
   break

def feature_points(f):
 if 'points' in f: return f['points']
 x,y,w,h=f['rect']
 return [(x,y+h/2),(x+w,y+h/2)] if f.get('axis')=='h' else [(x+w/2,y),(x+w/2,y+h)]

def crossing_point(a,b,c,d):
 u=(b[0]-a[0],b[1]-a[1]); v=(d[0]-c[0],d[1]-c[1]); det=u[0]*v[1]-u[1]*v[0]
 if abs(det)<1e-8:
  q=(c[0]-a[0],c[1]-a[1]); scale=max(1,math.hypot(*u)*math.hypot(*q))
  if abs(q[0]*u[1]-q[1]*u[0])>1e-10*scale: return None
  length=u[0]**2+u[1]**2
  if not length: return (a,0,0,0) if a==c or a==d else None
  t0=(q[0]*u[0]+q[1]*u[1])/length; qd=(d[0]-a[0],d[1]-a[1]); t1=(qd[0]*u[0]+qd[1]*u[1])/length
  lo=max(0,min(t0,t1)); hi=min(1,max(t0,t1))
  if hi<lo: return None
  point=(a[0]+lo*u[0],a[1]+lo*u[1]); vlength=v[0]**2+v[1]**2
  s=0 if not vlength else max(0,min(1,((point[0]-c[0])*v[0]+(point[1]-c[1])*v[1])/vlength))
  return point,lo,s,0
 q=(c[0]-a[0],c[1]-a[1]); t=(q[0]*v[1]-q[1]*v[0])/det; s=(q[0]*u[1]-q[1]*u[0])/det
 if not (0<=t<=1 and 0<=s<=1): return None
 return (a[0]+t*u[0],a[1]+t*u[1]),t,s,abs(det)/(math.hypot(*u)*math.hypot(*v))

def bridges(features):
 # Every carrier/water-segment pair owns its deck, independent of query order.
 cells={}; segments={}
 def keys(a,b):
  for ix in range(math.floor(min(a[0],b[0])/MILE),math.floor(max(a[0],b[0])/MILE)+1):
   for iy in range(math.floor(min(a[1],b[1])/MILE),math.floor(max(a[1],b[1])/MILE)+1): yield ix,iy
 for stream in features:
  if stream['kind']!='rivers': continue
  for i,(a,b) in enumerate(zip(stream['points'],stream['points'][1:])):
   id=stream['id']+':segment:'+str(i); segments[id]=(stream,i,a,b)
   for key in keys(a,b): cells.setdefault(key,set()).add(id)
 decks={}
 for road in features:
  if road['kind'] not in ('roads','rail'): continue
  pts=feature_points(road)
  for a,b in zip(pts,pts[1:]):
   candidates=set().union(*(cells.get(k,set()) for k in keys(a,b)))
   for water_id in sorted(candidates):
    stream,i,c,d=segments[water_id]; hit=crossing_point(a,b,c,d)
    if hit is None: continue
    p,t,s,sine=hit
    if sine<1e-6: continue
    length=math.dist(a,b)
    widths=stream.get('widths'); water_width=(widths[i]*(1-s)+widths[i+1]*s) if widths else stream['width']
    half=(water_width/2+FT(20))/max(sine,.01)
    ux,uy=(b[0]-a[0])/length,(b[1]-a[1])/length
    points=[(p[0]-half*ux,p[1]-half*uy),(p[0]+half*ux,p[1]+half*uy)]
    id='bridge:'+road['id']+'|'+water_id
    decks[id]=feature('bridges',id,points=points,width=road.get('width',option('region.row')),owner_id=road['id'],river_segment_id=water_id,carrier=road['kind'],surface=road.get('surface','rail'),crossing=p)
 # Gravel always bridges in this version: no water closure can orphan frontage.
 return list(decks.values())

def window(seed,rect,settings=None,detail=True):
 settings=settings or Settings()
 if len(rect)!=4 or any(not math.isfinite(v) for v in rect) or rect[2]<=0 or rect[3]<=0: raise ValueError('Window requires finite x,y and positive width,height')
 with applied(settings):
  out=(list(terrain(seed,rect)) if detail else [])+list(rivers(seed,rect))+list(paved_routes(seed,rect))
  # Historical section roads are global edges, independent of query or settlement tiles.
  for axis in ('h','v'):
   c0,c1=(rect[1],rect[1]+rect[3]) if axis=='h' else (rect[0],rect[0]+rect[2])
   s0,s1=(rect[0],rect[0]+rect[2]) if axis=='h' else (rect[1],rect[1]+rect[3])
   for line in range(math.floor(c0/MILE)-1,math.ceil(c1/MILE)+1):
    for segment in range(math.floor(s0/MILE)-1,math.ceil(s1/MILE)+1):
     surface=road_type(seed,axis,line,segment)
     if surface is None: continue
     c,lo=line*MILE,segment*MILE
     if axis=='v': c+=correction_shift(seed,math.floor(segment/24))
     length=MILE
     if surface=='paved' and axis=='v':
      # E-W survey roads stay continuous through correction parallels.
      # Rectangular survey sections share the continuous route, but do not
      # draw straight spurs through its rounded correction corners.
      trim=.18*MILE
      if segment%24==0: lo+=trim; length-=trim
      if segment%24==23: length-=trim
     r=(lo,c-option('region.row')/2,length,option('region.row')) if axis=='h' else (c-option('region.row')/2,lo,option('region.row'),length)
     out.append(feature('roads',f'road:{axis}:{line}:{segment}',rect=r,surface=surface,history=['wagon trail','upgraded','paved'] if surface=='paved' else ['survey grid','dirt',surface],axis=axis))
  if option('stages.rail'):
   for axis in ('v','h'):
    lo,hi=(rect[1],rect[1]+rect[3]) if axis=='v' else (rect[0],rect[0]+rect[2])
    for lane,c,_ in rail_lanes(seed,rect,axis):
     for row in range(math.floor(lo/MILE)-1,math.ceil(hi/MILE)+1):
      out.append(feature('rail',f'rail:{axis}:{lane}:{row}',points=rail_points(seed,lane,c,row,axis),width=option('region.rail_row'),axis=axis,route_id=f'rail:{axis}:{lane}',history=['drainage','survey roads','rail']))
  extent=option('world.tile_miles')*MILE; halo=option('world.halo_tiles') if detail else 0; serialized=json.dumps(document(settings),sort_keys=True)
  urban=settlements(seed,rect,serialized,detail)
  envelopes=[r for f in urban if f['kind']=='towns' for r in [f['bounds']]+f.get('reserved',[])]; countryside=[]
  for tx in range(math.floor(rect[0]/extent)-halo,math.floor((rect[0]+rect[2])/extent)+halo+1):
   for ty in range(math.floor(rect[1]/extent)-halo,math.floor((rect[1]+rect[3])/extent)+halo+1):
    tile=_tile(seed,tx,ty,serialized,detail)
    rejected=[f['id'] for f in tile if f['kind'] in ('lots','farms') and any(intersects(f['bounds'],e) for e in envelopes)]
    countryside.extend(f for f in tile if not any(f['id']==id or f['id'].startswith(id+':') for id in rejected))
  out.extend(countryside); out.extend(urban)
  out.extend(bridges(out))
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

COLORS={'contours':'#b5ba9b','rivers':'#509bc6','creeks':'#67a9cc','rail':'#685553','roads':'#b9a17a','buildings':'#d6dfc4','driveways':'#c8bea9','trees':'#3c6946','towns':'none','lots':'none','farms':'#8b7655','bridges':'#525954'}
# One style contract for SVG/browser rasterization and the world PNG exporter.
WORLD_STYLE={
 'background':'#f2f1e6','colors':COLORS,
 'roads':{'paved':'#525954','gravel':'#b9a17a','dirt':'#9a784c'},
 'minimum_px':{'paved':1.2,'gravel':.65,'dirt':.65,'rail':1.2,'river':1.5,'tributary':.8,'towns':.8},
 'gravel_hide_above_mi':48,'outline':'#687060','parcel_outline':'#b5c0ab','outline_px':.3,
 'order':['contours','rivers','creeks','towns','lots','roads','rail','bridges','driveways','buildings','trees','farms']}

def feature_style(f):
 kind=f['kind']; color=COLORS[kind]; minimum=0; hide=0
 if kind in ('roads','bridges'):
  surface=f.get('surface','paved'); color=COLORS['rail'] if surface=='rail' else WORLD_STYLE['roads'][surface]
  minimum=WORLD_STYLE['minimum_px'][surface]
  if surface in ('gravel','dirt'): hide=WORLD_STYLE['gravel_hide_above_mi']
 elif kind=='rivers': minimum=WORLD_STYLE['minimum_px']['tributary' if f.get('stream_order',3)<3 else 'river']
 elif kind in ('rail','towns'): minimum=WORLD_STYLE['minimum_px'][kind]
 return dict(color=color,minimum_px=minimum,hide_above_mi=hide)

def svg(scene,local=False):
 if local:
  # Chromium/Skia clips large absolute coordinates even with a distant viewBox.
  # Rebase geometry itself, rather than applying a large cancelling transform.
  x,y,w,h=scene["window"]; scene=copy.deepcopy(scene); scene["window"]=[0,0,w,h]
  for f in scene["features"]:
   if "rect" in f:
    a,b,c,d=f["rect"]; f["rect"]=[a-x,b-y,c,d]
   for key in ("points","shape","outline"):
    if key in f: f[key]=[(a-x,b-y) for a,b in f[key]]
 from html import escape
 x,y,w,h=scene['window']
 out=[f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{x} {y} {w} {h}"><rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{WORLD_STYLE["background"]}"/>']
 order=WORLD_STYLE['order']
 for kind in order:
  for f in scene['features']:
   if f['kind']!=kind: continue
   style=feature_style(f); color=style['color']
   title=escape(f['id']+' '+str(f.get('use','')))
   out.append(f'<g data-kind="{kind}" data-minimum-px="{style["minimum_px"]}" data-hide-above-mi="{style["hide_above_mi"]}"><title>'+title+'</title>')
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
    width=f.get('width',FT(1)) if kind in ('rivers','creeks') else FT(option('world.rail_draw_ft')) if kind=='rail' or (kind=='bridges' and f.get('surface')=='rail') else f.get('width',FT(1))
    if 'widths' in f:
     for a,b,wa,wb in zip(f['points'],f['points'][1:],f['widths'],f['widths'][1:]):
      out.append(f'<polyline points="{a[0]},{a[1]} {b[0]},{b[1]}" fill="none" stroke="{color}" stroke-width="{(wa+wb)/2}" stroke-linecap="round"/>')
    else:
     cap='round' if kind in ('rivers','creeks') else 'butt'
     out.append(f'<polyline points="{points}" fill="none" stroke="{color}" stroke-width="{width}" stroke-linecap="{cap}"/>')
   out.append('</g>')
 out.append('</svg>'); return ''.join(out).encode()
