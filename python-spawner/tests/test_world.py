import json
from settings import Settings
from world import window,intersects,MILE

def projection(scene,bounds):
 return {f['id']:f for f in scene['features'] if intersects(f['bounds'],bounds)}

def test_world_overlaps_negative_coordinates_and_request_order():
 settings=Settings({'stages.houses':False})
 a=(-MILE,-MILE,2*MILE,2*MILE); b=(0,-MILE,2*MILE,2*MILE); overlap=(0,-MILE,MILE,2*MILE)
 first=window(13,a,settings); second=window(13,b,settings)
 assert projection(first,overlap)==projection(second,overlap)
 assert json.dumps(first,sort_keys=True)==json.dumps(window(13,a,settings),sort_keys=True)
 assert window(14,a,settings)!=first
 # Cached feature data must not be writable through a query result.
 first['features'][0]['id']='mutated'
 assert window(13,a,settings)['features'][0]['id']!='mutated'

def test_rivers_flow_south_and_merge_into_wider_trunks():
 scene=window(13,(-MILE,0,3*MILE,9*MILE),Settings({'stages.houses':False}))
 streams=[f for f in scene['features'] if f['kind']=='rivers']
 assert streams
 assert all(f['points'][1][1]>f['points'][0][1] for f in streams)
 assert all(f['downstream'].startswith('river:') for f in streams)


def test_branch_widths_grow_from_headwaters_and_bends_have_independent_ids():
 from world import rivers
 from settings import applied
 with applied(Settings()):
  streams=list(rivers(31,(-40*MILE,-40*MILE,80*MILE,80*MILE)))
  branches=[f for f in streams if f['source']]
  assert len(branches)>10
  profiles=[]
  for branch in branches:
   widths=branch['widths']
   assert len(widths)==len(branch['points'])
   assert all(0<a<b for a,b in zip(widths,widths[1:]))
   assert widths[0]<widths[-1]/4
   assert widths[-1]==branch['width']
   # Normalize translation and scale; region-owned curves must still differ.
   points=branch['points']; start,end=points[0],points[-1]
   profiles.append(tuple(round((p[0]-end[0])/(start[0]-end[0]),6) for p in points[1:-1]))
  assert len(set(profiles))==len(profiles)


def test_drainage_continuity_crossings_and_downhill():
 from verification import river_checks
 from settings import applied
 for seed in (13,31,46):
  with applied(Settings()):
   checks=river_checks(seed,(-10*MILE,-10*MILE,40*MILE,40*MILE))
   assert all(c['pass'] for c in checks.values()),checks


def test_drainage_topology_accepts_owned_interior_confluences():
 from verification import drainage_topology_checks
 main=dict(id='main',points=[(0,0),(0,1),(0,2)],source=True,downstream='tail')
 tail=dict(id='tail',points=[(0,2),(0,3)],source=False,downstream='outside')
 branch=dict(id='branch',points=[(1,0),(0,1)],source=True,downstream='main')
 # The halo contains the tail, whose own receiver is beyond the query.
 checks=drainage_topology_checks([main,tail,branch],[main,branch])
 assert all(c['pass'] for c in checks.values()),checks
 branch['downstream']='tail'
 assert not drainage_topology_checks([main,tail,branch],[branch])['river_continuity']['pass']
 branch['downstream']='missing'
 assert not drainage_topology_checks([main,tail,branch],[branch])['river_continuity']['pass']


def test_drainage_topology_rejects_cycles_even_with_connected_endpoints():
 from verification import drainage_topology_checks
 a=dict(id='a',points=[(0,0),(1,1)],source=False,downstream='b')
 b=dict(id='b',points=[(1,1),(0,0)],source=False,downstream='a')
 checks=drainage_topology_checks([a,b])
 assert checks['river_continuity']['pass']
 assert not checks['river_acyclic']['pass']
 # Long chains must not depend on Python's recursion depth.
 chain=[dict(id=str(i),points=[(0,i),(0,i+1)],source=i==0,downstream=str(i+1)) for i in range(2000)]
 assert drainage_topology_checks(chain,chain[:1])['river_acyclic']['pass']


def test_rural_variety_reproducible_and_within_lots():
 from rural import layout,TYPES
 from region import make_region,spawn_region
 from settings import applied
 with applied(Settings()):
  reg=make_region(31); plans=spawn_region(reg); layouts=[]
  for lot in reg['lots']:
   spec=lot['spec']; plan=plans.get(spec['id'],{}).get('plan')
   if spec['kind']=='town' or not plan: continue
   site=layout(31,spec,plan)
   assert site==layout(31,spec,plan)
   assert all(0<=x and 0<=y and x+w<=spec['width'] and y+h<=spec['depth'] for _,(x,y,w,h) in site['buildings'])
   layouts.append(site)
  assert len({tuple(n for n,_ in s['buildings']) for s in layouts})>3
  assert len({s['offset'] for s in layouts})>3
  assert len({tuple(s['trees']) for s in layouts})>3


