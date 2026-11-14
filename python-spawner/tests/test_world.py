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


def test_drainage_continuity_crossings_and_downhill():
 from verification import river_checks
 from settings import applied
 for seed in (13,31,46):
  with applied(Settings()):
   checks=river_checks(seed,(-10*MILE,-10*MILE,40*MILE,40*MILE))
   assert all(c['pass'] for c in checks.values()),checks


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
  assert all(a[0]==b[0] for f in rails for a,b in zip(f['points'],f['points'][1:]))
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
   assert .007<len(missing[axis])/(40000*8/9)<.013
   assert all(line%9!=1 for line,segment in missing[axis])
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
 from world import road_type
 with applied(Settings({'world.missing_pct':10})):
  for axis in ('h','v'):
   for line in range(-26,28,9):
    assert all(road_type(31,axis,line,segment)=='paved' for segment in range(-100,100))


def test_rail_approaches_are_smooth_and_crossings_square():
 import math
 from world import rail_points
 from settings import applied
 with applied(Settings({'world.rail_diagonal_pct':100,'world.rail_bend_miles':.5})):
  points=rail_points(31,0,0,5)
  headings=[math.atan2(b[0]-a[0],b[1]-a[1]) for a,b in zip(points,points[1:])]
  assert all(heading==0 for heading in headings)
  assert headings[0]==headings[-1]==0
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
