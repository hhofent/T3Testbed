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
  town=reg['town']; add('towns','town',town['box'],size=town['size'],rail_town=on_rail,site_id=id,anchor=(x,y),route_axis=axis)
  reserved=[p['rect'] for p in reg['parcels'] if p['kind'] in ('elevator','industry')]+([reg['depot']] if reg['depot'] else [])
  out[-1]['reserved']=[]
  for a,b,c,d in reserved:
   p,q=absolute(a,b),absolute(a+c,b+d)
   out[-1]['reserved'].append((min(p[0],q[0]),min(p[1],q[1]),abs(q[0]-p[0]),abs(q[1]-p[1])))
  for road in reg['roads']:
   if road in reg['grid_roads']: continue
   add('roads','street:'+road['id'],road_rect(road),surface='paved',axis=road['axis'] if axis=='v' else ('h' if road['axis']=='v' else 'v'),history=['town plat','paved'])
  # The main-street frontage becomes paved only inside the town envelope.
  bx,by,bw,bh=town['box']
  for a in ('h','v'):
   r=(bx,MILE-option('region.row')/2,bw,option('region.row')) if a=='h' else (MILE-option('region.row')/2,by,option('region.row'),bh)
   add('roads','street:main-'+a,r,surface='paved',axis=a if axis=='v' else ('h' if a=='v' else 'v'),history=['town main street'])
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
     from trees import local_markers
     for i,p in enumerate(local_markers(plan)):
      a,b=to_world(pl,spec['width'],spec['depth'],*p); add('trees',lid+':tree:'+str(i),(a,b,0,0))
  if detail:
   for shop in town['commercial']:
    if shop.get('bldg'): add('buildings','business:'+shop['id'],shop['bldg'],use=shop['type'])
   for i,parcel in enumerate(reg['parcels']):
    if parcel['kind'] in ('elevator','industry'): add('buildings','industry:'+str(i),parcel['rect'],use=parcel['kind'])
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
 pts=_edge_curve(seed,ident,start,end,FT(100),'outlet')
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

