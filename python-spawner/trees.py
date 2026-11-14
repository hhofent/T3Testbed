"""Small trunk markers clipped to free yard space; no foliage geometry."""
from settings import option
from spawner import FT

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
