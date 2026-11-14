import sys, os, re
sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))
import pytest
from region import (make_region, make_roads, make_rail, spawn_region, road_rect, inside, to_world, world_rect, overlaps, rail_hits, acres,
                    FT, MILE, N_MI, ROW, RAIL_GAP, RAIL_ROW)
from spawner import spawn

SEED = 30      # natural rail town with a mobile home park (not forced)
NORAIL = 14    # natural town, region has no rail

@pytest.fixture(scope="module")
def reg(): return make_region(SEED)

def test_region_deterministic(reg):
    b = make_region(SEED)
    assert [(l["spec"], l["place"]) for l in reg["lots"]] == [(l["spec"], l["place"]) for l in b["lots"]]
    assert reg["parcels"] == b["parcels"]

def test_paved_share_and_lettered_routes(reg):
    assert 0.22 <= reg["paved_share"] <= 0.25
    for r in reg["grid_roads"]:
        if r["type"] == "paved": assert re.fullmatch(r"[A-Z]\d+", r["name"])
        assert 22 <= r["surface_ft"] <= (24 if r["type"] == "gravel" else 22) and r["w"] == FT(66)

def test_every_lot_fronts_its_address_road(reg):
    byname = {r["name"]: r for r in reg["roads"]}
    for l in reg["lots"]:
        s = l["spec"]; pt = to_world(l["place"], s["width"], s["depth"], s["width"] // 2, -FT(2))
        assert inside(pt, road_rect(byname[s["address_street"]])), s["id"]

def test_corner_lots_have_one_address_street(reg):
    corners = [l["spec"] for l in reg["lots"] if l["spec"]["corner_side"]]
    assert corners
    for s in corners:
        assert s["side_street"] and s["side_street"] != s["address_street"] and s["corner_side"] in ("left", "right")

def test_town_on_blacktop_crossing_with_crg_dimensions(reg):
    t = reg["town"]; assert t
    paved = [r for r in reg["grid_roads"] if r["type"] == "paved"]
    assert any(r["axis"] == "v" and r["c"] == t["Xc"] for r in paved) and any(r["axis"] == "h" and r["c"] == t["Yc"] for r in paved)
    assert 4 <= t["cols"] * t["rows"] <= 9 and FT(380) <= t["cx"] <= FT(445)
    assert FT(60) <= t["street_w"] <= FT(66) and FT(16) <= t["alley_w"] <= FT(20)
    for b in t["blocks"]: assert FT(130) <= b["lot_d"] <= FT(150)
    sb = t["setbacks"]
    assert FT(20) <= sb["front"] <= FT(30) and FT(5) <= sb["side"] <= FT(8) and FT(20) <= sb["rear"] <= FT(25) and FT(10) <= sb["corner"] <= FT(15)
    for l in reg["lots"]:
        s = l["spec"]
        if s["kind"] == "town":
            assert s["width"] >= s["base_lots"] * FT(50) and s["width"] < (s["base_lots"] + 1) * FT(50)
            assert (s["base_lots"] == 1) == (s["house_type"] == "trailer") and s["base_lots"] <= 4
            if s["corner_side"]: assert s[f"side_{s['corner_side']}"] == sb["corner"]

def test_rail_is_optional_per_region():
    has = [make_rail(s, make_roads(s, False)[0]) is not None for s in range(1, 13)]
    assert any(has) and not all(has)

def test_rail_runs_beside_a_road_and_crosses_roads(reg):
    r = reg["rail"]; assert r
    (xa, ya), (xb, yb) = r["pts"][1], r["pts"][2]
    assert xa == xb and yb - ya >= FT(3000)                       # a straight parallel run
    assert abs(xa - r["along_c"]) == ROW // 2 + RAIL_GAP + RAIL_ROW // 2   # just off the road ROW
    road_ids = {x["id"] for x in reg["grid_roads"]}
    assert len(r["crossings"]) >= 3 and all(c[0] in road_ids for c in r["crossings"])
    ew = [c for c in r["crossings"] if c[0].startswith("EW")]; assert ew   # at-grade crossings of E-W roads

def test_rail_town_is_aligned_with_the_rail(reg):
    t, r = reg["town"], reg["rail"]; assert t["rail_town"]
    b = t["box"]
    assert (t["Xc"], t["Yc"]) == r["site"]
    assert r["parallel"][0] <= b[1] and b[1] + b[3] <= r["parallel"][1]     # town sits along the parallel run
    assert (b[0] + b[2] <= r["along_c"] + FT(40)) if r["side"] > 0 else (b[0] >= r["along_c"] - FT(40))   # town across the road from the track
    assert t["rows"] >= t["cols"] and reg["depot"] and reg["siding"]
    el = next(p for p in reg["parcels"] if p["kind"] == "elevator")
    assert 3 <= el["acres"] <= 6 and not rail_hits(r, el["rect"])
    for l in reg["lots"]:
        s = l["spec"]; assert not rail_hits(r, world_rect(l["place"], s["width"], s["depth"])), s["id"]

def test_towns_without_rail_are_allowed():
    reg = make_region(NORAIL)
    assert reg["rail"] is None and reg["town"] and not reg["town"]["rail_town"]
    assert reg["depot"] is None and reg["siding"] is None
    els = [p for p in reg["parcels"] if p["kind"] == "elevator"]
    assert len(els) == ("elevator" in reg["town"]["program"]) and all(e.get("truck") for e in els)      # truck-served co-op only

def _inside(r, R): return R[0] <= r[0] and R[1] <= r[1] and r[0] + r[2] <= R[0] + R[2] and r[1] + r[3] <= R[1] + R[3]
def _ovl(a, b): return a[0] < b[0] + b[2] and b[0] < a[0] + a[2] and a[1] < b[1] + b[3] and b[1] < a[1] + a[3]

@pytest.fixture(scope="module")
def parkreg(reg):
    """Seed 30's natural town fails the park rule, so park geometry is tested on the same town with a big stick-built count."""
    from region import make_park
    big = dict(reg["town"], lots=[{"house_type": "stick"}] * 120)
    occ = [road_rect(r) for r in reg["roads"]] + [reg["town"]["box"]] + [p["rect"] for p in reg["parcels"] if not p.get("in_town")]
    p = make_park(SEED, big, reg["grid_roads"], reg["rail"], occ, force=True)
    return dict(reg, park=p)

def test_park_rule_branches(reg):
    from region import park_condition
    t = reg["town"]
    assert reg["park"] is None and not t["park_check"]["ok"]                                   # demo town: too many trailers
    only = dict(t, lots=[{"house_type": "trailer"}] * 10)
    assert park_condition(only, 20)["ok"] and "only" in park_condition(only, 20)["reason"]   # (a) trailers only
    mix = dict(t, lots=[{"house_type": "stick"}] * 60 + [{"house_type": "trailer"}] * 5)
    assert park_condition(mix, 25)["ok"] and not park_condition(mix, 26)["ok"]                # (b) exactly 2x boundary

def test_coop_office_on_coop_lot_not_main_street(reg):
    assert all(c["type"] != "co-op office" for c in reg["town"]["commercial"])
    el = next(p for p in reg["parcels"] if p["kind"] == "elevator")
    assert _inside(el["office"], el["rect"]) and _inside(el["office_parking"], el["rect"]) and _touch(el["office"], el["office_parking"])
    nr = make_region(NORAIL)       # no rail: co-op office only on a truck-served elevator lot, when the co-op exists
    for p in nr["parcels"]:
        if "office" in p: assert p["kind"] == "elevator" and p["truck"] and _inside(p["office"], p["rect"]) and _inside(p["office_parking"], p["rect"])
    assert all(c["type"] != "co-op office" for c in nr["town"]["commercial"])

def test_town_size_classes_and_ragged_edge():
    sizes = {}
    for s in range(1, 61):
        t = make_region(s)["town"]
        if t: sizes.setdefault(t["size"], []).append((s, t))
    assert {"tiny", "mid", "big"} <= set(sizes)
    for s, t in sizes["mid"] + sizes["big"]:
        assert len(t["blocks"]) < t["cols"] * t["rows"] or t["cols"] * t["rows"] > 0          # ragged: some outer blocks left out
        assert t["band"] >= 1
    assert any(len(t["blocks"]) < t["cols"] * t["rows"] for _, t in sizes["mid"] + sizes["big"])
    assert all(t["band"] == 2 for _, t in sizes["big"] if t["pop_est"] >= 750)

def test_small_town_fails_park_rule():
    r = make_region(13, force_park=True); t = r["town"]
    assert t["cols"] * t["rows"] <= 4 and r["park"] is None and not t["park_check"]["ok"]

@pytest.fixture(scope="module")
def bigreg():
    return make_region(SEED, force_pop=1000)            # same town geometry, 750-1,499 band: exercises most siting

def test_band_chooser_deterministic():
    from region import choose_businesses, town_band, BIZ_P
    assert town_band(100) == 0 and town_band(250) == 1 and town_band(749) == 1 and town_band(750) == 2
    for band in range(3): assert choose_businesses(7, band) == choose_businesses(7, band)
    assert len({tuple(choose_businesses(s, 1)) for s in range(20)}) > 5
    po = sum("post office" in choose_businesses(s, 0) for s in range(300)) / 300
    assert abs(po - BIZ_P["post office"][0] / 100) < 0.08
    assert make_region(SEED)["town"]["commercial"] == make_region(SEED)["town"]["commercial"]

def test_snapshot_year_and_monotone_rates():
    from region import region_year, biz_rates, vacant_pct, branch_pct, FADING_TYPES, YEARS
    assert region_year(SEED) == region_year(SEED) == make_region(SEED)["town"]["year"]
    assert len({region_year(s) for s in range(40)}) > 1
    for band in range(3):
        R = [biz_rates(band, y) for y in YEARS]
        for k in R[0]:
            seq = [r[k] for r in R]
            if k in FADING_TYPES: assert all(a >= b for a, b in zip(seq, seq[1:])), k       # fade toward 2000
            else: assert len(set(seq)) == 1, k                                               # banks etc. unchanged
        assert abs(biz_rates(band, 1998)["grocery"] - [6, 30, 70][band]) < 1e-9
    v = [vacant_pct(y) for y in YEARS]; b = [branch_pct(y) for y in YEARS]
    assert all(x < y for x, y in zip(v, v[1:])) and all(x < y for x, y in zip(b, b[1:]))

def _biz_checks(reg):
    t = reg["town"]; C = t["commercial"]; assert C
    roads = [road_rect(r) for r in reg["roads"]]
    houses = [world_rect(l["place"], l["spec"]["width"], l["spec"]["depth"]) for l in reg["lots"] if l["spec"].get("use", "house") == "house"]
    others = [p["rect"] for p in reg["parcels"] if not p.get("in_town")] + ([reg["park"]["rect"]] if reg["park"] else [])
    own = [c for c in C if c["type"] != "funeral home"]
    for i, c in enumerate(own):
        L = c["lot"]
        acc = [a for a in c["parking"] + [c["curb"]] if a]
        assert acc and any(_touch(a, r) for a in acc for r in roads), c["type"]          # street or curb access
        assert any(_touch(a, c["bldg"]) for a in acc), c["type"]
        assert not any(_ovl(L, o["lot"]) for o in own[i + 1:]), c["type"]
        assert not any(_ovl(L, h) for h in houses) and not any(_ovl(L, r) for r in roads) and not any(_ovl(L, o) for o in others), c["type"]
        assert not rail_hits(reg["rail"], L) and _inside(c["bldg"], L)
    fh = [c for c in C if c["type"] == "funeral home"]
    for c in fh: assert any(l["spec"]["id"] == c["house_lot"] and l["spec"]["use"] == "funeral home" for l in reg["lots"])

def test_business_access_and_no_overlap(reg, bigreg):
    _biz_checks(reg); _biz_checks(bigreg); _biz_checks(make_region(13))
    assert not bigreg["town"]["unplaced"]

def test_storefronts_attached(bigreg):
    S = [c for c in bigreg["town"]["commercial"] if c["storefront"]]; assert len(S) >= 2
    for c in S: assert c["bldg"][0] == c["lot"][0] and c["bldg"][2] == c["lot"][2] == FT(25)        # zero side setbacks
    for c in S:
        same = [o for o in S if o is not c and o["front_y"] == c["front_y"] and o["facing"] == c["facing"]]
        if same: assert any(o["bldg"][0] in (c["bldg"][0] + c["bldg"][2], c["bldg"][0] - o["bldg"][2]) for o in same)   # party walls

def test_gas_is_the_fuel_parcel(reg):
    gas = [c for c in reg["town"]["commercial"] if c["type"] == "gas station"]
    fuel = [p for p in reg["parcels"] if p["kind"] == "fuel"]; assert len(fuel) == 1
    if gas: assert fuel[0]["rect"] == gas[0]["lot"] and 2 <= len(gas[0]["pumps"]) <= 4 and 6 <= gas[0]["spaces"] <= 12

def test_park_pads_inside_and_clear(parkreg):
    reg = parkreg
    p = reg["park"]; assert p and 15 <= len(p["pads"]) <= 40
    T = [x["trailer"] for x in p["pads"]]
    for x in p["pads"]:
        for r in (x["trailer"], x["parking"]):
            assert _inside(r, p["rect"]) and not any(_ovl(r, w) for w in p["windbreak"]) and not any(_ovl(r, l) for l in p["lanes"])
    assert not any(_ovl(a, b) for i, a in enumerate(T) for b in T[i + 1:])
    assert p["windbreak"] and all(_inside(w, p["rect"]) for w in p["windbreak"])

def test_every_park_trailer_has_a_pad_on_a_lane_that_reaches_the_road(parkreg):
    reg = parkreg
    p = reg["park"]; road = next(r for r in reg["grid_roads"] if r["id"] == p["road"])
    edge = {road["c"] + ROW // 2, road["c"] - ROW // 2}
    R = p["rect"]; assert R[1] in edge or R[1] + R[3] in edge                     # park fronts the public road
    reach = {i for i, l in enumerate(p["lanes"]) if l[1] in edge or l[1] + l[3] in edge}
    assert reach
    while True:                                                                    # lanes connected to a road-touching lane
        more = {i for i, l in enumerate(p["lanes"]) if i not in reach and any(_touch(l, p["lanes"][j]) for j in reach)}
        if not more: break
        reach |= more
    assert reach == set(range(len(p["lanes"])))
    for x in p["pads"]:
        assert _touch(x["parking"], x["trailer"]) and _touch(x["parking"], p["lanes"][x["lane"]])
        tr = x["trailer"]; ln = p["lanes"][x["lane"]]
        assert (tr[2] > tr[3]) == (ln[3] > ln[2])                                 # trailer perpendicular to its lane

def test_town_lot_trailers_are_a_minority(reg):
    tl = [l for l in reg["lots"] if l["spec"]["kind"] == "town"]
    assert sum(l["spec"]["house_type"] == "trailer" for l in tl) <= 0.4 * len(tl)

def _touch(r, c): return r[0] <= c[0] + c[2] and c[0] <= r[0] + r[2] and r[1] <= c[1] + c[3] and c[1] <= r[1] + r[3]

def test_drive_endpoints_and_connections(reg):
    seen = set()
    for l in reg["lots"]:
        s = l["spec"]; key = (s["kind"], s.get("house_type"))
        if key in seen: continue
        plan, rep, _, _ = spawn(s)
        if plan is None: continue
        seen.add(key)
        dr = plan.site["drive"]; od = next((d for d in plan.doors if d.role == "overhead"), None)
        assert rep["3"][0] == "pass", (s["id"], rep["3"])
        assert any(r[1] == 0 or (s.get("alley_rear") and r[1] + r[3] == s["depth"]) for r in dr)    # starts at road (or alley)
        if od: assert any(r[0] <= od.x1 and od.x2 <= r[0] + r[2] and r[1] <= od.y1 <= r[1] + r[3] for r in dr)
        else:
            hs = [r for r in plan.rooms if r.kind != "garage"]; x0 = min(r.x for r in hs); x1 = max(r.x + r.w for r in hs)
            assert any(r[0] == x1 or r[0] + r[2] == x0 for r in dr)                                # pad beside the house
        for n, b in plan.site.get("farm", []): assert any(_touch(b, r) for r in dr), n              # vehicle buildings on the drive
    assert ("farmstead", None) in seen and ("town", "stick") in seen

def test_sample_plans_drive_farm_and_grove(reg):
    pick = {}
    for l in reg["lots"]: pick.setdefault(l["spec"]["kind"], []).append(l)
    for kind, ls in pick.items():
        for l in ls[:2]:
            plan, rep, _, _ = spawn(l["spec"])
            if plan is None: continue
            assert rep["3"][0] == "pass" and rep["FB"][0] == "pass", (l["spec"]["id"], rep["3"], rep["FB"])
            if kind == "farmstead":
                lane = plan.site["drive"][1]
                assert FT(12) <= lane[2] <= FT(16) and FT(150) <= lane[3] <= FT(800) and FT(24) <= plan.site["drive"][0][2] <= FT(40)
                assert plan.site["grove"] and plan.site["farm"]

def test_editing_one_lot_leaves_other_lot_plans_unchanged(reg):
    rural = [l for l in reg["lots"] if l["spec"]["kind"] == "acreage"]
    target = rural[0]["spec"]["id"]
    edited = make_region(SEED, overrides={target: 150})
    ids = [l["spec"]["id"] for l in reg["lots"] if l["spec"]["kind"] != "town"][:8]
    a = {l["spec"]["id"]: l for l in reg["lots"]}; b = {l["spec"]["id"]: l for l in edited["lots"]}
    for k in ids:
        if k != target:
            pa, pb = spawn(a[k]["spec"])[0], spawn(b[k]["spec"])[0]
            assert (pa and pa.digest()) == (pb and pb.digest()), k


def _hull_area(P):
    P = sorted(set(P))
    def cr(o, a, b): return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, up = [], []
    for p in P:
        while len(lo) >= 2 and cr(lo[-2], lo[-1], p) <= 0: lo.pop()
        lo.append(p)
    for p in reversed(P):
        while len(up) >= 2 and cr(up[-2], up[-1], p) <= 0: up.pop()
        up.append(p)
    h = lo[:-1] + up[:-1]
    return abs(sum(h[i][0] * h[i - 1][1] - h[i - 1][0] * h[i][1] for i in range(len(h)))) / 2

@pytest.fixture(scope="module")
def creekregs():
    regs = [make_region(s) for s in (31, 14, 13)]
    assert all(r["creeks"] for r in regs)
    return regs

def test_no_lots_or_parcels_in_floodplain(creekregs):
    for r in creekregs:
        C = r["creeks"]
        for l in r["lots"]:
            s = l["spec"]; assert not rail_hits(C, world_rect(l["place"], s["width"], s["depth"])), s["id"]
        for p in r["parcels"]: assert not rail_hits(C, p["rect"]), p["kind"]
        for c in r["town"]["commercial"]: assert not rail_hits(C, c["lot"]), c["type"]
        if r["park"]: assert not rail_hits(C, r["park"]["rect"])
        for c in C:
            assert FT(150) <= c["buffer"] <= FT(300)
            road_ids = {x["id"] for x in r["grid_roads"]}
            assert all(b[0] in road_ids for b in c["bridges"])
    assert any(c["bridges"] for r in creekregs for c in r["creeks"])        # creek crossing a road gets a bridge

def test_town_blocks_never_cross_creek(creekregs):
    for r in creekregs:
        for b in r["town"]["blocks"]: assert not rail_hits(r["creeks"], b["rect"]), b["id"]

def test_town_footprint_not_rectangular():
    for s in (31, 14, 46):
        t = make_region(s)["town"]; B = [b["rect"] for b in t["blocks"]]
        P = [(x + dx, y + dy) for x, y, w, h in B for dx in (0, w) for dy in (0, h)]
        assert sum(w * h for *_, w, h in B) / _hull_area(P) < 0.8, s
        assert len(B) < t["cols"] * t["rows"]
