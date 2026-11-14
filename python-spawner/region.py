"""Region stage, rural Iowa (Craig / Struble pattern): region seed -> 3 x 3 mile section grid of gravel roads,
lettered county blacktops (~25% of lines), a rare town at a blacktop crossing with an active rail line,
depot, elevator and siding; farmsteads, acreages, cemetery, church and fuel station; then the building spawner.

Lot seeds are hash(regionSeed, lotId); lot ids come from road / mile / slot, never from neighbors.
CRG = Construction Research Guy value (ranges are seeded inside the range). PLACEHOLDER = still needs a source.
"""
from settings import DEFAULTS
import math
from spawner import H, Draw, FT, IN, SF, make_lot, spawn, T

MILE = DEFAULTS['region.mile']
N_MI = DEFAULTS['region.n_mi']
ROW = DEFAULTS['region.row']
SURF = DEFAULTS['region.surf']
PAVED_PCT = DEFAULTS['region.paved_pct']
TOWN_PCT = DEFAULTS['region.town_pct']
CLEAR = DEFAULTS['region.clear']
FARM_PER_MILE = DEFAULTS['region.farm_per_mile']
ACREAGE_PCT = DEFAULTS['region.acreage_pct']
FARM_W = DEFAULTS['region.farm_w']
FARM_D = DEFAULTS['region.farm_d']
FARM_AC = DEFAULTS['region.farm_ac']
FARM_BACK = DEFAULTS['region.farm_back']
ACRE_W = DEFAULTS['region.acre_w']
ACRE_D = DEFAULTS['region.acre_d']
ACRE_AC = DEFAULTS['region.acre_ac']
ACRE_BACK = DEFAULTS['region.acre_back']
GROVE_W = DEFAULTS['region.grove_w']
TOWN_GRIDS = DEFAULTS['region.town_grids']
BLOCK_CX = DEFAULTS['region.block_cx']
STREET_W = DEFAULTS['region.street_w']
ALLEY_W = DEFAULTS['region.alley_w']
LOT_D = DEFAULTS['region.lot_d']
BASE_LOT = DEFAULTS['region.base_lot']
BASE_TAKE = DEFAULTS['region.base_take']
TRAILER_RUN_PCT = DEFAULTS['region.trailer_run_pct']
TRAILER_RUN = DEFAULTS['region.trailer_run']
PARK_PCT = DEFAULTS['region.park_pct']
PARK_PADS = DEFAULTS['region.park_pads']
PARK_PITCH = DEFAULTS['region.park_pitch']
PARK_LANE = DEFAULTS['region.park_lane']
PARK_SITE_D = DEFAULTS['region.park_site_d']
PARK_FRONT = DEFAULTS['region.park_front']
PARK_WB = DEFAULTS['region.park_wb']
INDUSTRY = DEFAULTS['region.industry']
RAIL_PCT = DEFAULTS['region.rail_pct']
RAIL_GAP = DEFAULTS['region.rail_gap']
RAIL_PARALLEL = DEFAULTS['region.rail_parallel']
RAILTOWN_PCT = DEFAULTS['region.railtown_pct']
NORAIL_TOWN_PCT = DEFAULTS['region.norail_town_pct']
RAIL_TOWN_GRIDS = DEFAULTS['region.rail_town_grids']
TOWN_SIZE = DEFAULTS['region.town_size']
GRIDS = DEFAULTS['region.grids']
RAGGED_PCT = DEFAULTS['region.ragged_pct']
RIBBON_DECAY = DEFAULTS['region.ribbon_decay']
EDGE_HALF_PCT = DEFAULTS['region.edge_half_pct']
FRINGE_PCT = DEFAULTS['region.fringe_pct']
MAIN_FACES = DEFAULTS['region.main_faces']
TOWN_SB = DEFAULTS['region.town_sb']
RAIL_ROW = DEFAULTS['region.rail_row']
RAIL_ANGLE = DEFAULTS['region.rail_angle']
ELEV_AC = DEFAULTS['region.elev_ac']
CEM = DEFAULTS['region.cem']
CHURCH_AC = DEFAULTS['region.church_ac']
FUEL = DEFAULTS['region.fuel']

def rng(*k): return Draw(H(*k))
def road_rect(r):
    hw = r.get("w", ROW) // 2
    return (r["lo"], r["c"] - hw, r["hi"] - r["lo"], 2 * hw) if r["axis"] == "h" else (r["c"] - hw, r["lo"], 2 * hw, r["hi"] - r["lo"])
def inside(pt, rect): x, y, w, h = rect; return x <= pt[0] <= x + w and y <= pt[1] <= y + h
def overlaps(a, b): return a[0] < b[0] + b[2] and b[0] < a[0] + a[2] and a[1] < b[1] + b[3] and b[1] < a[1] + a[3]
def acres(w, d): return w * d / SF / 43560

def to_world(place, W, D, x, y):
    x0, y0, fa = place["x0"], place["y0"], place["facing"]
    if fa == "S": return (x0 + x, y0 + y)
    if fa == "N": return (x0 + W - x, y0 + D - y)
    if fa == "W": return (x0 + y, y0 + W - x)
    return (x0 + D - y, y0 + x)
def world_rect(place, W, D): return (place["x0"], place["y0"], W, D) if place["facing"] in "SN" else (place["x0"], place["y0"], D, W)

# ---------- rail ----------
def _seg_rect(a, b, r):
    """Liang-Barsky: does segment a-b touch rect r?"""
    x0, y0, x1, y1 = r[0], r[1], r[0] + r[2], r[1] + r[3]
    dx, dy = b[0] - a[0], b[1] - a[1]; t0, t1 = 0.0, 1.0
    for p, q in ((-dx, a[0] - x0), (dx, x1 - a[0]), (-dy, a[1] - y0), (dy, y1 - a[1])):
        if p == 0:
            if q < 0: return False
        else:
            t = q / p
            if p < 0: t0 = max(t0, t)
            else: t1 = min(t1, t)
            if t0 > t1: return False
    return True

def rail_hits(rail, rect, pad=0):
    if isinstance(rail, list): return any(rail_hits(r, rect, pad) for r in rail)
    if not rail: return False
    e = rail["w"] // 2 + pad
    R = (rect[0] - e, rect[1] - e, rect[2] + 2 * e, rect[3] + 2 * e)
    return any(_seg_rect(a, b, R) for a, b in zip(rail["pts"], rail["pts"][1:]))

