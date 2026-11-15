"""Fast checks for culvert-or-bridge classification (item 3)."""
from settings import Settings, applied
from spawner import FT
from world import bridges, feature, feature_style, CROSSING_STYLE, COUNTY_BRIDGE_TYPES


def test_width_rule_and_county_mix_fields():
 with applied(Settings()):
  # Under 6 ft -> CMP
  narrow = [
   feature('rivers', 'w', points=[(0, -100), (0, 100)], width=FT(4), widths=[FT(4), FT(4)]),
   feature('roads', 'r', points=[(-100, 0), (100, 0)], width=12, surface='gravel'),
  ]
  decks = bridges(narrow, seed=31)
  assert len(decks) == 1
  d = decks[0]
  assert d['structure'] == 'cmp_culvert' and d['structure_class'] == 'culvert'
  assert d['channel_ft'] == 4
  assert d['label'] == 'simplified'
  assert 16 <= d['deck_ft'] <= 66
  # 6-20 ft -> box
  mid = [
   feature('rivers', 'w', points=[(0, -100), (0, 100)], width=FT(12), widths=[FT(12), FT(12)]),
   feature('roads', 'r', points=[(-100, 0), (100, 0)], width=12, surface='paved'),
  ]
  d = bridges(mid, seed=31)[0]
  assert d['structure'] == 'box_culvert' and d['structure_class'] == 'culvert'
  assert d['channel_ft'] == 12
  # Above 20 ft -> bridge from county mix
  wide = [
   feature('rivers', 'w', points=[(0, -100), (0, 100)], width=FT(40), widths=[FT(40), FT(40)]),
   feature('roads', 'r', points=[(-100, 0), (100, 0)], width=12, surface='gravel'),
  ]
  d = bridges(wide, seed=31)[0]
  assert d['structure_class'] == 'bridge'
  assert d['structure'] in {n for n, _, _ in COUNTY_BRIDGE_TYPES}
  assert d['span_ft'] >= 20 and d['span_ft'] >= d['channel_ft']
  assert d['fhwa_type'] is not None


def test_feature_style_distinct_per_structure():
 colors = set()
 dashes = set()
 for name in list(CROSSING_STYLE):
  style = feature_style({'kind': 'bridges', 'structure': name, 'surface': 'gravel'})
  colors.add(style['color'])
  dashes.add(style.get('dash', ''))
 assert len(colors) == len(CROSSING_STYLE)
 # At least culverts and several bridges differ in dash pattern too.
 assert len(dashes) >= 4


def test_rail_crossing_still_emitted_without_mix_requirement():
 with applied(Settings()):
  simple = [
   feature('rivers', 'water', points=[(0, -100), (0, 100)], width=FT(40), widths=[FT(40), FT(40)]),
   feature('roads', 'paved-road', points=[(-100, 0), (100, 0)], width=12, surface='paved'),
   feature('rail', 'train', points=[(-100, 50), (100, 50)], width=10),
  ]
  first = bridges(simple, seed=14)
  second = bridges(list(reversed(simple)), seed=14)
  assert sorted(first, key=lambda f: f['id']) == sorted(second, key=lambda f: f['id'])
  assert len(first) == 2
  rail = next(f for f in first if f['carrier'] == 'rail')
  road = next(f for f in first if f['carrier'] == 'roads')
  assert rail['structure_class'] in ('culvert', 'bridge')
  assert road['structure_class'] == 'bridge'
