"""Geometric verification. Failures return nonzero exit status through cli.py."""
import math,json
from collections import defaultdict
from settings import Settings,applied,option
from world import window,intersects,MILE
from region import make_region,road_rect
from spawner import FT

def positive_overlap(a,b): return a[0]<b[0]+b[2] and b[0]<a[0]+a[2] and a[1]<b[1]+b[3] and b[1]<a[1]+a[3]

class Index:
 def __init__(self,features):
  self.size=FT(option('verify.bin_ft')); self.cells=defaultdict(list)
  for f in features:
   for key in self.keys(f['bounds']): self.cells[key].append(f)
 def keys(self,r):
  for x in range(math.floor(r[0]/self.size),math.floor((r[0]+r[2])/self.size)+1):
   for y in range(math.floor(r[1]/self.size),math.floor((r[1]+r[3])/self.size)+1): yield x,y
 def candidates(self,r):
  found={}
  for k in self.keys(r):
   for f in self.cells[k]: found[f['id']]=f
  return found.values()

def building_collisions(features):
 buildings=[f for f in features if f['kind']=='buildings']; index=Index(buildings); collisions=[]
 for f in buildings:
  for other in index.candidates(f['bounds']):
   if other['id']>f['id'] and positive_overlap(f['bounds'],other['bounds']): collisions.append((f['id'],other['id']))
 return collisions

def orphan_roads(features):
 roads=[f for f in features if f['kind']=='roads']; index=Index(roads)
 parent={r['id']:r['id'] for r in roads}
 def root(id):
  while parent[id]!=id: parent[id]=parent[parent[id]]; id=parent[id]
  return id
 for r in roads:
  for other in index.candidates(r['bounds']):
   if intersects(r['bounds'],other['bounds']): parent[root(r['id'])]=root(other['id'])
 components=defaultdict(list)
 for r in roads: components[root(r['id'])].append(r['id'])
 largest=max(components.values(),key=len,default=[])
 return [id for group in components.values() if group is not largest for id in group]

def intersection_angle(a,b,c,d):
 u=(b[0]-a[0],b[1]-a[1]); v=(d[0]-c[0],d[1]-c[1]); det=u[0]*v[1]-u[1]*v[0]
 if det==0: return None
 q=(c[0]-a[0],c[1]-a[1]); t=(q[0]*v[1]-q[1]*v[0])/det; s=(q[0]*u[1]-q[1]*u[0])/det
 if not (0<=t<=1 and 0<=s<=1): return None
 cosine=min(1,abs(u[0]*v[0]+u[1]*v[1])/(math.hypot(*u)*math.hypot(*v)))
 return math.degrees(math.acos(cosine))

def road_line(f):
 x,y,w,h=f['rect']; axis=f.get('axis') or ('h' if w>=h else 'v')
 return ((x,y+h/2),(x+w,y+h/2)) if axis=='h' else ((x+w/2,y),(x+w/2,y+h))

def crossing_angles(features):
 angles=[]; roads=Index([f for f in features if f['kind']=='roads'])
 for rail in [f for f in features if f['kind']=='rail']:
  for road in roads.candidates(rail['bounds']):
   c,d=road_line(road)
   for a,b in zip(rail['points'],rail['points'][1:]):
    angle=intersection_angle(a,b,c,d)
    if angle is not None: angles.append(angle)
 return angles

def tree_collisions(features):
 blockers=Index([f for f in features if f['kind'] in ('roads','buildings','driveways','rail')]); failed=[]
 radius=FT(option('world.tree_radius_ft'))
 for f in features:
  if f['kind']!='trees': continue
  x,y,_,_=f['rect']; rect=(x-radius,y-radius,2*radius,2*radius)
  if any(intersects(rect,b['bounds']) for b in blockers.candidates(rect)): failed.append(f['id'])
 return failed

def run(seed=31,settings=None,rect=None):
 settings=settings or Settings(); rect=rect or (0,0,3*MILE,3*MILE)
 with applied(settings):
  a=window(seed,rect,settings); again=window(seed,rect,settings)
  shifted=(rect[0]+rect[2]/2,rect[1],rect[2],rect[3]); b=window(seed,shifted,settings)
  common=(shifted[0],rect[1],rect[2]/2,rect[3])
  project=lambda s:{f['id']:f for f in s['features'] if intersects(f['bounds'],common)}
  halo=(rect[0]-MILE,rect[1]-MILE,rect[2]+2*MILE,rect[3]+2*MILE)
  expanded=window(seed,halo,settings); features=expanded['features']
  collisions=building_collisions(features); orphans=orphan_roads(features); trees=tree_collisions(features)
  angles=crossing_angles(features)
  region=make_region(seed); local=[]
  if region['rail']:
   rails=[{'kind':'rail','id':'region-rail','points':region['rail']['pts'],'bounds':(0,0,3*MILE,3*MILE)}]
   roads=[dict(kind='roads',id=r['id'],rect=road_rect(r),bounds=road_rect(r),axis=r['axis']) for r in region['roads']]
   local=crossing_angles(rails+roads)
  local_roads=[dict(kind='roads',id='region:'+r['id'],rect=road_rect(r),bounds=road_rect(r),axis=r['axis']) for r in region['roads']]
  orphans.extend(orphan_roads(local_roads))
  all_angles=angles+local
  near=sum(a>=90-option('verify.square_deviation_deg') for a in all_angles)/len(all_angles) if all_angles else 1
  checks={
   'determinism':{'pass':json.dumps(a,sort_keys=True)==json.dumps(again,sort_keys=True)},
   'region_determinism':{'pass':json.dumps(region,sort_keys=True)==json.dumps(make_region(seed),sort_keys=True)},
   'matching_overlaps':{'pass':project(a)==project(b)},
   'no_building_overlaps':{'pass':not collisions,'failures':collisions[:10],'count':len(collisions),'examined':sum(f['kind']=='buildings' for f in features)},
   'no_orphan_roads':{'pass':not orphans,'failures':orphans[:10],'count':len(orphans),'examined':sum(f['kind']=='roads' for f in features)},
   'rail_crossings':{'pass':all(a>=option('verify.minimum_angle_deg') for a in all_angles) and near>=option('verify.square_share'),'count':len(all_angles),'minimum_angle':min(all_angles,default=None),'near_square_share':near},
   'no_tree_on_geometry':{'pass':not trees,'failures':trees[:10],'count':len(trees),'examined':sum(f['kind']=='trees' for f in features)},
  }
  return {'seed':seed,'pass':all(v['pass'] for v in checks.values()),'checks':checks}
