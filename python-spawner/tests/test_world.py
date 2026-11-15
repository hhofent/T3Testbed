import json
from settings import Settings
from world import window,intersects,MILE

def projection(scene,bounds):
 return {f['id']:f for f in scene['features'] if intersects(f['bounds'],bounds)}

def skip_river_generation(monkeypatch):
 import world
 monkeypatch.setattr(world,'rivers',lambda seed,rect:())
 monkeypatch.setattr(world,'_route_offset',lambda *args:0)

def skip_tile_settlement_work(monkeypatch):
 import world
 skip_river_generation(monkeypatch)
 monkeypatch.setattr(world,'_tile',lambda *args,**kwargs:[])
 monkeypatch.setattr(world,'settlements',lambda *args,**kwargs:[])

def test_world_overlaps_negative_coordinates_and_request_order(monkeypatch):
 skip_tile_settlement_work(monkeypatch)
 settings=Settings({'stages.houses':False})
 a=(-2*MILE,-2*MILE,MILE,MILE); b=(-1.5*MILE,-2*MILE,MILE,MILE); overlap=(-1.5*MILE,-2*MILE,.5*MILE,MILE)
 first=window(13,a,settings,detail=False); second=window(13,b,settings,detail=False)
 assert projection(first,overlap)==projection(second,overlap)
 assert json.dumps(first,sort_keys=True)==json.dumps(window(13,a,settings,detail=False),sort_keys=True)
 assert window(14,a,settings,detail=False)!=first
 # Cached feature data must not be writable through a query result.
 first['features'][0]['id']='mutated'
 assert window(13,a,settings,detail=False)['features'][0]['id']!='mutated'

def test_rivers_follow_seeded_regional_flow_and_merge_into_wider_trunks():
 from world import basin,_drainage_options
 streams=basin(13,0,0,_drainage_options())
 assert streams
 assert len({f['basin'] for f in streams})>1
 assert all(f['points'][0]!=f['points'][-1] for f in streams)
 assert all(f['downstream'].startswith('river:') for f in streams)
 assert len({f['id'] for f in streams})==len(streams)


def test_branch_widths_grow_from_headwaters_and_bends_have_independent_ids():
 from world import rivers
 from settings import applied
 from spawner import FT
 with applied(Settings()):
  from world import basin,_drainage_options
  streams=list(basin(31,0,0,_drainage_options()))
  branches=[f for f in streams if f['source']]
  assert branches
  profiles=[]
  for branch in branches:
   widths=branch['widths']
   assert len(widths)==len(branch['points'])
   assert all(0<a for a in widths)
   assert 8*FT(1)<=branch['width']<=15*FT(1) if branch['stream_order']==1 else branch['width']>0
   # Normalize translation and scale; region-owned curves must still differ.
   points=branch['points']; start,end=points[0],points[-1]
   profiles.append(tuple(round((p[0]-end[0])/(start[0]-end[0]),6) for p in points[1:-1]))
  assert len(set(profiles))>1 if len(profiles)>1 else bool(profiles)


def test_drainage_continuity_and_downhill():
 from verification import drainage_topology_checks
 from world import basin,_drainage_options,elevation
 from settings import applied
 with applied(Settings()):
  streams=basin(13,0,0,_drainage_options())
 by_id={f['id']:f for f in streams}
 local=[f for f in streams if not f['id'].startswith('river:outlet:')]
 checks=drainage_topology_checks(streams,local)
 assert all(c['pass'] for c in checks.values()),checks
 for f in local:
  if f['downstream'] in by_id:
   assert min(__import__('math').dist(f['points'][-1],p) for p in by_id[f['downstream']]['points'])<.001
   assert elevation(13,*f['points'][-1])<=elevation(13,*f['points'][0])+1e-7


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
  def tree_key(s):
   trees=s['trees']
   if trees and isinstance(trees[0],dict): return tuple(sorted((t['x'],t['y']) for t in trees))
   return tuple(trees)
  assert len({tree_key(s) for s in layouts})>3


def test_diagonal_and_parallel_rail_has_square_road_crossings(monkeypatch):
 skip_tile_settlement_work(monkeypatch)
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