def test_diagonal_and_parallel_rail_has_square_road_crossings():
 from settings import applied
 from verification import crossing_angles
 with applied(Settings({'world.rail_enabled_pct':100,'world.rail_diagonal_pct':100})):
  scene=window(31,(-MILE,-MILE,6*MILE,12*MILE),Settings({'world.rail_enabled_pct':100,'world.rail_diagonal_pct':100}),detail=False)
  rails=[f for f in scene['features'] if f['kind']=='rail']
  assert {f['axis'] for f in rails}=={'h','v'}
  assert any(a[0]==b[0] for f in rails for a,b in zip(f['points'],f['points'][1:]))
  angles=crossing_angles(scene['features'])
  assert angles and min(angles)>=45
  assert sum(a>=75 for a in angles)/len(angles)>=.8


def test_missing_roads_both_axes_one_percent_and_cycle_detours():
 from settings import applied
 from world import road_type
 with applied(Settings()):
  missing={axis:[] for axis in ('h','v')}
  for axis in missing:
   for line in range(-100,100):
    for segment in range(-100,100):
     if road_type(31,axis,line,segment) is None: missing[axis].append((line,segment))
   assert .006<len(missing[axis])/(40000*8/9)<.013
   from world import paved_edge
   assert all(not paved_edge(31,axis,line,segment) for line,segment in missing[axis])
  for axis,edges in missing.items():
   for line,segment in edges:
    bx,by=(segment//2,line//2) if axis=='h' else (line//2,segment//2)
    cycle=[('h',2*by,2*bx),('h',2*by+1,2*bx),('v',2*bx,2*by),('v',2*bx+1,2*by)]
    assert sum(road_type(31,*edge) is None for edge in cycle)==1


def test_world_has_no_independent_legacy_creek_segments():
 scene=window(31,[0,0,3*MILE,3*MILE],Settings(),detail=False)
 assert not any(f['kind']=='creeks' for f in scene['features'])


def test_empty_water_blocked_town_does_not_poison_prefetch():
 # Found through the real 20-mile browser flow: an entirely rejected town plat
 # in a halo tile must become countryside, rather than failing every request.
 scene=window(896138,[21*MILE,-3*MILE,3*MILE,3*MILE],Settings(),detail=False)
 assert scene['features']


def test_paved_roads_never_missing_even_at_maximum_loss():
 from settings import applied
 from world import paved_routes,paved_edge,road_type
 with applied(Settings({'world.missing_pct':10})):
  routes=list(paved_routes(31,(-30*MILE,-30*MILE,70*MILE,70*MILE)))
  assert routes
  for axis in ('h','v'):
   for line in range(-30,31):
    for segment in range(-30,31):
     if paved_edge(31,axis,line,segment): assert road_type(31,axis,line,segment)=='paved'


def test_paved_routes_continuous_over_seventy_miles_with_curved_jogs():
 import math
 from world import paved_routes
 from verification import paved_road_gaps
 from settings import applied
 rect=(-30*MILE,-30*MILE,70*MILE,70*MILE)
 with applied(Settings()):
  routes=list(paved_routes(31,rect)); groups={}
  for route in routes: groups.setdefault(route['route_id'],[]).append(route)
  for group in groups.values():
   group.sort(key=lambda f:int(f['id'].split(':')[-1]))
   assert all(a['points'][-1]==b['points'][0] for a,b in zip(group,group[1:]))
   assert sum(sum(math.dist(a,b) for a,b in zip(f['points'],f['points'][1:])) for f in group)>70*MILE
   assert all(len(f['points'])>10 for f in group)
  assert not paved_road_gaps(routes,rect)
  removed=next(f for f in routes if f['id']=='paved:v:0:0')
  assert removed['id'] in paved_road_gaps([f for f in routes if f is not removed],rect)


def test_rail_approaches_are_smooth_and_crossings_square():
 import math
 from world import rail_points
 from settings import applied
 with applied(Settings({'world.rail_diagonal_pct':100,'world.rail_bend_miles':.5})):
  points=rail_points(31,0,0,5)
  headings=[math.atan2(b[0]-a[0],b[1]-a[1]) for a,b in zip(points,points[1:])]
  assert max(abs(h) for h in headings)<math.radians(30)
  assert abs(headings[0])<.01 and abs(headings[-1])<.01
  assert points[-1]==rail_points(31,0,0,6)[0]


def test_long_winding_tributaries_and_tight_trunks_across_chunks():
 import math
 from settings import applied
 from world import rivers,drainage_y,drainage_omega
 with applied(Settings()):
  streams=list(rivers(31,(-20*MILE,-20*MILE,40*MILE,40*MILE)))
  branches=[f for f in streams if f['source']]
  assert all(sum(math.dist(a,b) for a,b in zip(f['points'],f['points'][1:]))>6*MILE for f in branches)
  trunk=[f for f in streams if f['id'].startswith('river:0:')]
  assert len(trunk)>200
  # Oxbow-like necks compress longitudinal spacing by roughly tenfold.
  omega=drainage_omega(); step=.001*MILE
  grades=[(drainage_y(31,i*step+step)-drainage_y(31,i*step))/step for i in range(5000)]
  assert 0<min(grades)<.11
  by_id={f['id']:f for f in streams}
  for f in trunk:
   if f['downstream'] in by_id: assert f['points'][-1]==by_id[f['downstream']]['points'][0]


def test_towns_remain_spaced_at_default_world_settings():
 import math
 scene=window(31,(-18*MILE,-18*MILE,36*MILE,36*MILE),Settings(),detail=False)
 centers=[(f['rect'][0]+f['rect'][2]/2,f['rect'][1]+f['rect'][3]/2) for f in scene['features'] if f['kind']=='towns']
 assert 3<=len(centers)<=12
 nearest=[min(math.dist(a,b)/MILE for b in centers if b!=a) for a in centers]
 assert min(nearest)>=3 and max(nearest)<=25


def test_river_bends_vary_between_lanes_and_global_intervals():
 from world import drainage_x,rivers
 from settings import applied
 with applied(Settings()):
  profiles=[]
  for lane in (-1,0,1):
   for start in (-24,0,24):
    profiles.append(tuple(round(drainage_x(31,(start+i)*MILE,lane)/MILE,5) for i in range(8)))
  assert len(set(profiles))==len(profiles)
  a={f['id']:f for f in rivers(31,(-10*MILE,-3*MILE,20*MILE,6*MILE))}
  b={f['id']:f for f in rivers(31,(-10*MILE,0,20*MILE,6*MILE))}
  assert a.keys()&b.keys()
  assert all(a[k]==b[k] for k in a.keys()&b.keys())
  joins=[tuple(f['points'][-1]) for f in a.values() if f['source']]
  assert len(joins)==len(set(joins))


def test_basin_hierarchy_widens_at_owned_confluences_and_varies_shapes():
 import math,statistics
 from world import rivers,elevation
 from settings import applied
 with applied(Settings()):
  streams=list(rivers(31,(-40*MILE,-40*MILE,80*MILE,80*MILE)))
  by_id={f['id']:f for f in streams}
  assert {f['stream_order'] for f in streams}=={1,2,3}
  branches=[f for f in streams if f['source']]
  ratios=[]; profiles=[]
  for f in branches:
   receiver=by_id.get(f['downstream'])
   if receiver is None: continue
   assert f['points'][-1] in receiver['points']
   assert f['width']<receiver['width']
   assert f['stream_order']<receiver['stream_order']
   # Confluences point downstream, with a genuine angled approach.
   a,b=f['points'][-2:]; j=receiver['points'].index(b)
   c=receiver['points'][min(j+1,len(receiver['points'])-1)]
   if c!=b:
    u=(b[0]-a[0],b[1]-a[1]); v=(c[0]-b[0],c[1]-b[1])
    assert u[1]>0 and v[1]>0
    assert abs(u[0]*v[1]-u[1]*v[0])>1
   for x,y in f['points'][::8]:
    assert abs(elevation(31,x,y)+y/MILE*4)<1e-7
   p=f['points']; length=sum(math.dist(a,b) for a,b in zip(p,p[1:]))
   ratios.append(length/math.dist(p[0],p[-1]))
   profiles.append(tuple(round((x-p[0][0])/MILE,6) for x,y in p))
  assert 1.6<=statistics.median(ratios)<=2.5
  assert len(set(profiles))==len(profiles)
  # Coarse-region trunk endpoints meet exactly, but the interiors never repeat.
  from world import basin,_drainage_options
  profiles=[]
  for row in range(-6,6):
   a=basin(31,0,row,_drainage_options())[:64]
   b=basin(31,0,row+1,_drainage_options())[0]
   assert a[-1]['points'][-1]==b['points'][0]
   profiles.append(tuple(round((f['points'][0][0]-a[0]['points'][0][0])/MILE,6) for f in a))
  assert len(set(profiles))==len(profiles)


def test_world_region_never_calls_legacy_creeks(monkeypatch):
 import region,world
 world._tile.cache_clear()
 def forbidden(*args): raise AssertionError('legacy per-tile water was called')
 monkeypatch.setattr(region,'make_creeks',forbidden)
 assert window(14,(0,0,MILE,MILE),Settings({'stages.houses':False}),detail=False)['features']


def test_correction_lines_shift_north_south_every_twenty_four_miles():
 from world import correction_shift,paved_points,road_type
 from settings import applied
 with applied(Settings()):
  shifts=[correction_shift(31,band) for band in range(-3,4)]
  assert all(a!=b for a,b in zip(shifts,shifts[1:]))
  assert shifts!=[correction_shift(14,band) for band in range(-3,4)]
  for band in range(-2,3):
   points=paved_points(31,'v',0,band)
   from world import route_offset
   assert points[0][0]==(1+route_offset(31,'v',0,band))*MILE+correction_shift(31,band)
   assert points[-1]==paved_points(31,'v',0,band+1)[0]
   assert any(y==(band+1)*24*MILE for x,y in points)
   # Correction parallels remain unbroken cross roads, including at max loss.
   with applied(Settings({'world.missing_pct':10})):
    assert all(road_type(31,'h',band*24,segment) is not None for segment in range(-30,30))
  north=window(31,(-MILE,23*MILE,4*MILE,2*MILE),Settings({'stages.houses':False}),detail=False)
  roads={f['id']:f for f in north['features'] if f['kind']=='roads'}
  a=roads['road:v:2:23']['rect']; b=roads['road:v:2:24']['rect']
  assert a[0]!=b[0] and a[1]+a[3]==b[1]==24*MILE
  # E-W survey sections on either side still meet, without correction offsets.
  for line in (23,24,25):
   a=roads[f'road:h:{line}:0']['rect']; b=roads[f'road:h:{line}:1']['rect']
   assert a[0]+a[2]==b[0] and a[1]==b[1]


def test_bridges_cover_every_paved_rail_and_gravel_water_crossing():
 from world import bridges,feature,feature_points,crossing_point
 from settings import applied
 with applied(Settings()):
  rect=(-5*MILE,-4*MILE,12*MILE,12*MILE)
  features=window(31,rect,Settings({'stages.houses':False}),detail=False)['features']
  decks={f['id']:f for f in features if f['kind']=='bridges'}
  assert decks
  rivers=[f for f in features if f['kind']=='rivers']
  checked=0
  for road in features:
   if road['kind'] not in ('roads','rail'): continue
   for stream in rivers:
    if not intersects(road['bounds'],stream['bounds']): continue
    points=feature_points(road)
    for a,b in zip(points,points[1:]):
     for i,(c,d) in enumerate(zip(stream['points'],stream['points'][1:])):
      hit=crossing_point(a,b,c,d)
      if hit is None: continue
      x,y=hit[0]
      if not rect[0]<x<rect[0]+rect[2] or not rect[1]<y<rect[1]+rect[3]: continue
      water_id=stream['id']+':segment:'+str(i)
      id='bridge:'+road['id']+'|'+water_id
      assert id in decks,(road['id'],water_id)
      assert decks[id]['owner_id']==road['id']
      checked+=1
  assert checked>20
  simple=[feature('rivers','water',points=[(0,-100),(0,100)],width=20),
          feature('roads','paved-road',points=[(-100,0),(100,0)],width=12,surface='paved'),
          feature('rail','train',points=[(-100,50),(100,50)],width=10)]
  first=bridges(simple); second=bridges(list(reversed(simple)))
  assert sorted(first,key=lambda f:f['id'])==sorted(second,key=lambda f:f['id'])
  assert len(first)==2


def test_rail_density_directions_curvature_and_corrected_crossing_angles():
 import math
 from world import rail_lanes,rail_points
 from verification import crossing_angles
 from settings import applied
 with applied(Settings()):
  rect=(-200*MILE,-200*MILE,400*MILE,400*MILE)
  miles=0; counts={}
  for axis in ('h','v'):
   lanes=[(k,c) for k,c,_ in rail_lanes(31,rect,axis) if -200*MILE<=c<200*MILE]
   counts[axis]=len(lanes)
   miles+=sum(sum(math.dist(a,b) for a,b in zip(rail_points(31,k,c,0,axis),rail_points(31,k,c,0,axis)[1:]))/MILE*400 for k,c in lanes)
  density=miles/(400*400)*400
  assert all(counts.values()) and 23<=density<=35,density
  for axis in ('h','v'):
   points=rail_points(31,0,MILE*1.25,4,axis)
   assert points[-1]==rail_points(31,0,MILE*1.25,5,axis)[0]
   assert len({round(p[0] if axis=='v' else p[1]) for p in points})>4
  for seed in (14,31,46):
   settings=Settings({'world.rail_enabled_pct':100,'stages.houses':False})
   scene=window(seed,(-3*MILE,22*MILE,8*MILE,5*MILE),settings,detail=False)
   angles=crossing_angles(scene['features'])
   assert angles and min(angles)>=45,(seed,min(angles))
   assert sum(a>=75 for a in angles)/len(angles)>=.8