def make_rail(seed, roads, force=None):
    """Rail stage (after roads): mostly straight; runs parallel just off one N-S road's ROW past the E-W blacktop,
    then leans off diagonally to the region edges, crossing roads at grade."""
    d = rng(seed, "railroute")
    if not (d.chance("has", RAIL_PCT) if force is None else force): return None
    top = N_MI * MILE
    ew = next(r for r in roads if r["type"] == "paved" and r["axis"] == "h")
    along = d.choice("along", [r for r in roads if r["axis"] == "v" and 0 < r["c"] < top])
    side = 1 if d.chance("side", 50) else -1
    x = along["c"] + side * (ROW // 2 + RAIL_GAP + RAIL_ROW // 2)
    ya = ew["c"] - FT(d.choice("l1", RAIL_PARALLEL)); yb = ew["c"] + FT(d.choice("l2", RAIL_PARALLEL))
    k = 1 / math.tan(math.radians(RAIL_ANGLE[0] + d.int("ang", RAIL_ANGLE[1] - RAIL_ANGLE[0] + 1)))
    lean = 1 if d.chance("lean", 50) else -1
    pts = [(int(x - lean * ya * k), 0), (x, ya), (x, yb), (int(x + lean * (top - yb) * k), top)]
    cross = set()
    for r in roads:
        for a, b in zip(pts, pts[1:]):
            i = 1 if r["axis"] == "h" else 0
            if a[i] != b[i] and min(a[i], b[i]) <= r["c"] <= max(a[i], b[i]):
                t = (r["c"] - a[i]) / (b[i] - a[i]); o = int(a[1 - i] + t * (b[1 - i] - a[1 - i]))
                if 0 <= o <= top: cross.add((r["id"], (o, r["c"]) if i == 1 else (r["c"], o)))
    return {"pts": pts, "w": RAIL_ROW, "along": along["id"], "along_c": along["c"], "x": x, "side": side,
            "parallel": (ya, yb), "crossings": sorted(cross), "site": (along["c"], ew["c"])}

# ---------- creeks (PLACEHOLDER) ----------
CREEK_N = DEFAULTS['region.creek_n']
CREEK_W = DEFAULTS['region.creek_w']
CREEK_BUF = DEFAULTS['region.creek_buf']

def make_creeks(seed, roads):
    """Meandering, roughly diagonal polylines (Lyon County refs). The first passes 900-2200 ft off the blacktop crossing."""
    d = rng(seed, "creek"); top = N_MI * MILE; out = []
    ew = next(r for r in roads if r["type"] == "paved" and r["axis"] == "h"); ns = next((r for r in roads if r["type"] == "paved" and r["axis"] == "v"), None)
    ax_, ay_ = (ns["c"] if ns else top // 2), ew["c"]
    for k in range(d.weighted("n", CREEK_N)):
        dk = rng(seed, "creek", k)
        th = math.radians(dk.choice("th", [30, 40, 50, 130, 140, 150]) + dk.int("j", 11) - 5)
        ux, uy = math.cos(th), math.sin(th); px_, py_ = -uy, ux
        off = FT((900 + 100 * dk.int("off", 14)) if k == 0 else (3000 + 200 * dk.int("off", 21))) * (1 if dk.chance("side", 50) else -1)
        P = (ax_ + px_ * off, ay_ + py_ * off)
        pts = []
        for sgn in (-1, 1):
            x, y = P; lat = 0; seq = []
            for i in range(60):
                lat = max(-FT(400), min(FT(400), lat + FT(dk.int(f"m{sgn}{i}", 301) - 150)))
                x += sgn * ux * FT(600); y += sgn * uy * FT(600)
                seq.append((int(x + px_ * lat), int(y + py_ * lat)))
                if not (-FT(600) <= x <= top + FT(600) and -FT(600) <= y <= top + FT(600)): break
            pts = (seq[::-1] + [(int(P[0]), int(P[1]))]) if sgn < 0 else pts + seq
        w = FT(dk.int("w", CREEK_W[1] - CREEK_W[0] + 1) + CREEK_W[0]); buf = FT(CREEK_BUF[0] + 10 * dk.int("b", (CREEK_BUF[1] - CREEK_BUF[0]) // 10 + 1))
        bridges = []
        for r in roads:
            i = 1 if r["axis"] == "h" else 0
            for a, b in zip(pts, pts[1:]):
                if a[i] != b[i] and min(a[i], b[i]) <= r["c"] <= max(a[i], b[i]):
                    t = (r["c"] - a[i]) / (b[i] - a[i]); o = int(a[1 - i] + t * (b[1 - i] - a[1 - i]))
                    if r["lo"] <= o <= r["hi"]: bridges.append((r["id"], (o, r["c"]) if i == 1 else (r["c"], o)))
        out.append({"id": f"creek{k}", "name": ["Rock Creek", "Mud Creek", "Otter Creek"][k % 3], "pts": pts, "chan_w": w, "buffer": buf,
                    "w": w + 2 * buf, "bridges": bridges})
    return out

# ---------- roads ----------
def make_roads(seed, force_town):
    d = rng(seed, "roads"); n = N_MI
    pct = PAVED_PCT[0] + d.int("pct", PAVED_PCT[1] - PAVED_PCT[0] + 1)
    lines = [(ax, i) for ax in "hv" for i in range(n + 1)]
    n_paved = max(1, round(pct * len(lines) / 100))
    # lettered county routes first: one E-W and one N-S interior line, so they cross
    ew = 1 + d.int("ew", n - 1); ns = 1 + d.int("ns", n - 1)
    paved = [("h", ew), ("v", ns)][:n_paved]
    gravel_ns = ["Key Ave", "Lake Ave", "Mallard Ave", "Nature Ave", "Oak Ave"]
    roads = []
    for ax, i in lines:
        t = "paved" if (ax, i) in paved else "gravel"
        if t == "paved": name = f"{'C' if ax == 'h' else 'K'}{10 + 2 * d.int(ax + 'num', 40)}"   # lettered county route
        else: name = f"{100 + 10 * i}th St" if ax == "h" else gravel_ns[i]
        sw = SURF[t][0] + d.int(f"surf{ax}{i}", SURF[t][1] - SURF[t][0] + 1)
        roads.append({"id": f"{'EW' if ax == 'h' else 'NS'}{i}", "name": name, "axis": ax, "c": i * MILE, "lo": 0,
                      "hi": n * MILE, "type": t, "surface_ft": sw, "w": ROW})
    return roads, pct

COOP_OFFICE = DEFAULTS['region.coop_office']
# ---------- town businesses (Construction Research Guy, small-town Iowa 1985-2000; all PLACEHOLDER) ----------
PEOPLE_PER_HOUSE = DEFAULTS['region.people_per_house']
BANDS = DEFAULTS['region.bands']
BIZ_P = DEFAULTS['region.biz_p']
CHURCH2_P = DEFAULTS['region.church2_p']
VACANT_PCT = DEFAULTS['region.vacant_pct']
FADING = DEFAULTS['region.fading']
STOREFRONT = DEFAULTS['region.storefront']
EDGE = DEFAULTS['region.edge']
# edge parcels: (lot w, lot d, bldg w, bldg d, front setback) ft
EDGE_FP = DEFAULTS['region.edge_fp']
COMM_COUNT = DEFAULTS['region.comm_count']

def town_band(pop): return 0 if pop < BANDS[0] else 1 if pop < BANDS[1] else 2

YEARS = DEFAULTS['region.years']
BASE_YEAR = DEFAULTS['region.base_year']
FADE_1985 = DEFAULTS['region.fade_1985']
FADING_TYPES = DEFAULTS['region.fading_types']
BRANCH_PCT = DEFAULTS['region.branch_pct']

def region_year(seed): return rng(seed, "year").choice("year", YEARS)
def fade_factor(year): return 1 + (FADE_1985 - 1) * (BASE_YEAR - year) / (BASE_YEAR - 1985)    # linear; <1 after 1998
def vacant_pct(year): return VACANT_PCT * (2 - fade_factor(year))                              # opposite direction
def branch_pct(year): return BRANCH_PCT[0] + (BRANCH_PCT[1] - BRANCH_PCT[0]) * (year - 1985) / 15
def biz_rates(band, year=BASE_YEAR):
    f = fade_factor(year)
    return {t: min(100.0, p[band] * f) if t in FADING_TYPES else p[band] for t, p in BIZ_P.items()}

def choose_businesses(seed, band, year=BASE_YEAR):
    """Deterministic per seed: each type drawn independently with the band's chance for the world's snapshot year."""
    R = biz_rates(band, year)
    d = rng(seed, "biz"); out = [t for t in BIZ_P if d.chance(t, R[t])]
    if "church" in out and d.chance("church2", CHURCH2_P[band]): out.append("church")
    if "grocery" in out and band == 2 and d.chance("hwy", 50): out[out.index("grocery")] = "grocery (highway)"
    return out

def _wr(F, x0, u, w, h):   # face-local (x0 world x, u = depth from the street edge) -> world rect
    return (x0, F["front_y"] + u, w, h) if F["fa"] == "S" else (x0, F["front_y"] - u - h, w, h)

def _main_biz(typ, x0, F, n, seed, vacant_of=None):
    D = F["d"]; dd = rng(seed, F["run"], "biz", n)
    curb = _wr(F, x0, -FT(18), 0, FT(18))
    def rec(lw, bldg, parking, extras=None, site="main street"):
        c = _wr(F, x0, -FT(18), lw, FT(18))
        return {"id": f"{F['run']}:biz{n}", "type": typ if not vacant_of else f"vacant (former {vacant_of})", "site": site,
                "lot": _wr(F, x0, 0, lw, D), "bldg": bldg, "parking": parking, "curb": c, "canopy": None, "pumps": [],
                "front_y": F["front_y"], "facing": F["fa"], "storefront": typ in STOREFRONT or bool(vacant_of), "vacant": bool(vacant_of)}
    if typ == "gas station":
        lw = FT(dd.choice("gw", [120, 135, 150])); bu = D - FT(10) - FT(40)
        r = rec(lw, _wr(F, x0 + lw - FT(60), bu, FT(60), FT(40)), [_wr(F, x0, 0, lw, bu)], site="blacktop corner")
        np_ = 2 + dd.int("np", 3); r["canopy"] = _wr(F, x0 + FT(15), FT(25), FT(20) * np_ + FT(10), FT(24))
        r["pumps"] = [_wr(F, x0 + FT(25) + k * FT(20), FT(35), FT(4), FT(4)) for k in range(np_)]
        r["spaces"] = 6 + dd.int("sp", 7); r["curb"] = None; return r
    if typ == "bank":
        lw = FT(50); bw = FT(dd.choice("bw", [25, 35, 50])); bd = FT(dd.choice("bd", [80, 90, 100]))
        return rec(lw, _wr(F, x0, 0, bw, bd), [_wr(F, x0, bd, lw, D - bd)] + ([_wr(F, x0 + bw, 0, lw - bw, bd)] if bw < lw else []), site="main street corner")
    if typ in ("post office", "city hall / fire"):
        lw = FT(50 if typ == "post office" else 75); bw, bd = (FT(40), FT(60)) if typ == "post office" else (FT(60), FT(80))
        return rec(lw, _wr(F, x0 + (lw - bw) // 2, FT(10), bw, bd), [_wr(F, x0, 0, lw, FT(10)), _wr(F, x0, FT(10) + bd, lw, D - FT(10) - bd)],
                   site="main street" if F["main"] else "one block off main")
    bd = min(FT(dd.choice("sd", [80, 90, 100, 110, 120])), D - FT(20))                 # attached storefront, zero side setbacks
    return rec(FT(25), _wr(F, x0, 0, FT(25), bd), [_wr(F, x0, bd, FT(25), D - bd)])    # curb out front, alley yard behind

def _church(x0, w, F, n, cnt):
    D = F["d"]; bx = x0 + (w - FT(40)) // 2
    return {"id": f"{F['run']}:church{n}", "type": "church", "site": f"residential corner ({cnt} lots)", "lot": _wr(F, x0, 0, w, D),
            "bldg": _wr(F, bx, FT(25), FT(40), FT(80)), "parsonage": _wr(F, x0 + FT(5), FT(110), FT(28), min(FT(24), D - FT(115))),
            "parking": [_wr(F, x0, 0, (w - FT(40)) // 2, FT(105))], "curb": None, "canopy": None, "pumps": [],
            "front_y": F["front_y"], "facing": F["fa"], "storefront": False, "vacant": False}

# ---------- town ----------
def make_town(seed, roads, rail, force_town, force_pop=None, creeks=()):
    """Rail town where the rail passes the blacktop crossing (grid on the far side of the road, rows along the rail),
    else maybe a no-rail town at the blacktop x blacktop crossing."""
    d = rng(seed, "town")
    ew = next((r for r in roads if r["type"] == "paved" and r["axis"] == "h"), None)
    ns = next((r for r in roads if r["type"] == "paved" and r["axis"] == "v"), None)
    rail_town = bool(rail) and (d.chance("railtown", RAILTOWN_PCT) or bool(force_town))
    if not rail_town and not (ew and ns and (d.chance("town", NORAIL_TOWN_PCT) or force_town)): return None
    cx = FT(d.choice("cx", list(BLOCK_CX))); sw = FT(d.choice("sw", list(STREET_W)))
    al = FT(d.choice("al", list(ALLEY_W))); ld = FT(d.choice("ld", list(LOT_D)))
    cy = 2 * ld + al + sw
    size = d.weighted("size", TOWN_SIZE)
    if rail_town:
        Xc, Yc = rail["site"]; cols, rows = d.choice("grid", GRIDS[size][1]); sd = rail["side"]
        xs = sorted(Xc - sd * k * cx for k in range(cols + 1))
    else:
        Xc, Yc = ns["c"], ew["c"]; cols, rows = d.choice("grid", GRIDS[size][0]); a = cols // 2
        xs = [Xc + k * cx for k in range(-a, cols - a + 1)]
    b = rows // 2
    ys = [Yc + k * cy for k in range(-b, rows - b + 1)]
    names_h = ["Williams St", "Durbend St", "Doring St", "Elm St", "Maple St"]
    streets = []
    for k, x in enumerate(xs):
        if x != Xc: streets.append({"id": f"T-A{k}", "name": f"{k + 1}{'st' if k == 0 else 'nd' if k == 1 else 'rd' if k == 2 else 'th'} Ave",
                                     "axis": "v", "type": "paved", "c": x, "lo": ys[0], "hi": ys[-1], "w": sw, "surface_ft": 22})
    for k, y in enumerate(ys):
        if y != Yc: streets.append({"id": f"T-S{k}", "name": names_h[k % 5], "axis": "h", "type": "paved", "c": y,
                                     "lo": xs[0], "hi": xs[-1], "w": sw, "surface_ft": 22})
    hw = lambda v, c: ROW // 2 if v == c else sw // 2
    sb = {k: FT(v[0] + d.int("sb" + k, v[1] - v[0] + 1)) for k, v in TOWN_SB.items()}
    blocks, alleys, lots, faces = [], [], [], []
    for i in range(cols):
        for j in range(rows):
            outer = i in (0, cols - 1) or j in (0, rows - 1)
            on_main = Yc in (ys[j], ys[j + 1]) and min(abs(xs[i] - Xc), abs(xs[i + 1] - Xc)) < 3 * cx
            if size != "tiny" and outer and not on_main and rng(seed, "ragged", i, j).chance("skip", RAGGED_PCT): continue
            bx0, bx1 = xs[i] + hw(xs[i], Xc), xs[i + 1] - hw(xs[i + 1], Xc)
            by0, by1 = ys[j] + hw(ys[j], Yc), ys[j + 1] - hw(ys[j + 1], Yc)
            if rail_hits(list(creeks), (bx0, by0, bx1 - bx0, by1 - by0)): continue        # town stops at the floodplain
            # ribbon growth: blocks on the main paved roads always grow; others thin out with distance from both roads
            ra = min(abs(xs[i] - Xc), abs(xs[i + 1] - Xc)) / cx; rb = min(abs(ys[j] - Yc), abs(ys[j + 1] - Yc)) / cy
            if min(ra, rb) >= 1 and not rng(seed, "ribbon", i, j).chance("keep", max(15, 100 - RIBBON_DECAY * 6 / max(cols, rows, 6) * (ra + rb - 1))): continue
            bld = (by1 - by0 - al) // 2
            blocks.append({"id": f"b{i}{j}", "ij": (i, j), "rect": (bx0, by0, bx1 - bx0, by1 - by0), "lot_d": bld})
            alleys.append((bx0, by0 + bld, bx1 - bx0, al))
            for half, y0, fa in (("S", by0, "S"), ("N", by1 - bld, "N")):
                front = ys[j] if half == "S" else ys[j + 1]
                edge = (half == "S" and j == 0) or (half == "N" and j == rows - 1)
                if edge and front != Yc and rng(seed, "halfblock", i, j).chance("drop", EDGE_HALF_PCT): continue   # uneven edge depth
                main = front == Yc and Xc in (xs[i], xs[i + 1])
                faces.append({"run": f"town:b{i}{j}:{half}", "y0": y0, "fa": fa, "bx0": bx0, "bx1": bx1, "d": bld,
                              "front_y": by0 if half == "S" else by1, "main": front == Yc,
                              "dist": min(abs(xs[i] - Xc), abs(xs[i + 1] - Xc)), "from_lo": xs[i] == Xc or (xs[i + 1] != Xc and xs[i] < Xc)})
    kept = {b["ij"] for b in blocks}; streets = []; ds = rng(seed, "streets")
    def segs(cells):
        cells = sorted(cells); out = []
        for a, b in cells:
            if out and out[-1][1] >= a: out[-1][1] = max(out[-1][1], b)
            else: out.append([a, b])
        return out
    for k, x in enumerate(xs):
        if x == Xc: continue
        for n_, (lo, hi) in enumerate(segs([(ys[j], ys[j + 1]) for (i, j) in kept if i in (k - 1, k)])):
            if ds.chance(f"v{k}{n_}", FRINGE_PCT) and hi + cy <= ys[-1] + cy: hi += cy // 2       # dead-end fringe stub
            streets.append({"id": f"T-A{k}-{n_}", "name": f"{k + 1}{'st' if k == 0 else 'nd' if k == 1 else 'rd' if k == 2 else 'th'} Ave",
                            "axis": "v", "type": "paved", "c": x, "lo": lo, "hi": hi, "w": sw, "surface_ft": 22})
    for k, y in enumerate(ys):
        if y == Yc: continue
        for n_, (lo, hi) in enumerate(segs([(xs[i], xs[i + 1]) for (i, j) in kept if j in (k - 1, k)])):
            if ds.chance(f"h{k}{n_}", FRINGE_PCT): lo -= cx // 2
            streets.append({"id": f"T-S{k}-{n_}", "name": names_h[k % 5], "axis": "h", "type": "paved", "c": y,
                            "lo": lo, "hi": hi, "w": sw, "surface_ft": 22})
    def face_lots(F, bx0, bx1):
        run, y0, fa, bld = F["run"], F["y0"], F["fa"], F["d"]; out = []
        nb = (bx1 - bx0) // BASE_LOT; rest = (bx1 - bx0) - nb * BASE_LOT
        if nb == 0: return out
        dr = rng(seed, run)
        tr = set()
        if dr.chance("trailers", TRAILER_RUN_PCT):        # seeded trailer cluster on adjacent single lots
            n_tr = TRAILER_RUN[0] + dr.int("ntr", TRAILER_RUN[1] - TRAILER_RUN[0] + 1)
            st = dr.int("st", max(1, nb - n_tr + 1)); tr = set(range(st, min(nb, st + n_tr)))
        takes, used = [], 0
        while used < nb:
            k = 1 if used in tr else min(rng(seed, run, len(takes)).weighted("take", BASE_TAKE), nb - used)
            if k > 1 and any(u in tr for u in range(used, used + k)): k = min(u for u in tr if u >= used) - used
            if k == 1 and used not in tr:
                if takes and (used - 1) not in tr: takes[-1] += 1
                elif used + 1 < nb and (used + 1) not in tr: k = 2; takes.append(2)
                else: takes.append(1); tr.add(used)
            else: takes.append(k)
            used += k
        x = bx0; used = 0
        for n, k in enumerate(takes):
            w = k * BASE_LOT + (rest if used + k == nb else 0)
            px = x if fa == "S" else bx1 - (x - bx0) - w
            out.append({"id": f"{run}:{n}", "w": w, "d": bld, "setting": "town", "kind": "town",
                        "place": {"x0": px, "y0": y0, "facing": fa}, "along": px, "base_lots": k,
                        "house_type": "trailer" if (k == 1 and used in tr) else "stick", "use": "house"})
            x += w; used += k
        return out
    houses = sum(len(face_lots(F, F["bx0"], F["bx1"])) for F in faces)          # pass 1: houses before businesses
    pop = round(houses * PEOPLE_PER_HOUSE); band = town_band(force_pop if force_pop else pop)
    year = region_year(seed); prog = choose_businesses(seed, band, year); db = rng(seed, "bizplace")
    mains = sorted((F for F in faces if F["main"]), key=lambda F: (F["dist"], F["run"]))[:MAIN_FACES]
    off = sorted((F for F in faces if not F["main"] and abs((F["front_y"]) - Yc) < 2 * cy), key=lambda F: (F["dist"], F["run"]))
    for F in faces: F["lo"], F["hi"] = F["bx0"], F["bx1"]
    def take(F, w, far=False):
        if F["hi"] - F["lo"] < w: return None
        lo_end = F["from_lo"] != far
        if lo_end: x0 = F["lo"]; F["lo"] += w
        else: x0 = F["hi"] - w; F["hi"] -= w
        return x0
    commercial, unplaced, n = [], [], 0
    def put(typ, faces_, w, **k):
        nonlocal n
        for F in faces_:
            x0 = take(F, w)
            if x0 is not None: commercial.append(_main_biz(typ, x0, F, n, seed, **k)); n += 1; return True
        unplaced.append(typ); return False
    if "gas station" in prog: put("gas station", mains[:1], FT(150))
    if "bank" in prog and put("bank", mains[1:2] + mains, FT(50)):
        bk = commercial[-1]; bk["branch"] = db.chance("branch", branch_pct(year))
        if bk["branch"]: bk["type"] = "bank (branch)"
    shops = [t for t in prog if t in STOREFRONT]
    nvac = sum(db.chance(f"vac{i}", vacant_pct(year)) for i in range(len(shops) + 2)) if shops or band > 0 else 0
    for t in shops: put(t, mains[1:] + mains[:1], FT(25))
    for i in range(nvac): put("vacant", mains[1:] + mains[:1], FT(25), vacant_of=db.choice(f"vf{i}", FADING))
    for t in ("post office", "city hall / fire"):
        if t in prog: put(t, mains + off, FT(50 if t == "post office" else 75))
    res = [F for F in faces if not F["main"]]
    for c in range(prog.count("church")):
        cnt = 2 + db.int(f"cl{c}", 3)
        for F in sorted(res, key=lambda F: H(seed, "church", c, F["run"])):
            if F.get("church"): continue
            x0 = take(F, cnt * BASE_LOT, far=True)
            if x0 is not None: F["church"] = True; commercial.append(_church(x0, cnt * BASE_LOT, F, c, cnt)); break
        else: unplaced.append("church")
    for F in faces: lots.extend(face_lots(F, F["lo"], F["hi"]))
    if "funeral home" in prog:                              # converted house near main street
        cand = [L for L in lots if L["house_type"] == "stick" and any(L["id"].startswith(F["run"] + ":") for F in mains + off)]
        if cand:
            L = cand[db.int("fh", len(cand))]; L["use"] = "funeral home"
            commercial.append({"id": L["id"] + ":fh", "type": "funeral home", "site": "converted house near main street",
                               "lot": world_rect(L["place"], L["w"], L["d"]), "bldg": None, "parking": [], "curb": None,
                               "canopy": None, "pumps": [], "storefront": False, "vacant": False, "house_lot": L["id"]})
        else: unplaced.append("funeral home")
    edge_want = [t for t in prog if t in EDGE or t == "grocery (highway)"]
    bx = [b["rect"] for b in blocks]
    box = (min(r[0] for r in bx) - sw, min(r[1] for r in bx) - sw, 0, 0)
    box = (box[0], box[1], max(r[0] + r[2] for r in bx) + sw - box[0], max(r[1] + r[3] for r in bx) + sw - box[1])
    return {"Xc": Xc, "Yc": Yc, "cols": cols, "rows": rows, "cx": cx, "cy": cy, "street_w": sw, "alley_w": al,
            "setbacks": sb, "blocks": blocks, "alleys": alleys, "streets": streets, "lots": lots, "box": box, "rail_town": rail_town, "commercial": commercial,
            "year": year, "size": size, "pop_est": pop, "houses_est": houses, "band": band, "program": prog, "edge_want": edge_want, "unplaced": unplaced}

def place_rail_features(seed, town, rail, occupied, bar=None):
    """Rail towns only: depot between road and track, elevator 3-6 ac across the track with a siding beside it."""
    d = rng(seed, "rail"); sd, rx, Yc = rail["side"], rail["x"], town["Yc"]
    depot = (int(rx - sd * (RAIL_ROW // 2 - FT(25)) - FT(12)), Yc + ROW // 2 + FT(100), FT(24), FT(80))
    while True:
        w, h = FT(d.choice("ew", list(range(350, 451, 50)))), FT(d.choice("eh", list(range(400, 551, 50))))
        if ELEV_AC[0] <= acres(w, h) <= ELEV_AC[1]: break
        d = Draw(H(d.seed, "again"))
    ex = rx + sd * (RAIL_ROW // 2 + FT(40)); ex = ex if sd > 0 else ex - w
    rect = None
    for y0 in (Yc - ROW // 2 - FT(60) - h, Yc + ROW // 2 + FT(60), Yc - ROW // 2 - FT(700) - h):
        r = (ex, y0, w, h)
        if not rail_hits(bar or rail, r) and not any(overlaps(r, o) for o in occupied): rect = r; break
    if rect is None: return None, depot, None
    sx = rx + sd * (RAIL_ROW // 2 + FT(15))
    siding = [(sx, rect[1] - FT(300)), (sx, rect[1] + h + FT(300))]
    return {"kind": "elevator", "name": "Co-op elevator", "rect": rect, "acres": acres(w, h)}, depot, siding

# ---------- mobile home park ----------
def _park_layout(d):
    """Local frame: x along the public road, y away from it (y=0 = ROW edge). Comb = dead-end lanes off the road;
    U = two legs off the road joined by a back lane around a green/pond."""
    pattern = "U" if d.chance("u", 40) else "comb"
    P, LW, SD = FT(d.choice("p", PARK_PITCH)), FT(d.choice("lw", PARK_LANE)), FT(PARK_SITE_D)
    N = PARK_PADS[0] + d.int("n", PARK_PADS[1] - PARK_PADS[0] + 1)
    wb = [k for k in ("back", "left", "right") if d.chance("wb" + k, 60)] or ["back"]
    L = FT(PARK_WB) if "left" in wb else FT(10); F = FT(PARK_FRONT)
    lanes, rows = [], []            # rows: (lane_rect, side +1/-1 in x, y0, count)
    if pattern == "comb":
        k = 1 if N <= 18 else 2 if N <= 32 else 3; per = -(-N // (2 * k))
        ll = F + per * P + FT(20); x = L
        for i in range(k):
            lane = (x + SD, 0, LW, ll); lanes.append(lane)
            rows += [(lane, -1, F, per), (lane, 1, F, per)]; x += 2 * SD + LW
        W = x; Dl = ll
    else:
        per = -(-N // 2); ll = F + per * P + FT(20); green = FT(d.choice("g", [100, 140, 180]))
        a = (L + SD, 0, LW, ll + LW); b = (L + SD + LW + green, 0, LW, ll + LW)
        back = (a[0], ll, b[0] + LW - a[0], LW); lanes = [a, b, back]
        rows = [(a, -1, F, per), (b, 1, F, per)]; W = b[0] + LW + SD
        pond = (a[0] + LW + FT(20), F, green - FT(40), ll - F - FT(20))
        Dl = ll + LW
    W += FT(PARK_WB) if "right" in wb else FT(10)
    D = Dl + (FT(PARK_WB) if "back" in wb else FT(10)) + (SD if pattern == "U" else 0) * 0
    pads, tw = [], FT(d.choice("tw", [14, 16]))
    for lane, side, y0, n in rows:
        for j in range(n):
            if len(pads) >= N: break
            y = y0 + j * P; tl = FT(60 + 2 * d.int(f"tl{len(pads)}", 9))
            ex = lane[0] + lane[2] if side > 0 else lane[0]              # lane edge
            x0 = ex + FT(6) if side > 0 else ex - FT(6) - tl
            trailer = (x0, y, tl, tw)                                  # long axis perpendicular to the lane
            px0 = ex if side > 0 else ex - FT(20)
            parking = (px0, y + tw, FT(20), FT(10))                   # parking pad beside the trailer, on the lane
            pads.append({"trailer": trailer, "parking": parking, "lane": lanes.index(lane)})
    wbr = []
    if "back" in wb: wbr.append((0, D - FT(PARK_WB), W, FT(PARK_WB)))
    if "left" in wb: wbr.append((0, 0, FT(PARK_WB), D))
    if "right" in wb: wbr.append((W - FT(PARK_WB), 0, FT(PARK_WB), D))
    return {"pattern": pattern, "W": W, "D": D, "lanes": lanes, "pads": pads, "windbreak": wbr, "windbreak_sides": wb,
            "pond": pond if pattern == "U" else None, "pitch": P, "lane_w": LW}

def _edge_slot(town, ew, lw, ld, rail, occupied, order=(-1, 1)):
    b = town["box"]; top = N_MI * MILE
    for step in range(30):
        for sx in order if order != (2, 1) else (1, -1):
            for sy in (1, -1):
                gap = FT(40) + step * FT(100)
                x0 = b[0] - gap - lw if sx < 0 else b[0] + b[2] + gap
                y0 = ew["c"] + ROW // 2 if sy > 0 else ew["c"] - ROW // 2 - ld
                lot = (x0, y0, lw, ld)
                if x0 < 0 or x0 + lw > top or y0 < 0 or y0 + ld > top: continue
                if rail_hits(rail, lot) or any(overlaps(lot, o) for o in occupied): continue
                return lot, (lambda x, u, w, h, x0=x0, y0=y0, sy=sy: (x0 + x, y0 + u, w, h) if sy > 0 else (x0 + x, y0 + ld - u - h, w, h)), sy
    return None

def place_edge_biz(seed, town, roads, rail, occupied):
    """Edge-of-town / highway businesses fronting the E-W blacktop just past the town edge (CRG siting; PLACEHOLDER sizes)."""
    ew = next(r for r in roads if r["type"] == "paved" and r["axis"] == "h"); b = town["box"]; top = N_MI * MILE
    for k, typ in enumerate(town["edge_want"]):
        lw, ld, bw, bd, sb = (FT(v) for v in EDGE_FP[typ]); done = False
        for step in range(12):
            for sx in (-1, 1):
                for sy in (1, -1):
                    gap = FT(40) + step * FT(100)
                    x0 = b[0] - gap - lw if sx < 0 else b[0] + b[2] + gap
                    y0 = ew["c"] + ROW // 2 if sy > 0 else ew["c"] - ROW // 2 - ld
                    lot = (x0, y0, lw, ld)
                    if x0 < 0 or x0 + lw > top or y0 < 0 or y0 + ld > top: continue
                    if rail_hits(rail, lot) or any(overlaps(lot, o) for o in occupied): continue
                    L = lambda x, u, w, h: (x0 + x, y0 + u, w, h) if sy > 0 else (x0 + x, y0 + ld - u - h, w, h)
                    bx = (lw - bw) // 2
                    rec = {"id": f"edge{k}", "type": typ, "site": "edge of town on " + ew["name"], "lot": lot, "bldg": L(bx, sb, bw, bd),
                           "parking": [L(0, 0, lw, sb)] if typ in ("implement dealer", "lumberyard") else [L(max(0, bx - FT(20)), 0, min(lw, bw + FT(40)), sb)],
                           "curb": None, "canopy": None, "pumps": [], "storefront": False, "vacant": False, "front_y": ew["c"] + sy * (ROW // 2)}
                    if typ == "school": rec["field"] = L(FT(50), sb + bd + FT(30), FT(300), min(FT(300), ld - sb - bd - FT(50)))
                    if typ in ("implement dealer", "lumberyard"): rec["yard"] = "gravel"
                    town["commercial"].append(rec); occupied.append(lot); done = True; break
                if done: break
            if done: break
        if not done: town["unplaced"].append(typ)

def park_condition(town, pads):
    """User rule: park only if trailers are the town's only house style, or stick-built >= 2 x (in-block trailers + park pads)."""
    st = sum(l["house_type"] == "stick" for l in town["lots"]); tr = sum(l["house_type"] == "trailer" for l in town["lots"])
    ok_a = st == 0; ok_b = st >= 2 * (tr + pads)
    why = "trailers are the only house style" if ok_a else (f"stick {st} >= 2 x (trailers {tr} + pads {pads})" if ok_b
          else f"stick {st} < 2 x (trailers {tr} + pads {pads})")
    return {"ok": ok_a or ok_b, "stick": st, "trailers_in_block": tr, "pads": pads, "reason": why}

def make_park(seed, town, roads, rail, occupied, force=None):
    d = rng(seed, "park")
    if not town or not (d.chance("has", PARK_PCT) if force is None else force): return None
    lay = _park_layout(d)
    chk = park_condition(town, len(lay["pads"]))
    town["park_check"] = chk
    if not chk["ok"]: return None
    ew = next(r for r in roads if r["type"] == "paved" and r["axis"] == "h"); b = town["box"]
    W, D = lay["W"], lay["D"]; top = N_MI * MILE
    cands = []
    for gap in (FT(60), FT(300), FT(600)):
        for sx in ((-1, 1) if d.chance("west", 50) else (1, -1)):
            for sy in ((1, -1) if d.chance("north", 50) else (-1, 1)):
                x0 = b[0] - gap - W if sx < 0 else b[0] + b[2] + gap
                cands.append((x0, sy))
    for x0, sy in cands:
        y0 = ew["c"] + ROW // 2 if sy > 0 else ew["c"] - ROW // 2 - D
        rect = (x0, y0, W, D)
        if x0 < 0 or x0 + W > top or y0 < 0 or y0 + D > top: continue
        if rail_hits(rail, rect) or any(overlaps(rect, o) for o in occupied): continue
        def w(r):   # local -> world (x same direction; y flipped when the park is south of the road)
            return (x0 + r[0], y0 + r[1], r[2], r[3]) if sy > 0 else (x0 + r[0], y0 + D - r[1] - r[3], r[2], r[3])
        return {"rect": rect, "road": ew["id"], "road_name": ew["name"], "pattern": lay["pattern"], "pitch": lay["pitch"],
                "lanes": [w(r) for r in lay["lanes"]], "windbreak": [w(r) for r in lay["windbreak"]], "windbreak_sides": lay["windbreak_sides"],
                "pond": w(lay["pond"]) if lay["pond"] else None, "acres": acres(W, D),
                "pads": [{"trailer": w(p["trailer"]), "parking": w(p["parking"]), "lane": p["lane"]} for p in lay["pads"]]}
    return None

# ---------- region ----------
def make_region(seed, force_town=False, overrides=None, force_rail=None, force_park=None, force_pop=None):
    roads, pct = make_roads(seed, force_town)
    rail = make_rail(seed, roads, force_rail)
    creeks = make_creeks(seed, roads)
    bar = [rail] + creeks if rail else list(creeks)     # rail corridor + floodplains: no lots or parcels
    town = make_town(seed, roads, rail, force_town, force_pop, creeks)
    d = rng(seed, "region")
    clear = FT(CLEAR[0] + d.int("clear", CLEAR[1] - CLEAR[0] + 1))
    allroads = roads + (town["streets"] if town else [])
    occupied = [road_rect(r) for r in allroads]
    parcels, depot, siding = [], None, None
    if town:
        occupied.append(town["box"])
        if town["rail_town"]:
            el, depot, siding = place_rail_features(seed, town, rail, occupied, bar)
            if el: parcels.append(el); occupied.append(el["rect"])
            if el:   # co-op office inside the elevator parcel, at the corner nearest the blacktop, with a parking pad
                e = el["rect"]; ow, oh = COOP_OFFICE; near_top = abs(e[1] + e[3] - town["Yc"]) < abs(e[1] - town["Yc"])
                ox = e[0] + FT(20); oy = e[1] + e[3] - FT(20) - oh if near_top else e[1] + FT(20)
                el["office"] = (ox, oy, ow, oh)
                el["office_parking"] = (ox + ow, oy, FT(40), oh)
            if el:   # industry (feed mill / scales) lining the corridor beside the elevator
                e = el["rect"]; iw, ih = INDUSTRY
                for iy in (e[1] + e[3] + FT(40), e[1] - FT(40) - ih):
                    ir = (e[0], iy, iw, ih)
                    if not rail_hits(bar, ir) and not any(overlaps(ir, o) for o in occupied):
                        parcels.append({"kind": "industry", "name": "Feed mill / scales", "rect": ir, "acres": acres(iw, ih)}); occupied.append(ir); break
    if town and not town["rail_town"] and "elevator" in town["program"]:   # truck-served co-op elevator at the research rate
        ew_ = next(r for r in roads if r["type"] == "paved" and r["axis"] == "h")
        slot = _edge_slot(town, ew_, FT(400), FT(450), bar, occupied, (2, 1))
        if slot:
            lot, L, sy = slot; ow, oh = COOP_OFFICE
            el = {"kind": "elevator", "name": "Co-op elevator (truck)", "rect": lot, "acres": acres(lot[2], lot[3]), "truck": True,
                  "office": L(FT(20), FT(20), ow, oh), "office_parking": L(FT(20) + ow, 0, FT(40), FT(20) + oh)}
            parcels.append(el); occupied.append(lot)
        else: town["unplaced"].append("elevator")
    if town: place_edge_biz(seed, town, roads, bar, occupied)
    park = make_park(seed, town, roads, bar, occupied, force_park)
    if park: occupied.append(park["rect"])
    ew = next((r for r in roads if r["type"] == "paved" and r["axis"] == "h"), None)
    # cemetery on the E-W blacktop just outside town (Struble pattern), or on any road
    cr = ew or roads[1]
    base_x = (town["box"][0] if town else cr["c"] + MILE) - FT(400 + 100 * d.int("cem", 9)) - CEM[0]
    cem = (base_x, cr["c"] + ROW // 2, *CEM) if cr["axis"] == "h" else None
    if cem and not rail_hits(bar, cem) and not any(overlaps(cem, o) for o in occupied):
        parcels.append({"kind": "cemetery", "name": "St. Joseph Cemetery", "rect": cem, "acres": acres(*CEM), "road": cr["name"]})
        occupied.append(cem)
    # fuel station on a blacktop corner outside town
    paved = [r for r in roads if r["type"] == "paved"]
    crossings = [(p, q) for p in paved for q in roads if q["axis"] != p["axis"] and not (town and q["type"] == "paved")]
    gas = next((c for c in town["commercial"] if c["type"] == "gas station"), None) if town else None
    if gas:   # the region's fuel station IS the town gas station (no duplicate)
        parcels.append({"kind": "fuel", "name": "Gas station (town)", "rect": gas["lot"], "acres": acres(gas["lot"][2], gas["lot"][3]),
                        "road": ew["name"] if ew else "", "in_town": True})
    elif crossings:
        p, q = crossings[d.int("fuel", len(crossings))]
        X, Y = (q["c"], p["c"]) if p["axis"] == "h" else (p["c"], q["c"])
        sx, sy = (1, 1) if d.chance("fq1", 50) else (-1, 1)
        if X == 0: sx = 1
        if X == N_MI * MILE: sx = -1
        if Y == N_MI * MILE: sy = -1
        r = (X + ROW // 2 if sx > 0 else X - ROW // 2 - FUEL[0], Y + ROW // 2 if sy > 0 else Y - ROW // 2 - FUEL[1], *FUEL)
        if not any(overlaps(r, o) for o in occupied) and not rail_hits(bar, r):
            parcels.append({"kind": "fuel", "name": "Fuel / co-op station", "rect": r, "acres": acres(*FUEL), "road": p["name"]})
            occupied.append(r)
    # country church on a gravel road
    gr = [r for r in roads if r["type"] == "gravel" and 0 < r["c"] < N_MI * MILE]
    for t in range(10):
        dd = rng(seed, "church", t)
        w, h = FT(dd.choice("w", list(range(200, 301, 25)))), FT(dd.choice("h", list(range(250, 401, 50))))
        if not CHURCH_AC[0] <= acres(w, h) <= CHURCH_AC[1]: continue
        r0 = gr[dd.int("road", len(gr))]; m = dd.int("m", N_MI)
        pos = m * MILE + clear + ROW // 2 + dd.int("pos", 10) * FT(100)
        rect = (pos, r0["c"] + ROW // 2, w, h) if r0["axis"] == "h" else (r0["c"] + ROW // 2, pos, h, w)
        if not any(overlaps(rect, o) for o in occupied) and not rail_hits(bar, rect):
            parcels.append({"kind": "church", "name": "Country church", "rect": rect, "acres": acres(w, h), "road": r0["name"]})
            occupied.append(rect); break
    # rural lots: farmsteads ~1.2 per road mile, plus acreages
    raw = []
    for r in roads:
        for m in range(N_MI):
            dm = rng(seed, r["id"], m)
            items = [("f", k) for k in range(dm.weighted("farms", FARM_PER_MILE))] + ([("a", 0)] if dm.chance("acre", ACREAGE_PCT) else [])
            for typ, k in items:
                lid = f"{r['id']}:{m}:{typ}{k}"
                for t in range(8):
                    dl = rng(seed, lid, t)
                    if typ == "f": W, Dp, lo_ac, hi_ac = FT(dl.choice("w", list(FARM_W))), FT(dl.choice("d", list(FARM_D))), *FARM_AC
                    else: W, Dp, lo_ac, hi_ac = FT(dl.choice("w", list(ACRE_W))), FT(dl.choice("d", list(ACRE_D))), *ACRE_AC
                    if not lo_ac <= acres(W, Dp) <= hi_ac: continue
                    side = 1 if dl.chance("side", 50) else -1
                    if (r["c"] == 0 and side < 0) or (r["c"] == N_MI * MILE and side > 0): side = -side
                    a0 = m * MILE + ROW // 2 + clear; a1 = (m + 1) * MILE - ROW // 2 - clear - W
                    pos = a0 + dl.int("pos", max(1, (a1 - a0) // FT(10))) * FT(10)
                    if r["axis"] == "h":
                        place = {"x0": pos, "y0": r["c"] + ROW // 2 if side > 0 else r["c"] - ROW // 2 - Dp, "facing": "S" if side > 0 else "N"}
                    else:
                        place = {"x0": r["c"] + ROW // 2 if side > 0 else r["c"] - ROW // 2 - Dp, "y0": pos, "facing": "W" if side > 0 else "E"}
                    wr = world_rect(place, W, Dp)
                    if wr[0] < 0 or wr[1] < 0 or wr[0] + wr[2] > N_MI * MILE or wr[1] + wr[3] > N_MI * MILE: continue
                    if rail_hits(bar, wr) or any(overlaps(wr, o) for o in occupied): continue
                    raw.append({"id": lid, "w": W, "d": Dp, "road": r, "setting": "rural", "kind": "farmstead" if typ == "f" else "acreage",
                                "place": place, "along": pos})
                    occupied.append(wr); break
    if town:
        byid = {x["id"]: x for x in allroads}
        for L in town["lots"]:
            wr = world_rect(L["place"], L["w"], L["d"])
            if rail_hits(bar, wr): continue
            # address street = the street the lot faces
            fx, fy = to_world(L["place"], L["w"], L["d"], L["w"] // 2, -FT(2))
            L["road"] = next(x for x in allroads if inside((fx, fy), road_rect(x)))
            raw.append(L)
    year = region_year(seed)
    lots = []
    for L in raw:
        if overrides and L["id"] in overrides: L["w"] = FT(overrides[L["id"]])
        W, Dp, pl = L["w"], L["d"], L["place"]
        others = [x for x in allroads if x is not L["road"]]
        corner, side_street = None, None
        for side, x in (("left", -FT(2)), ("right", W + FT(2))):
            hit = [x2 for x2 in others if inside(to_world(pl, W, Dp, x, Dp // 4), road_rect(x2))]
            if hit: corner, side_street = side, hit[0]["name"]
        spec = make_lot(L["id"], W / FT(1), seed, Dp / FT(1), corner_side=corner, year=year, address_street=L["road"]["name"],
                        side_street=side_street, road_type=L["road"]["type"], setting=L["setting"])
        ov = {"kind": L["kind"]}
        dl = rng(seed, L["id"], "sb")
        if L["kind"] == "town":
            sb = town["setbacks"]
            ov.update(front_setback=sb["front"], front_min=FT(TOWN_SB["front"][0]), rear_setback=sb["rear"], rear_min=FT(TOWN_SB["rear"][0]),
                      acc_rear=FT(5 + dl.int("ar", 6)), base_lots=L["base_lots"], house_type=L["house_type"], alley_rear=True, use=L.get("use", "house"))
            for k in ("left", "right"):
                c = corner == k
                ov[f"side_{k}"] = sb["corner"] if c else sb["side"]
                ov[f"side_{k}_min"] = FT(TOWN_SB["corner" if c else "side"][0])
        else:
            # grove on whichever lot edges face north and west (never the road side)
            gs = []
            for k, (x, y) in (("left", (-FT(10), Dp // 2)), ("right", (W + FT(10), Dp // 2)), ("rear", (W // 2, Dp + FT(10)))):
                mx, my = to_world(pl, W, Dp, W // 2, Dp // 2); px, py = to_world(pl, W, Dp, x, y)
                if py - my > abs(px - mx) or mx - px > abs(py - my): gs.append(k)
            back = FT(ACRE_BACK) if L["kind"] == "acreage" else min(FT(FARM_BACK[0] + 10 * dl.int("back", 21)), Dp - FT(36 + 260))
            back = max(back, FT(FARM_BACK[0])) if L["kind"] == "farmstead" else back
            ov.update(front_setback=back, front_min=back, rear_setback=spec["depth"] // 10, rear_min=spec["depth"] // 20)
            if L["kind"] == "farmstead":
                ov["grove_sides"] = gs
                for k in ("left", "right"):
                    if k in gs: ov[f"side_{k}"] = ov[f"side_{k}_min"] = max(spec[f"side_{k}"], GROVE_W + FT(20))
                if "rear" in gs: ov["rear_setback"] = ov["rear_min"] = GROVE_W + FT(20)
        spec.update(ov)
        lots.append({"spec": spec, "place": pl, "road_id": L["road"]["id"], "along": L["along"]})
    lots.sort(key=lambda l: (l["road_id"], l["along"], l["spec"]["id"]))
    paved_len = sum(r["hi"] - r["lo"] for r in roads if r["type"] == "paved"); tot = sum(r["hi"] - r["lo"] for r in roads)
    return {"seed": seed, "creeks": creeks, "park": park, "rail": rail, "roads": allroads, "grid_roads": roads, "town": town, "lots": lots, "parcels": parcels,
            "depot": depot, "siding": siding, "clear": clear, "paved_pct_drawn": pct, "paved_share": paved_len / tot,
            "road_miles": tot / MILE}

# ---------- rule 35 ----------
def signature(plan):
    P = plan.program
    att = [r for r in plan.rooms if not (r.kind == "garage" and plan.site.get("detached_garage"))]
    fy = min(r.y for r in att); x0 = min(r.x for r in att); x1 = max(r.x + r.w for r in att)
    wins = tuple(w.ro_w // IN(12) for w in sorted((w for w in plan.windows if w.y1 == w.y2 == fy), key=lambda w: w.x1))
    return {"roof": P["roof"], "roof_h": P["roof_h"], "front_w": (x1 - x0) // FT(4),
            "silhouette": ((max(r.y + r.h for r in att) - fy) // FT(4), bool(plan.site.get("porch"))),
            "front_windows": min(wins, wins[::-1]),
            "garage_pos": {"double": "front", "single": "front", "detached": "rear", "none": "none"}[P["garage_built"]],
            "siding": P["siding"]}

def check35(plan, prev):
    s = signature(plan); worst = None
    for pid, p in prev:
        diff = sum(s[k] != v for k, v in signature(p).items())
        if diff < 3 and (worst is None or diff < worst[1]): worst = (pid, diff)
    if worst: return {"35": ("fail", f"only {worst[1]} of 7 features differ from {worst[0]}")}
    return {"35": ("pass", f"differs in >=3 features from {len(prev)} earlier lot(s) on this road")}

def spawn_region(region, rule35=True, max_attempts=25):
    out, done = {}, []
    for L in region["lots"]:
        prev = [(d["spec"]["id"], out[d["spec"]["id"]]["plan"]) for d in done
                if d["road_id"] == L["road_id"] and out[d["spec"]["id"]]["plan"]][-2:]
        extra = (lambda p, prev=prev: check35(p, prev)) if rule35 else None
        plan, rep, att, fails = spawn(L["spec"], max_attempts, extra=extra)
        out[L["spec"]["id"]] = {"plan": plan, "report": rep, "attempts": att, "fails": fails}
        done.append(L)
    return out

from settings import configurable as _configurable
make_region = _configurable(make_region)
spawn_region = _configurable(spawn_region)