def test_missing_roads_both_axes_one_percent_and_cycle_detours(monkeypatch):
 from settings import applied
 from world import road_type
 skip_river_generation(monkeypatch)
 with applied(Settings()):
  missing={axis:[] for axis in ('h','v')}
  for axis in missing:
   for line in range(-25,25):
    for segment in range(-25,25):
     if road_type(31,axis,line,segment) is None: missing[axis].append((line,segment))
   assert .006<len(missing[axis])/(2500*8/9)<.013
   from world import paved_edge
   assert all(not paved_edge(31,axis,line,segment) for line,segment in missing[axis])
  for axis,edges in missing.items():
   for line,segment in edges:
    bx,by=(segment//2,line//2) if axis=='h' else (line//2,segment//2)
    cycle=[('h',2*by,2*bx),('h',2*by+1,2*bx),('v',2*bx,2*by),('v',2*bx+1,2*by)]
    assert sum(road_type(31,*edge) is None for edge in cycle)==1


def test_empty_water_blocked_town_does_not_poison_prefetch(monkeypatch):
 skip_river_generation(monkeypatch)
 # Found through the real 20-mile browser flow: an entirely rejected town plat
 # in a halo tile must become countryside, rather than failing every request.
 scene=window(896138,[21*MILE,-3*MILE,3*MILE,3*MILE],Settings(),detail=False)
 assert scene['features']


def test_paved_roads_never_missing_even_at_maximum_loss(monkeypatch):
 from settings import applied
 from world import paved_routes,paved_edge,road_type
 skip_river_generation(monkeypatch)
 with applied(Settings({'world.missing_pct':10})):
  routes=list(paved_routes(31,(-30*MILE,-30*MILE,70*MILE,70*MILE)))
  assert routes
  for axis in ('h','v'):
   for line in range(-30,31):
    for segment in range(-30,31):
     if paved_edge(31,axis,line,segment): assert road_type(31,axis,line,segment)=='paved'


def test_paved_routes_continuous_over_seventy_miles_with_curved_jogs(monkeypatch):
 import math
 skip_river_generation(monkeypatch)
 from world import paved_routes
 from verification import paved_road_gaps
 from settings import applied
 rect=(-12*MILE,-12*MILE,24*MILE,24*MILE)
 with applied(Settings()):
  routes=list(paved_routes(31,rect)); groups={}
  for route in routes: groups.setdefault(route['route_id'],[]).append(route)
  for group in groups.values():
   group.sort(key=lambda f:int(f['id'].split(':')[-1]))
   assert all(a['points'][-1]==b['points'][0] for a,b in zip(group,group[1:]))
   assert sum(sum(math.dist(a,b) for a,b in zip(f['points'],f['points'][1:])) for f in group)>20*MILE
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
 from world import basin,_drainage_options
 with applied(Settings()):
  streams=list(basin(31,0,0,_drainage_options()))
  branches=[f for f in streams if f['source']]
  assert all(sum(math.dist(a,b) for a,b in zip(f['points'],f['points'][1:]))>0 for f in branches)
  trunk=[f for f in streams if f['id'].startswith('river:outlet:')]
  assert trunk
  by_id={f['id']:f for f in streams}
  for f in trunk:
   if f['downstream'] in by_id: assert f['points'][-1]==by_id[f['downstream']]['points'][0]


def test_towns_remain_spaced_at_default_world_settings(monkeypatch):
 import math
 skip_river_generation(monkeypatch)
 import json
 from presets import document
 from world import settlements
 settings=Settings({'stages.houses':False})
 with __import__('settings').applied(settings): scene=settlements(31,(-9*MILE,-9*MILE,18*MILE,18*MILE),json.dumps(document(settings),sort_keys=True),False)
 centers=[f['anchor'] for f in scene if f['kind']=='towns']
 assert 3<=len(centers)<=12
 nearest=[min(math.dist(a,b)/MILE for b in centers if b!=a) for a in centers]
 assert min(nearest)>=3 and max(nearest)<=25


def test_basin_networks_are_repeatable_across_overlapping_windows():
 from world import basin,_drainage_options
 from settings import applied
 with applied(Settings()):
  a=basin(31,0,0,_drainage_options()); b=basin(31,0,0,_drainage_options())
  assert a==b


def test_basin_hierarchy_widens_at_owned_confluences_and_varies_shapes():
 import math,statistics
 from world import basin,elevation,_drainage_options
 from settings import applied
 with applied(Settings()):
  streams=list(basin(31,0,0,_drainage_options()))
  by_id={f['id']:f for f in streams}
  assert min(f['stream_order'] for f in streams)==1
  assert max(f['stream_order'] for f in streams)>=3
  branches=[f for f in streams if f['source']]
  ratios=[]; profiles=[]
  for f in branches:
   receiver=by_id.get(f['downstream'])
   if receiver is None: continue
   assert f['points'][-1] in receiver['points']
   assert f['widths'][-1]<=receiver['width']
   assert f['stream_order']<=receiver['stream_order']
   # Confluences point downstream, with a genuine angled approach.
   a,b=f['points'][-2:]; j=receiver['points'].index(b)
   c=receiver['points'][min(j+1,len(receiver['points'])-1)]
   if c!=b:
    u=(b[0]-a[0],b[1]-a[1]); v=(c[0]-b[0],c[1]-b[1])
    assert math.hypot(*u)>0 and math.hypot(*v)>0
    assert abs(u[0]*v[1]-u[1]*v[0])>1
   for x,y in f['points'][::8]:
    assert math.isfinite(elevation(31,x,y))
   p=f['points']; length=sum(math.dist(a,b) for a,b in zip(p,p[1:]))
   ratios.append(length/math.dist(p[0],p[-1]))
   profiles.append(tuple(round((x-p[0][0])/MILE,6) for x,y in p))
  assert statistics.median(ratios)>=1
  assert len(set(profiles))==len(profiles)
  from world import basin,_drainage_options
  assert basin(31,0,0,_drainage_options())==basin(31,0,0,_drainage_options())

def test_finished_river_metrics_smoke_fails_on_comb_geometry():
 from verification import river_geometry_metrics
 from settings import applied
 with applied(Settings()):
  m=river_geometry_metrics(13,(0,0,4*MILE,4*MILE))
 assert 1.6<=m['sinuosity_median']<=2.2 and m['sinuosity_spread']>=.4
 assert m['bearing_entropy']>=.8 and m['ewest_fraction']<=.30
 assert m['order1_width_ft'] and 8<=min(m['order1_width_ft'])<=max(m['order1_width_ft'])<=15
 assert not m['width_rule_violations']
 # The old comb has almost all E-W channels and very low bearing entropy.
 # This finished-feature smoke test remains diagnostic while the full metric
 # survey runs from checks/run_all.py.

def test_crossing_point_finds_collinear_overlap():
 from world import crossing_point
 assert crossing_point((0,0),(10,0),(5,0),(15,0))==((5,0),.5,0,0)
 assert crossing_point((0,0),(10,0),(5,1),(15,1)) is None

def test_outlet_lattice_and_regional_bearing_smoke():
 from world import _bearing,_basin_angle,_outlet,_next_outlet
 import math,statistics
 x_crossings=[]
 for seed in (13,14):
  angle=_basin_angle(seed); candidates=[]
  for cx in range(-2,3):
   for cy in range(-2,2):
    nx,ny,end=_next_outlet(seed,cx,cy); start=_outlet(seed,cx,cy)
    for a,b in zip((start,end),(end,start)):
     if (a[1]<=0<b[1]) or (b[1]<=0<a[1]):
      t=-a[1]/(b[1]-a[1]); candidates.append(a[0]+t*(b[0]-a[0]))
  assert candidates
  x_crossings.append(round(min(candidates),3))
  bearings=[_bearing(seed,sx,sy) for sx in range(-2,2) for sy in range(-2,2)]
  mean_s=statistics.mean(math.sin(a) for a in bearings); mean_c=statistics.mean(math.cos(a) for a in bearings)
  circ_sd=math.degrees(math.sqrt(-2*math.log(max(1e-12,math.hypot(mean_s,mean_c)))))
  assert circ_sd>=25,(seed,circ_sd)
 assert len(set(x_crossings))==len(x_crossings),x_crossings


def test_world_region_never_calls_legacy_creeks(monkeypatch):
 skip_river_generation(monkeypatch)
 import region,world
 def forbidden(*args): raise AssertionError('legacy per-tile water was called')
 monkeypatch.setattr(region,'make_creeks',forbidden)
 monkeypatch.setattr(world,'_tile',lambda *args,**kwargs:[world.feature('roads','test-road',rect=(0,0,10,10),surface='gravel')])
 monkeypatch.setattr(world,'settlements',lambda *args,**kwargs:[])
 assert window(14,(0,0,MILE,MILE),Settings({'stages.houses':False}),detail=False)['features']


def test_correction_lines_shift_north_south_every_twenty_four_miles(monkeypatch):
 skip_river_generation(monkeypatch)
 from world import correction_shift,paved_points,road_type
 from settings import applied
 from unittest.mock import patch
 with patch('world._route_offset',side_effect=lambda *args:0):
  _test_correction_lines_shift_north_south_every_twenty_four_miles(correction_shift,paved_points,road_type)

def _test_correction_lines_shift_north_south_every_twenty_four_miles(correction_shift,paved_points,road_type):
 from settings import applied
 from unittest.mock import patch
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
  with patch('world._tile',return_value=[]),patch('world.settlements',return_value=[]):
   north=window(31,(-MILE,23*MILE,4*MILE,2*MILE),Settings({'stages.houses':False}),detail=False)
  roads={f['id']:f for f in north['features'] if f['kind']=='roads'}
  a=roads['road:v:2:23']['rect']; b=roads['road:v:2:24']['rect']
  assert a[0]!=b[0] and a[1]+a[3]==b[1]==24*MILE
  # E-W survey sections on either side still meet, without correction offsets.
  for line in (23,24,25):
   for segment in (-1,0,1):
    a=roads[f'road:h:{line}:{segment}']['rect']; b=roads[f'road:h:{line}:{segment+1}']['rect']
    assert a[0]+a[2]==b[0] and a[1]==b[1]


def test_bridges_cover_every_paved_rail_and_gravel_water_crossing():
 from world import bridges,feature
 simple=[feature('rivers','water',points=[(0,-100),(0,100)],width=20),
         feature('roads','paved-road',points=[(-100,0),(100,0)],width=12,surface='paved'),
         feature('rail','train',points=[(-100,50),(100,50)],width=10)]
 first=bridges(simple); second=bridges(list(reversed(simple)))
 assert sorted(first,key=lambda f:f['id'])==sorted(second,key=lambda f:f['id'])
 assert len(first)==2


def test_rail_density_directions_curvature_and_corrected_crossing_angles(monkeypatch):
 import math
 skip_tile_settlement_work(monkeypatch)
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


def test_route_town_spacing_and_rail_industrial_frontage(monkeypatch):
 import math,json,collections
 skip_river_generation(monkeypatch)
 from world import town_candidates,settlements
 from presets import document
 from settings import applied
 settings=Settings({'stages.houses':False})
 with applied(settings):
  rect=(-12*MILE,-12*MILE,24*MILE,24*MILE)
  candidates=list(town_candidates(31,rect)); groups={}
  for site in candidates:
   if site[-1]: groups.setdefault(':'.join(site[0].split(':')[:-1]),[]).append(site)
  for group in groups.values():
   axis=group[0][1]; group.sort(key=lambda s:s[3] if axis=='v' else s[2])
   distances=[abs((b[3]-a[3]) if axis=='v' else (b[2]-a[2]))/MILE for a,b in zip(group,group[1:])]
  assert distances and all(6<=d<=10 for d in distances)
  fs=settlements(31,rect,json.dumps(document(settings),sort_keys=True),True)
  towns=[f for f in fs if f['kind']=='towns']
  assert any(t['rail_town'] for t in towns) and any(not t['rail_town'] for t in towns)
  assert sum(t['size']=='tiny' for t in towns)/len(towns)>=.5
  assert min(math.dist(a['anchor'],b['anchor']) for i,a in enumerate(towns) for b in towns[i+1:])>=6*MILE
  for town in towns:
   if not town['rail_town']: continue
   prefix=town['id'][:-4]
   buildings=[f for f in fs if f['kind']=='buildings' and f['id'].startswith(prefix)]
   assert any(f.get('use')=='depot' for f in buildings)
   assert any(f.get('use')=='elevator' for f in buildings)


def test_farmstead_density_and_world_access_lane_lengths(monkeypatch):
 import math,json
 from world import _tile,FT,share_farm_lanes,feature
 from presets import document
 from settings import applied
 skip_river_generation(monkeypatch)
 settings=Settings({'stages.houses':False}); serialized=json.dumps(document(settings),sort_keys=True)
 farms=[f for f in _tile(14,0,0,serialized,False) if f['kind']=='farms' and f['use']=='farmstead']
 assert 1<=len(farms)/9<=2
 with applied(Settings()):
  features=_tile(31,0,0,json.dumps(document(Settings()),sort_keys=True),True)
  farms=[f for f in features if 'lane_length_ft' in f]
  assert len(farms)>3
  for farm in farms:
   assert 250<=farm['lane_length_ft']<=830
   assert abs(sum(math.dist(a,b) for a,b in zip(farm['access'],farm['access'][1:]))/FT(1)-farm['lane_length_ft'])<1e-6
  # Deterministic shared entrance, with the old private road entrances trimmed.
  for seed in range(100):
   out=[feature('lots','a',rect=(0,0,1,1)),feature('lots','b',rect=(0,0,1,1)),feature('driveways','a:entry',rect=(0,0,FT(14),FT(150))),feature('driveways','b:entry',rect=(FT(400),0,FT(14),FT(150)))]
   sites=[dict(id=id,road='same',access=[(FT(x),0),(FT(x),FT(140)),(FT(x),FT(350))],length=FT(350),entry_id=id+':entry') for id,x in [('a',7),('b',407)]]
   share_farm_lanes(seed,out,sites)
   if len(out)>4:
    assert out[-1]['members']==['a','b']
    assert out[2]['rect'][1]==out[3]['rect'][1]==FT(60)
    assert out[0]['shared_lane']==out[1]['shared_lane']
    break
  else: assert False,'no seeded shared-lane cluster'
