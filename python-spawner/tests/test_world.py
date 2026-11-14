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
