import math
from verification import building_collisions,orphan_roads,tree_collisions,intersection_angle,run
from world import feature,MILE
from settings import Settings

def test_verifier_catches_injected_geometry_faults():
 a=feature('buildings','a',rect=(0,0,100,100)); b=feature('buildings','b',rect=(50,50,100,100))
 assert building_collisions([a,b])==[('a','b')]
 road=feature('roads','road',rect=(0,0,100,10),axis='h')
 isolated=feature('roads','isolated',rect=(10000,10000,100,10),axis='h')
 assert orphan_roads([road,isolated])
 tree=feature('trees','tree',rect=(5,5,0,0))
 assert tree_collisions([road,tree])==['tree']
 angle=intersection_angle((-1,0),(1,0),(-1,-math.tan(math.pi/6)),(1,math.tan(math.pi/6)))
 assert abs(angle-30)<1e-8
 assert intersection_angle((0,0),(1,0),(0,1),(1,1)) is None

def test_verification_end_to_end_with_trees_and_buildings(monkeypatch):
 import verification,world
 from world import feature,intersects
 roads=[feature('roads','smoke-road-h',rect=(0,500,2*MILE,20),axis='h',surface='gravel'),
        feature('roads','smoke-road-v',rect=(MILE-10,0,20,2*MILE),axis='v',surface='gravel')]
 rail=feature('rail','smoke-rail',points=[(MILE,0),(MILE,2*MILE)],width=10)
 buildings=[feature('buildings','smoke-building-a',rect=(100,100,100,100)),
            feature('buildings','smoke-building-b',rect=(300,100,100,100))]
 trees=[feature('trees','smoke-tree',rect=(MILE/2,MILE/2,0,0))]
 scene_features=roads+[rail]+buildings+trees
 def scene(seed,rect,settings=None):
  return {'seed':seed,'window':rect,'features':[f for f in scene_features if intersects(f['bounds'],rect)]}
 monkeypatch.setattr(verification,'window',scene)
 monkeypatch.setattr(verification,'paved_road_gaps',lambda features,rect:[])
 monkeypatch.setattr(verification,'river_checks',lambda seed,rect:{name:{'pass':True,'failures':[]} for name in ('river_continuity','river_acyclic','rivers_downhill','no_river_crossings')})
 monkeypatch.setattr(world,'rivers',lambda seed,rect:())
 report=run(13,Settings(),(0,0,MILE,MILE))
 assert report['pass'],report
 assert report['checks']['no_building_overlaps']['examined']>0
 assert report['checks']['no_tree_on_geometry']['examined']>0
 assert report['checks']['rail_crossings']['minimum_angle']>=45


def test_ragged_street_islands_are_connected_without_losing_stubs():
 from connectivity import connect
 from region import road_rect
 grid=[dict(id='main',axis='h',c=0,lo=0,hi=1000,w=10)]
 streets=[dict(id='stub',axis='v',c=100,lo=100,hi=200,w=10)]
 connect(streets,grid)
 assert streets[0]['lo']==0 and streets[0]['hi']==200
 features=[dict(kind='roads',id=r['id'],rect=road_rect(r),bounds=road_rect(r)) for r in grid+streets]
 assert orphan_roads(features)==[]


def test_paved_gap_verifier_detects_deleted_edge():
 from verification import paved_road_gaps
 from world import window,MILE
 from settings import Settings
 rect=(0,0,2*MILE,2*MILE)
 features=window(31,rect,Settings(),detail=False)['features']
 assert not paved_road_gaps(features,rect)
 assert 'road:h:1:0' in paved_road_gaps([f for f in features if f['id']!='road:h:1:0'],rect)
