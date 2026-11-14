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
