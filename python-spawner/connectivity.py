"""Connect ragged street components with the shortest collinear extension.
The reference layout is retained in parity mode. Normal generation repairs islands
before rural lots are placed, so new connections reserve their road right-of-way.
"""
from region import road_rect,inside,overlaps

def connect(streets,grid):
 connected=list(grid); waiting=list(streets)
 while waiting:
  reached=[s for s in waiting if any(overlaps(road_rect(s),road_rect(r)) or _touch(s,r) for r in connected)]
  if reached:
   connected.extend(reached); waiting=[s for s in waiting if s not in reached]; continue
  choices=[]
  for i,s in enumerate(waiting):
   for r in connected:
    if s['axis']==r['axis']: continue
    if not r['lo']-r['w']/2<=s['c']<=r['hi']+r['w']/2: continue
    target=r['c']; distance=max(s['lo']-target,target-s['hi'],0)
    choices.append((distance,s['id'],r['id'],i,target))
  if not choices: raise ValueError('No grid connection available for street component')
  _,_,_,i,target=min(choices); s=waiting[i]
  s['lo']=min(s['lo'],target); s['hi']=max(s['hi'],target)

def _touch(a,b):
 x,y,w,h=road_rect(a); p,q,u,v=road_rect(b)
 return x<=p+u and p<=x+w and y<=q+v and q<=y+h
