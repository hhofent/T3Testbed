"""Small trunk markers clipped to free yard space; no foliage geometry."""
import math
from settings import option
from spawner import FT
from spawner import Draw, H

# Iowa street-tree counts: Wray & Mize 1985 report sugar maple 6,814,
# green ash 5,666, silver maple 4,850, Norway maple 3,283 and only 608
# American elms among 39,000+ trees; Gleason 1992 notes Dutch elm disease.
SPECIES = [('sugar_maple',26),('green_ash',22),('silver_maple',18),('norway_maple',12),('hackberry',5),('honeylocust',4),('linden',3),('bur_oak',3),('spruce',3),('siberian_elm',3),('american_elm',0)]

def town_markers(plan, lot_seed, older=False, street_trees=False):
 lot=plan.lot; w,dpth=lot['width'],lot['depth']; draw=Draw(H(lot_seed,'town-trees'))
 pad=FT(3); blocked=[r.rect() for r in plan.rooms]+[r for _,r in plan.site.get('farm',[])]+list(plan.site.get('drive',[]))
 blocked += [plan.site[k] for k in ('porch','deck') if plan.site.get(k)]
 blocked=[(x-pad,y-pad,w0+2*pad,h+2*pad) for x,y,w0,h in blocked]
 def free(x,y):
  return pad<=x<=w-pad and pad<=y<=dpth-pad and not any(a<=x<=a+c and b<=y<=b+e for a,b,c,e in blocked)
 pts=[]; gap=FT(option('town.tree_gap_ft')+draw.int('yard-gap',10))
 def place(x,y):
  if not free(x,y) or any((x-a)**2+(y-b)**2<gap**2 for a,b in pts): return False
  pts.append((x,y)); return True
 n=0 if draw.chance('none',option('town.yard_tree_none_pct')) else 1+draw.int('n',3)
 if older and draw.chance('old',30): n=min(3,n+1)
 clumps=1 if n<=2 else 1+draw.int('k',2); centers=[]
 rear=max((r.rect()[1]+r.rect()[3] for r in plan.rooms),default=dpth//2)+FT(10)
 for k in range(clumps):
  if draw.chance(f'side{k}',35):
   cx=FT(10) if draw.chance(f'side{k}x',50) else w-FT(10)
   cy=FT(10+draw.int(f'cy{k}',max(1,dpth//FT(1)-20)))
  else:
   lo=min(dpth-FT(10),max(FT(10),rear))
   cx=FT(10+draw.int(f'cx{k}',max(1,w//FT(1)-20)))
   cy=lo+FT(draw.int(f'cy{k}',max(1,(dpth-lo)//FT(1))))
  centers.append((cx,cy))
 for i in range(n):
  cx,cy=centers[i%len(centers)]
  for attempt in range(30):
   radius=FT(15+draw.int(f'r{i}-{attempt}',16)); angle=draw.int(f'a{i}-{attempt}',360)*math.pi/180
   if place(int(cx+radius*math.cos(angle)),int(cy+radius*math.sin(angle))): break
 if street_trees:
  x=FT(10+draw.int('s0',30)); k=0; added=0
  while x<w and added<2:
   if place(x,FT(4+draw.int(f'sy{k}',5))): added+=1
   x+=FT(40+draw.int(f's{k}',31)); k+=1
 out=[]
 for i,(x,y) in enumerate(pts):
  species=draw.weighted(f'sp{i}',SPECIES)
  out.append({'x':x,'y':y,'species':species,'setting':'town_street' if y<FT(9) else 'yard','condition':'declining' if 'elm' in species and draw.chance(f'dc{i}',47) else 'good'})
 return out

def local_markers(plan):
 spacing=FT(option('trees.spacing_ft')); lot=plan.lot; pad=FT(option('world.tree_radius_ft'))
 blocked=[r.rect() for r in plan.rooms]+[r for _,r in plan.site.get('farm',[])]+list(plan.site.get('drive',[]))
 blocked += [plan.site[k] for k in ('porch','deck') if plan.site.get(k)]
 points=[]
 for x in range(spacing,lot['width'],spacing):
  for y in range(spacing,lot['depth'],spacing):
   if any(a-pad<=x<=a+w+pad and b-pad<=y<=b+h+pad for a,b,w,h in blocked): continue
   points.append((x,y))
 return points
