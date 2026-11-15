"""Fast checks for rural yard trees (item 1)."""
from settings import Settings, applied
from spawner import FT


def test_rural_yard_trees_clumpy_in_lot_with_species():
 from region import make_region, spawn_region
 from rural import layout
 with applied(Settings()):
  reg = make_region(31)
  plans = spawn_region(reg)
  farm = acre = 0
  for lot in reg['lots']:
   spec = lot['spec']
   plan = plans.get(spec['id'], {}).get('plan')
   if spec['kind'] not in ('farmstead', 'acreage') or not plan:
    continue
   site = layout(31, spec, plan, place=lot['place'])
   trees = site['trees']
   assert trees, spec['id']
   assert all(isinstance(t, dict) for t in trees)
   assert all(t.get('species') and t.get('setting') == 'yard' and t.get('label') == 'simplified' for t in trees)
   shape = site['shape']
   # Ray-cast containment (same idea as rural_trees._in_poly).
   def inside(x, y):
    n = len(shape); ok = False; j = n - 1
    for i in range(n):
     xi, yi = shape[i]; xj, yj = shape[j]
     if ((yi > y) != (yj > y)) and (x < (xj - xi) * (y - yi) / ((yj - yi) or 1e-12) + xi):
      ok = not ok
     j = i
    return ok
   rooms = [r.rect() for r in plan.rooms]
   x0 = min(r[0] for r in rooms); y0 = min(r[1] for r in rooms)
   hw = max(r[0] + r[2] for r in rooms) - x0; hd = max(r[1] + r[3] for r in rooms) - y0
   dx, dy = site['offset']; hx, hy = x0 + dx, y0 + dy
   cx, cy = hx + hw / 2, hy + hd / 2
   pad = FT(1.5)
   blockers = [(hx, hy, hw, hd)] + [r for _, r in site['buildings']] + list(site['driveways'])
   near = 0
   for t in trees:
    x, y = t['x'], t['y']
    assert inside(x, y), (spec['id'], x, y)
    assert not any(a - pad <= x <= a + w + pad and b - pad <= y <= b + h + pad for a, b, w, h in blockers)
    if (x - cx) ** 2 + (y - cy) ** 2 <= (FT(150) + FT(5)) ** 2:
     near += 1
   if spec['kind'] == 'farmstead':
    assert near >= 4, (spec['id'], near, len(trees))
    farm += 1
   else:
    acre += 1
   assert site == layout(31, spec, plan, place=lot['place'])
  assert farm >= 3 and acre >= 1
