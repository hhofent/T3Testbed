"""Small trunk markers clipped to free yard space; no foliage geometry."""
from settings import option
from spawner import FT
from spawner import Draw, H

# Iowa street-tree counts: Wray & Mize 1985 report sugar maple 6,814,
# green ash 5,666, silver maple 4,850, Norway maple 3,283 and only 608
# American elms among 39,000+ trees; Gleason 1992 notes Dutch elm disease.
SPECIES = [('sugar_maple',26),('green_ash',22),('silver_maple',18),('norway_maple',12),('hackberry',5),('honeylocust',4),('linden',3),('bur_oak',3),('spruce',3),('siberian_elm',3),('american_elm',1)]

def town_markers(plan, lot_seed, older=False, street_trees=False):
 lot=plan.lot; w,dpth=lot['width'],lot['depth']; draw=Draw(H(lot_seed,'town-trees'))
 pad=FT(3); blocked=[r.rect() for r in plan.rooms]+[r for _,r in plan.site.get('farm',[])]+list(plan.site.get('drive',[]))
 blocked += [plan.site[k] for k in ('porch','deck') if plan.site.get(k)]
 blocked=[(x-pad,y-pad,w0+2*pad,h+2*pad) for x,y,w0,h in blocked]
 def free(x,y):
  return pad<=x<=w-pad and pad<=y<=dpth-pad and not any(a<=x<=a+c and b<=y<=b+e for a,b,c,e in blocked)
 pts=[]
 def place(x,y):
  if not free(x,y) or any((x-a)**2+(y-b)**2<FT(15)**2 for a,b in pts): return False
  pts.append((x,y)); return True
 n=0 if draw.chance('none',20) else 1+draw.int('n',2)
 if older and draw.chance('old',30): n=min(3,n+1)
 for i in range(n):
  for attempt in range(30):
   x=FT(10+draw.int(f'x{i}-{attempt}',max(1,w//FT(1)-20)))
   y=FT(dpth//FT(2)+draw.int(f'y{i}-{attempt}',max(1,dpth//FT(2)-10)))
   if place(x,y): break
 if street_trees:
  x=FT(15+draw.int('sx',30))
  while x<w and len(pts)<6:
   place(x,FT(4+draw.int(f'sy{x}',5))); x+=FT(40+draw.int(f'step{x}',31))
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