def _angle_reaches(seed,features):
 by_id={f['id']:f for f in features}; incoming={}
 for f in features:
  if f.get('downstream') in by_id:
   key=(f['downstream'],tuple(f['points'][-1])); incoming.setdefault(key,[]).append(f)
 for f in features:
  if f['id'].startswith('river:outlet:') or len(f['points'])<3: continue
  receiver=by_id.get(f.get('downstream'))
  if receiver is None: continue
  join=f['points'][-1]
  group=incoming.get((receiver['id'],tuple(join)),())
  if len(group)<2 and f.get('stream_order',3)>=receiver.get('stream_order',3): continue
  idx=min(range(len(receiver['points'])),key=lambda i:math.dist(receiver['points'][i],join))
  if math.dist(receiver['points'][idx],join)>.01: continue
  if idx+1<len(receiver['points']): a,b=join,receiver['points'][idx+1]
  else: continue
  vx,vy=b[0]-a[0],b[1]-a[1]; length=min(math.dist(join,f['points'][-2]),FT(30))
  if not length or not math.hypot(vx,vy): continue
  uneven=f.get('stream_order')!=receiver.get('stream_order')
  normal=sum((H(seed,'junction-normal',f['id'],receiver['id'],i)%100000)/100000 for i in range(12))-6
  angle_deg=65+30*normal
  angle_deg+=.25*((90 if uneven else 58)-angle_deg)
  angle=math.radians(max(25,min(110,angle_deg)))
  old=(join[0]-f['points'][-2][0],join[1]-f['points'][-2][1])
  side=1 if vx*old[1]-vy*old[0]>=0 else -1
  direction=math.atan2(vy,vx)+side*angle
  f['points'][-2]=(join[0]-length*math.cos(direction),join[1]-length*math.sin(direction))

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
 from collections import defaultdict
 channels=sorted((f for f in features if not f['id'].startswith('river:outlet:')),key=lambda f:f['id'])
 cell=FT(200)
 buckets=defaultdict(set); segments={}
 def cells(a,b):
  return ((ix,iy) for ix in range(math.floor(min(a[0],b[0])/cell),math.floor(max(a[0],b[0])/cell)+1)
   for iy in range(math.floor(min(a[1],b[1])/cell),math.floor(max(a[1],b[1])/cell)+1))
 def add_segments(f):
  for j,(a,b) in enumerate(zip(f['points'],f['points'][1:])):
   key=(f['id'],j); segments[key]=(f,a,b)
   for cell_key in cells(a,b): buckets[cell_key].add(key)
 def remove_segments(f):
  for j,(a,b) in enumerate(zip(f['points'],f['points'][1:])):
   key=(f['id'],j); segments.pop(key,None)
   for cell_key in cells(a,b):
    bucket=buckets.get(cell_key)
    if bucket is not None:
     bucket.discard(key)
     if not bucket: buckets.pop(cell_key,None)
 for other in features: add_segments(other)
 def collision(f,buckets,segments):
  for i,(a,b) in enumerate(zip(f['points'],f['points'][1:])):
   possible=set()
   for cell_key in cells(a,b): possible.update(buckets.get(cell_key,()))
   for key in possible:
    other,c,d=segments[key]
    if other is f: continue
    if max(a[0],b[0])<min(c[0],d[0]) or max(c[0],d[0])<min(a[0],b[0]) or max(a[1],b[1])<min(c[1],d[1]) or max(c[1],d[1])<min(a[1],b[1]): continue
    hit=crossing_point(a,b,c,d)
    if hit is None: continue
    point,t,u,_=hit
    if (t<1e-7 or t>1-1e-7) and (u<1e-7 or u>1-1e-7): continue
    if f.get('downstream')==other['id'] and math.dist(point,f['points'][-1])<=max(f['width'],other['width'])+FT(20): continue
    if other.get('downstream')==f['id'] and math.dist(point,other['points'][-1])<=max(f['width'],other['width'])+FT(20): continue
    return True
  return False
 for f in channels:
  original_widths=list(f['widths'])
  remove_segments(f)
  end=f['points'][-1]; previous=f['points'][-2]; length=math.dist(previous,end)
  direction=math.atan2(end[1]-previous[1],end[0]-previous[0])
  collides=False
  # Rephase around local junction pockets before falling back to a straight
  # chord; straight fallbacks are topologically safe but create copied
  # tributary silhouettes in otherwise varied networks.
  for attempt in range(24):
   collides=collision(f,buckets,segments)
   if not collides: break
   group=(f['downstream'],'phase',attempt+1)
   pts=_edge_curve(seed,f['id'],f['points'][0],end,f['width'],group)
   pts[-2]=(end[0]-length*math.cos(direction),end[1]-length*math.sin(direction))
   f['points']=pts
   f['widths']=[original_widths[min(len(original_widths)-1,round(i/(len(pts)-1)*(len(original_widths)-1)))] for i in range(len(pts))]
   f['bounds']=box(pts,max(f['widths'])/2)
  if collides: collides=collision(f,buckets,segments)
  if collides:
   # A straight chord fixes the crossing and then fails the valley check.
   # Keep a seeded bend, and only accept a sampled straight line if every
   # gentler bend still crosses another channel.
   start=f['points'][0]; found=False
   for phase in range(12):
    pts=_edge_curve(seed,f['id'],start,end,f['width'],('fallback',phase))
    f['points']=pts
    f['widths']=[original_widths[min(len(original_widths)-1,round(i/(len(pts)-1)*(len(original_widths)-1)))] for i in range(len(pts))]
    f['bounds']=box(pts,max(f['widths'])/2)
    if not collision(f,buckets,segments): found=True; break
   if not found:
    n=max(2,math.ceil(math.dist(start,end)/FT(200)))
    f['points']=[(start[0]+(end[0]-start[0])*i/n,start[1]+(end[1]-start[1])*i/n) for i in range(n+1)]
    f['widths']=[original_widths[min(len(original_widths)-1,round(i/(len(f['points'])-1)*(len(original_widths)-1)))] for i in range(len(f['points']))]
    f['bounds']=box(f['points'],max(f['widths'])/2)
  add_segments(f)

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
 key=ident if phase_id is None else phase_id
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

def _edge_curve(seed,ident,a,b,width,phase_id=None):
 """Valley centreline plus the width-scaled meander, both pinned at a and b.

 Samples follow the meander spacing, at least one vertex every 200 ft, so a
 mile of channel keeps more than six points after the valley bow is applied.
 Outlet centreline stays on the meander alone. A valley bow on the trunk
 moves the relief surface and changes which headwaters drain downhill.
 """
 if phase_id=='outlet': return _meander_points(seed,ident,a,b,width,phase_id)
 chord=math.dist(a,b)
 if chord<=1: return [a,b]
 valley=_valley_points(seed,ident,a,b,phase_id)
 meander=_meander_points(seed,ident,a,b,width,phase_id)
 # Reach-to-reach amplitude spread. A shared valley scale pulls raw
 # sinuosity into a band tighter than the finished-river check allows.
 mix=.70+.70*((H(seed,'meander-mix',ident if phase_id is None else phase_id)%10001)/10000)
 ux=(b[0]-a[0])/chord; uy=(b[1]-a[1])/chord; nx,ny=-uy,ux
 output=[a]
 for p in meander[1:-1]:
  along=(p[0]-a[0])*ux+(p[1]-a[1])*uy
  lat=((p[0]-a[0])*nx+(p[1]-a[1])*ny)*mix
  base,normal=_valley_frame(valley,min(1,max(0,along/chord)))
  output.append((base[0]+normal[0]*lat,base[1]+normal[1]*lat))
 output.append(b)
 return output

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
    if op<own: options2.append((op,H(seed,'path',gx,gy,gx+dx,gy+dy),other))
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
    if other==key or potential[other]>=own: continue
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
     pts=_edge_curve(seed,ids[i],a,b,widths[key],(receiver,side))
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
   pts=_edge_curve(seed,piece_id,pa,pb,(w0+w1)/2,(receiver,side,i))
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
 _angle_reaches(seed,out)
 _untangle_reaches(seed,out)
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
   for key in ("points","shape"):
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
