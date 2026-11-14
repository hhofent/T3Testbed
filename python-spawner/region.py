"""Region stage, rural Iowa (Craig / Struble pattern): region seed -> 3 x 3 mile section grid of gravel roads,
lettered county blacktops (~25% of lines), a rare town at a blacktop crossing with an active rail line,
depot, elevator and siding; farmsteads, acreages, cemetery, church and fuel station; then the building spawner.

Lot seeds are hash(regionSeed, lotId); lot ids come from road / mile / slot, never from neighbors.
CRG = Construction Research Guy value (ranges are seeded inside the range). PLACEHOLDER = still needs a source.
"""
from settings import DEFAULTS, number, option
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
    hw = r.get("w", ROW) // number('region.invariants_numbers', 'road_rect_63_28')
    return (r["lo"], r["c"] - hw, r["hi"] - r["lo"], number('region.invariants_numbers', 'road_rect_64_53') * hw) if r["axis"] == "h" else (r["c"] - hw, r["lo"], number('region.invariants_numbers', 'road_rect_64_109') * hw, r["hi"] - r["lo"])
def inside(pt, rect): x, y, w, h = rect; return x <= pt[number('region.invariants_numbers', 'inside_65_56')] <= x + w and y <= pt[number('region.invariants_numbers', 'inside_65_80')] <= y + h
def overlaps(a, b): return a[number('region.invariants_numbers', 'overlaps_66_29')] < b[number('region.invariants_numbers', 'overlaps_66_36')] + b[number('region.invariants_numbers', 'overlaps_66_43')] and b[number('region.invariants_numbers', 'overlaps_66_52')] < a[number('region.invariants_numbers', 'overlaps_66_59')] + a[number('region.invariants_numbers', 'overlaps_66_66')] and a[number('region.invariants_numbers', 'overlaps_66_75')] < b[number('region.invariants_numbers', 'overlaps_66_82')] + b[number('region.invariants_numbers', 'overlaps_66_89')] and b[number('region.invariants_numbers', 'overlaps_66_98')] < a[number('region.invariants_numbers', 'overlaps_66_105')] + a[number('region.invariants_numbers', 'overlaps_66_112')]
def acres(w, d): return w * d / SF / number('region.invariants_numbers', 'acres_67_37')

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
    x0, y0, x1, y1 = r[number('region.invariants_numbers', '_seg_rect_80_23')], r[number('region.invariants_numbers', '_seg_rect_80_29')], r[number('region.invariants_numbers', '_seg_rect_80_35')] + r[number('region.invariants_numbers', '_seg_rect_80_42')], r[number('region.invariants_numbers', '_seg_rect_80_48')] + r[number('region.invariants_numbers', '_seg_rect_80_55')]
    dx, dy = b[number('region.invariants_numbers', '_seg_rect_81_15')] - a[number('region.invariants_numbers', '_seg_rect_81_22')], b[number('region.invariants_numbers', '_seg_rect_81_28')] - a[number('region.invariants_numbers', '_seg_rect_81_35')]; t0, t1 = number('region.invariants_numbers', '_seg_rect_81_48'), number('region.invariants_numbers', '_seg_rect_81_53')
    for p, q in ((-dx, a[number('region.invariants_numbers', '_seg_rect_82_25')] - x0), (dx, x1 - a[number('region.invariants_numbers', '_seg_rect_82_47')]), (-dy, a[number('region.invariants_numbers', '_seg_rect_82_60')] - y0), (dy, y1 - a[number('region.invariants_numbers', '_seg_rect_82_82')])):
        if p == number('region.invariants_numbers', '_seg_rect_83_16'):
            if q < number('region.invariants_numbers', '_seg_rect_84_19'): return False
        else:
            t = q / p
            if p < number('region.invariants_numbers', '_seg_rect_87_19'): t0 = max(t0, t)
            else: t1 = min(t1, t)
            if t0 > t1: return False
    return True

def rail_hits(rail, rect, pad=number('region.invariants_numbers', 'rail_hits_92_30')):
    if isinstance(rail, list): return any(rail_hits(r, rect, pad) for r in rail)
    if not rail: return False
    e = rail["w"] // number('region.invariants_numbers', 'rail_hits_95_21') + pad
    R = (rect[number('region.invariants_numbers', 'rail_hits_96_14')] - e, rect[number('region.invariants_numbers', 'rail_hits_96_27')] - e, rect[number('region.invariants_numbers', 'rail_hits_96_40')] + number('region.invariants_numbers', 'rail_hits_96_45') * e, rect[number('region.invariants_numbers', 'rail_hits_96_57')] + number('region.invariants_numbers', 'rail_hits_96_62') * e)
    return any(_seg_rect(a, b, R) for a, b in zip(rail["pts"], rail["pts"][number('region.invariants_numbers', 'rail_hits_97_75'):]))

def make_rail(seed, roads, force=None):
    """Rail stage (after roads): mostly straight; runs parallel just off one N-S road's ROW past the E-W blacktop,
    then leans off diagonally to the region edges, crossing roads at grade."""
    if not option("stages.rail"): return None
    d = rng(seed, "railroute")
    if not (d.chance("has", RAIL_PCT) if force is None else force): return None
    top = N_MI * MILE
    ew = next(r for r in roads if r["type"] == "paved" and r["axis"] == "h")
    along = d.choice("along", [r for r in roads if r["axis"] == "v" and number('region.make_rail_numbers', 'make_rail_106_72') < r["c"] < top])
    side = number('region.make_rail_numbers', 'make_rail_107_11') if d.chance("side", number('region.make_rail_numbers', 'make_rail_107_33')) else -number('region.make_rail_numbers', 'make_rail_107_43')
    x = along["c"] + side * (ROW // number('region.make_rail_numbers', 'make_rail_108_36') + RAIL_GAP + RAIL_ROW // number('region.make_rail_numbers', 'make_rail_108_63'))
    ya = ew["c"] - FT(d.choice("l1", RAIL_PARALLEL)); yb = ew["c"] + FT(d.choice("l2", RAIL_PARALLEL))
    k = number('region.make_rail_numbers', 'make_rail_110_8') / math.tan(math.radians(RAIL_ANGLE[number('region.make_rail_numbers', 'make_rail_110_45')] + d.int("ang", RAIL_ANGLE[number('region.make_rail_numbers', 'make_rail_110_74')] - RAIL_ANGLE[number('region.make_rail_numbers', 'make_rail_110_90')] + number('region.make_rail_numbers', 'make_rail_110_95'))))
    lean = number('region.make_rail_numbers', 'make_rail_111_11') if d.chance("lean", number('region.make_rail_numbers', 'make_rail_111_33')) else -number('region.make_rail_numbers', 'make_rail_111_43')
    pts = [(int(x - lean * ya * k), number('region.make_rail_numbers', 'make_rail_112_36')), (x, ya), (x, yb), (int(x + lean * (top - yb) * k), top)]
    cross = set()
    for r in roads:
        for a, b in zip(pts, pts[number('region.make_rail_numbers', 'make_rail_115_33'):]):
            i = number('region.make_rail_numbers', 'make_rail_116_16') if r["axis"] == "h" else number('region.make_rail_numbers', 'make_rail_116_43')
            if a[i] != b[i] and min(a[i], b[i]) <= r["c"] <= max(a[i], b[i]):
                t = (r["c"] - a[i]) / (b[i] - a[i]); o = int(a[number('region.make_rail_numbers', 'make_rail_118_63') - i] + t * (b[number('region.make_rail_numbers', 'make_rail_118_79') - i] - a[number('region.make_rail_numbers', 'make_rail_118_90') - i]))
                if number('region.make_rail_numbers', 'make_rail_119_19') <= o <= top: cross.add((r["id"], (o, r["c"]) if i == number('region.make_rail_numbers', 'make_rail_119_74') else (r["c"], o)))
    return {"pts": pts, "w": RAIL_ROW, "along": along["id"], "along_c": along["c"], "x": x, "side": side,
            "parallel": (ya, yb), "crossings": sorted(cross), "site": (along["c"], ew["c"])}

# ---------- creeks (PLACEHOLDER) ----------
CREEK_N = DEFAULTS['region.creek_n']
CREEK_W = DEFAULTS['region.creek_w']
CREEK_BUF = DEFAULTS['region.creek_buf']

def make_creeks(seed, roads):
    """Meandering, roughly diagonal polylines (Lyon County refs). The first passes 900-2200 ft off the blacktop crossing."""
    if not option("stages.creeks"): return []
    d = rng(seed, "creek"); top = N_MI * MILE; out = []
    ew = next((r for r in roads if r["type"] == "paved" and r["axis"] == "h"), next(r for r in roads if r["axis"] == "h")); ns = next((r for r in roads if r["type"] == "paved" and r["axis"] == "v"), None)
    ax_, ay_ = (ns["c"] if ns else top // number('region.make_creeks_numbers', 'make_creeks_132_42')), ew["c"]
    for k in range(d.weighted("n", CREEK_N)):
        dk = rng(seed, "creek", k)
        th = math.radians(dk.choice("th", [number('region.make_creeks_numbers', 'make_creeks_135_43'), number('region.make_creeks_numbers', 'make_creeks_135_47'), number('region.make_creeks_numbers', 'make_creeks_135_51'), number('region.make_creeks_numbers', 'make_creeks_135_55'), number('region.make_creeks_numbers', 'make_creeks_135_60'), number('region.make_creeks_numbers', 'make_creeks_135_65')]) + dk.int("j", number('region.make_creeks_numbers', 'make_creeks_135_85')) - number('region.make_creeks_numbers', 'make_creeks_135_91'))
        ux, uy = math.cos(th), math.sin(th); px_, py_ = -uy, ux
        off = FT((number('region.make_creeks_numbers', 'make_creeks_137_18') + number('region.make_creeks_numbers', 'make_creeks_137_24') * dk.int("off", number('region.make_creeks_numbers', 'make_creeks_137_44'))) if k == number('region.make_creeks_numbers', 'make_creeks_137_57') else (number('region.make_creeks_numbers', 'make_creeks_137_65') + number('region.make_creeks_numbers', 'make_creeks_137_72') * dk.int("off", number('region.make_creeks_numbers', 'make_creeks_137_92')))) * (number('region.make_creeks_numbers', 'make_creeks_137_101') if dk.chance("side", number('region.make_creeks_numbers', 'make_creeks_137_124')) else -number('region.make_creeks_numbers', 'make_creeks_137_134'))
        P = (ax_ + px_ * off, ay_ + py_ * off)
        pts = []
        for sgn in (-number('region.make_creeks_numbers', 'make_creeks_140_21'), number('region.make_creeks_numbers', 'make_creeks_140_24')):
            x, y = P; lat = number('region.make_creeks_numbers', 'make_creeks_141_28'); seq = []
            for i in range(number('region.make_creeks_numbers', 'make_creeks_142_27')):
                lat = max(-FT(number('region.make_creeks_numbers', 'make_creeks_143_30')), min(FT(number('region.make_creeks_numbers', 'make_creeks_143_43')), lat + FT(dk.int(f"m{sgn}{i}", number('region.make_creeks_numbers', 'make_creeks_143_79')) - number('region.make_creeks_numbers', 'make_creeks_143_86'))))
                x += sgn * ux * FT(number('region.make_creeks_numbers', 'make_creeks_144_35')); y += sgn * uy * FT(number('region.make_creeks_numbers', 'make_creeks_144_60'))
                seq.append((int(x + px_ * lat), int(y + py_ * lat)))
                if not (-FT(number('region.make_creeks_numbers', 'make_creeks_146_28')) <= x <= top + FT(number('region.make_creeks_numbers', 'make_creeks_146_50')) and -FT(number('region.make_creeks_numbers', 'make_creeks_146_63')) <= y <= top + FT(number('region.make_creeks_numbers', 'make_creeks_146_85'))): break
            pts = (seq[::-number('region.make_creeks_numbers', 'make_creeks_147_26')] + [(int(P[number('region.make_creeks_numbers', 'make_creeks_147_39')]), int(P[number('region.make_creeks_numbers', 'make_creeks_147_50')]))]) if sgn < number('region.make_creeks_numbers', 'make_creeks_147_66') else pts + seq
        w = FT(dk.int("w", CREEK_W[number('region.make_creeks_numbers', 'make_creeks_148_35')] - CREEK_W[number('region.make_creeks_numbers', 'make_creeks_148_48')] + number('region.make_creeks_numbers', 'make_creeks_148_53')) + CREEK_W[number('region.make_creeks_numbers', 'make_creeks_148_66')]); buf = FT(CREEK_BUF[number('region.make_creeks_numbers', 'make_creeks_148_90')] + number('region.make_creeks_numbers', 'make_creeks_148_95') * dk.int("b", (CREEK_BUF[number('region.make_creeks_numbers', 'make_creeks_148_123')] - CREEK_BUF[number('region.make_creeks_numbers', 'make_creeks_148_138')]) // number('region.make_creeks_numbers', 'make_creeks_148_145') + number('region.make_creeks_numbers', 'make_creeks_148_150')))
        bridges = []
        for r in roads:
            i = number('region.make_creeks_numbers', 'make_creeks_151_16') if r["axis"] == "h" else number('region.make_creeks_numbers', 'make_creeks_151_43')
            for a, b in zip(pts, pts[number('region.make_creeks_numbers', 'make_creeks_152_37'):]):
                if a[i] != b[i] and min(a[i], b[i]) <= r["c"] <= max(a[i], b[i]):
                    t = (r["c"] - a[i]) / (b[i] - a[i]); o = int(a[number('region.make_creeks_numbers', 'make_creeks_154_67') - i] + t * (b[number('region.make_creeks_numbers', 'make_creeks_154_83') - i] - a[number('region.make_creeks_numbers', 'make_creeks_154_94') - i]))
                    if r["lo"] <= o <= r["hi"]: bridges.append((r["id"], (o, r["c"]) if i == number('region.make_creeks_numbers', 'make_creeks_155_93') else (r["c"], o)))
        out.append({"id": f"creek{k}", "name": ["Rock Creek", "Mud Creek", "Otter Creek"][k % number('region.make_creeks_numbers', 'make_creeks_156_94')], "pts": pts, "chan_w": w, "buffer": buf,
                    "w": w + number('region.make_creeks_numbers', 'make_creeks_157_29') * buf, "bridges": bridges})
    return out

# ---------- roads ----------
def make_roads(seed, force_town):
    d = rng(seed, "roads"); n = N_MI
    pct = PAVED_PCT[number('region.make_roads_numbers', 'make_roads_163_20')] + d.int("pct", PAVED_PCT[number('region.make_roads_numbers', 'make_roads_163_48')] - PAVED_PCT[number('region.make_roads_numbers', 'make_roads_163_63')] + number('region.make_roads_numbers', 'make_roads_163_68'))
    lines = [(ax, i) for ax in "hv" for i in range(n + number('region.make_roads_numbers', 'make_roads_164_55'))]
    n_paved = max(number('region.make_roads_numbers', 'make_roads_165_18'), round(pct * len(lines) / number('region.make_roads_numbers', 'make_roads_165_46')))
    # lettered county routes first: one E-W and one N-S interior line, so they cross
    ew = number('region.make_roads_numbers', 'make_roads_167_9') + d.int("ew", n - number('region.make_roads_numbers', 'make_roads_167_29')); ns = number('region.make_roads_numbers', 'make_roads_167_38') + d.int("ns", n - number('region.make_roads_numbers', 'make_roads_167_58'))
    paved = [("h", ew), ("v", ns)][:n_paved]
    gravel_ns = ["Key Ave", "Lake Ave", "Mallard Ave", "Nature Ave", "Oak Ave"]
    roads = []
    for ax, i in lines:
        t = "paved" if (ax, i) in paved else "gravel"
        if t == "paved": name = f"{'C' if ax == 'h' else 'K'}{number('region.make_roads_numbers', 'make_roads_173_62') + number('region.make_roads_numbers', 'make_roads_173_67') * d.int(ax + 'num', number('region.make_roads_numbers', 'make_roads_173_89'))}"   # lettered county route
        else: name = f"{number('region.make_roads_numbers', 'make_roads_174_24') + number('region.make_roads_numbers', 'make_roads_174_30') * i}th St" if ax == "h" else gravel_ns[i]
        sw = SURF[t][number('region.make_roads_numbers', 'make_roads_175_21')] + d.int(f"surf{ax}{i}", SURF[t][number('region.make_roads_numbers', 'make_roads_175_56')] - SURF[t][number('region.make_roads_numbers', 'make_roads_175_69')] + number('region.make_roads_numbers', 'make_roads_175_74'))
        roads.append({"id": f"{'EW' if ax == 'h' else 'NS'}{i}", "name": name, "axis": ax, "c": i * MILE, "lo": number('region.make_roads_numbers', 'make_roads_176_112'),
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

def town_band(pop): return number('region.invariants_numbers', 'town_band_194_27') if pop < BANDS[number('region.invariants_numbers', 'town_band_194_44')] else number('region.invariants_numbers', 'town_band_194_52') if pop < BANDS[number('region.invariants_numbers', 'town_band_194_69')] else number('region.invariants_numbers', 'town_band_194_77')

YEARS = DEFAULTS['region.years']
BASE_YEAR = DEFAULTS['region.base_year']
FADE_1985 = DEFAULTS['region.fade_1985']
FADING_TYPES = DEFAULTS['region.fading_types']
BRANCH_PCT = DEFAULTS['region.branch_pct']

def region_year(seed): return rng(seed, "year").choice("year", YEARS) if option("generation.parity") else option("generation.snapshot_year")
def fade_factor(year): return number('region.invariants_numbers', 'fade_factor_203_30') + (FADE_1985 - number('region.invariants_numbers', 'fade_factor_203_47')) * (BASE_YEAR - year) / (BASE_YEAR - number('region.invariants_numbers', 'fade_factor_203_86'))    # linear; <1 after 1998
def vacant_pct(year): return VACANT_PCT * (number('region.invariants_numbers', 'vacant_pct_204_43') - fade_factor(year))                              # opposite direction
def branch_pct(year): return BRANCH_PCT[number('region.invariants_numbers', 'branch_pct_205_40')] + (BRANCH_PCT[number('region.invariants_numbers', 'branch_pct_205_57')] - BRANCH_PCT[number('region.invariants_numbers', 'branch_pct_205_73')]) * (year - number('region.invariants_numbers', 'branch_pct_205_87')) / number('region.invariants_numbers', 'branch_pct_205_95')
def biz_rates(band, year=BASE_YEAR):
    f = fade_factor(year)
    return {t: min(number('region.invariants_numbers', 'biz_rates_208_19'), p[band] * f) if t in FADING_TYPES else p[band] for t, p in BIZ_P.items()}

def choose_businesses(seed, band, year=BASE_YEAR):
    """Deterministic per seed: each type drawn independently with the band's chance for the world's snapshot year."""
    R = biz_rates(band, year)
    d = rng(seed, "biz"); out = [t for t in BIZ_P if d.chance(t, R[t])]
    if "church" in out and d.chance("church2", CHURCH2_P[band]): out.append("church")
    if "grocery" in out and band == number('region.choose_businesses_numbers', 'choose_businesses_215_36') and d.chance("hwy", number('region.choose_businesses_numbers', 'choose_businesses_215_58')): out[out.index("grocery")] = "grocery (highway)"
    return out

def _wr(F, x0, u, w, h):   # face-local (x0 world x, u = depth from the street edge) -> world rect
    return (x0, F["front_y"] + u, w, h) if F["fa"] == "S" else (x0, F["front_y"] - u - h, w, h)

def _main_biz(typ, x0, F, n, seed, vacant_of=None):
    D = F["d"]; dd = rng(seed, F["run"], "biz", n)
    curb = _wr(F, x0, -FT(number('region._main_biz_numbers', '_main_biz_223_26')), number('region._main_biz_numbers', '_main_biz_223_31'), FT(number('region._main_biz_numbers', '_main_biz_223_37')))
    def rec(lw, bldg, parking, extras=None, site="main street"):
        c = _wr(F, x0, -FT(number('region._main_biz_numbers', '_main_biz_225_27')), lw, FT(number('region._main_biz_numbers', '_main_biz_225_39')))
        return {"id": f"{F['run']}:biz{n}", "type": typ if not vacant_of else f"vacant (former {vacant_of})", "site": site,
                "lot": _wr(F, x0, number('region._main_biz_numbers', '_main_biz_227_34'), lw, D), "bldg": bldg, "parking": parking, "curb": c, "canopy": None, "pumps": [],
                "front_y": F["front_y"], "facing": F["fa"], "storefront": typ in STOREFRONT or bool(vacant_of), "vacant": bool(vacant_of)}
    if typ == "gas station":
        lw = FT(dd.choice("gw", [number('region._main_biz_numbers', '_main_biz_230_33'), number('region._main_biz_numbers', '_main_biz_230_38'), number('region._main_biz_numbers', '_main_biz_230_43')])); bu = D - FT(number('region._main_biz_numbers', '_main_biz_230_63')) - FT(number('region._main_biz_numbers', '_main_biz_230_72'))
        r = rec(lw, _wr(F, x0 + lw - FT(number('region._main_biz_numbers', '_main_biz_231_40')), bu, FT(number('region._main_biz_numbers', '_main_biz_231_52')), FT(number('region._main_biz_numbers', '_main_biz_231_60'))), [_wr(F, x0, number('region._main_biz_numbers', '_main_biz_231_78'), lw, bu)], site="blacktop corner")
        np_ = number('region._main_biz_numbers', '_main_biz_232_14') + dd.int("np", number('region._main_biz_numbers', '_main_biz_232_31')); r["canopy"] = _wr(F, x0 + FT(number('region._main_biz_numbers', '_main_biz_232_64')), FT(number('region._main_biz_numbers', '_main_biz_232_72')), FT(number('region._main_biz_numbers', '_main_biz_232_80')) * np_ + FT(number('region._main_biz_numbers', '_main_biz_232_95')), FT(number('region._main_biz_numbers', '_main_biz_232_103')))
        r["pumps"] = [_wr(F, x0 + FT(number('region._main_biz_numbers', '_main_biz_233_37')) + k * FT(number('region._main_biz_numbers', '_main_biz_233_50')), FT(number('region._main_biz_numbers', '_main_biz_233_58')), FT(number('region._main_biz_numbers', '_main_biz_233_66')), FT(number('region._main_biz_numbers', '_main_biz_233_73'))) for k in range(np_)]
        r["spaces"] = number('region._main_biz_numbers', '_main_biz_234_22') + dd.int("sp", number('region._main_biz_numbers', '_main_biz_234_39')); r["curb"] = None; return r
    if typ == "bank":
        lw = FT(number('region._main_biz_numbers', '_main_biz_236_16')); bw = FT(dd.choice("bw", [number('region._main_biz_numbers', '_main_biz_236_46'), number('region._main_biz_numbers', '_main_biz_236_50'), number('region._main_biz_numbers', '_main_biz_236_54')])); bd = FT(dd.choice("bd", [number('region._main_biz_numbers', '_main_biz_236_86'), number('region._main_biz_numbers', '_main_biz_236_90'), number('region._main_biz_numbers', '_main_biz_236_94')]))
        return rec(lw, _wr(F, x0, number('region._main_biz_numbers', '_main_biz_237_34'), bw, bd), [_wr(F, x0, bd, lw, D - bd)] + ([_wr(F, x0 + bw, number('region._main_biz_numbers', '_main_biz_237_95'), lw - bw, bd)] if bw < lw else []), site="main street corner")
    if typ in ("post office", "city hall / fire"):
        lw = FT(number('region._main_biz_numbers', '_main_biz_239_16') if typ == "post office" else number('region._main_biz_numbers', '_main_biz_239_48')); bw, bd = (FT(number('region._main_biz_numbers', '_main_biz_239_66')), FT(number('region._main_biz_numbers', '_main_biz_239_74'))) if typ == "post office" else (FT(number('region._main_biz_numbers', '_main_biz_239_112')), FT(number('region._main_biz_numbers', '_main_biz_239_120')))
        return rec(lw, _wr(F, x0 + (lw - bw) // number('region._main_biz_numbers', '_main_biz_240_48'), FT(number('region._main_biz_numbers', '_main_biz_240_54')), bw, bd), [_wr(F, x0, number('region._main_biz_numbers', '_main_biz_240_80'), lw, FT(number('region._main_biz_numbers', '_main_biz_240_90'))), _wr(F, x0, FT(number('region._main_biz_numbers', '_main_biz_240_110')) + bd, lw, D - FT(number('region._main_biz_numbers', '_main_biz_240_131')) - bd)],
                   site="main street" if F["main"] else "one block off main")
    bd = min(FT(dd.choice("sd", [number('region._main_biz_numbers', '_main_biz_242_33'), number('region._main_biz_numbers', '_main_biz_242_37'), number('region._main_biz_numbers', '_main_biz_242_41'), number('region._main_biz_numbers', '_main_biz_242_46'), number('region._main_biz_numbers', '_main_biz_242_51')])), D - FT(number('region._main_biz_numbers', '_main_biz_242_66')))                 # attached storefront, zero side setbacks
    return rec(FT(number('region._main_biz_numbers', '_main_biz_243_18')), _wr(F, x0, number('region._main_biz_numbers', '_main_biz_243_34'), FT(number('region._main_biz_numbers', '_main_biz_243_40')), bd), [_wr(F, x0, bd, FT(number('region._main_biz_numbers', '_main_biz_243_69')), D - bd)])    # curb out front, alley yard behind

def _church(x0, w, F, n, cnt):
    D = F["d"]; bx = x0 + (w - FT(number('region.invariants_numbers', '_church_246_34'))) // number('region.invariants_numbers', '_church_246_42')
    return {"id": f"{F['run']}:church{n}", "type": "church", "site": f"residential corner ({cnt} lots)", "lot": _wr(F, x0, number('region.invariants_numbers', '_church_247_123'), w, D),
            "bldg": _wr(F, bx, FT(number('region.invariants_numbers', '_church_248_34')), FT(number('region.invariants_numbers', '_church_248_42')), FT(number('region.invariants_numbers', '_church_248_50'))), "parsonage": _wr(F, x0 + FT(number('region.invariants_numbers', '_church_248_84')), FT(number('region.invariants_numbers', '_church_248_91')), FT(number('region.invariants_numbers', '_church_248_100')), min(FT(number('region.invariants_numbers', '_church_248_112')), D - FT(number('region.invariants_numbers', '_church_248_124')))),
            "parking": [_wr(F, x0, number('region.invariants_numbers', '_church_249_35'), (w - FT(number('region.invariants_numbers', '_church_249_46'))) // number('region.invariants_numbers', '_church_249_54'), FT(number('region.invariants_numbers', '_church_249_60')))], "curb": None, "canopy": None, "pumps": [],
            "front_y": F["front_y"], "facing": F["fa"], "storefront": False, "vacant": False}

# ---------- town ----------
def make_town(seed, roads, rail, force_town, force_pop=None, creeks=()):
    """Rail town where the rail passes the blacktop crossing (grid on the far side of the road, rows along the rail),
    else maybe a no-rail town at the blacktop x blacktop crossing."""
    if not option("stages.towns"): return None
    d = rng(seed, "town")
    ew = next((r for r in roads if r["type"] == "paved" and r["axis"] == "h"), None)
    ns = next((r for r in roads if r["type"] == "paved" and r["axis"] == "v"), None)
    rail_town = bool(rail) and (d.chance("railtown", RAILTOWN_PCT) or bool(force_town))
    if not rail_town and not (ew and ns and (d.chance("town", NORAIL_TOWN_PCT) or force_town)): return None
    cx = FT(d.choice("cx", list(BLOCK_CX))); sw = FT(d.choice("sw", list(STREET_W)))
    al = FT(d.choice("al", list(ALLEY_W))); ld = FT(d.choice("ld", list(LOT_D)))
    cy = number('region.make_town_numbers', 'make_town_263_9') * ld + al + sw
    size = d.weighted("size", TOWN_SIZE)
    if rail_town:
        Xc, Yc = rail["site"]; cols, rows = d.choice("grid", GRIDS[size][number('region.make_town_numbers', 'make_town_266_73')]); sd = rail["side"]
        xs = sorted(Xc - sd * k * cx for k in range(cols + number('region.make_town_numbers', 'make_town_267_59')))
    else:
        Xc, Yc = ns["c"], ew["c"]; cols, rows = d.choice("grid", GRIDS[size][number('region.make_town_numbers', 'make_town_269_77')]); a = cols // number('region.make_town_numbers', 'make_town_269_94')
        xs = [Xc + k * cx for k in range(-a, cols - a + number('region.make_town_numbers', 'make_town_270_56'))]
    b = rows // number('region.make_town_numbers', 'make_town_271_16')
    ys = [Yc + k * cy for k in range(-b, rows - b + number('region.make_town_numbers', 'make_town_272_52'))]
    names_h = ["Williams St", "Durbend St", "Doring St", "Elm St", "Maple St"]
    streets = []
    for k, x in enumerate(xs):
        if x != Xc: streets.append({"id": f"T-A{k}", "name": f"{k + number('region.make_town_numbers', 'make_town_276_68')}{'st' if k == number('region.make_town_numbers', 'make_town_276_84') else 'nd' if k == number('region.make_town_numbers', 'make_town_276_104') else 'rd' if k == number('region.make_town_numbers', 'make_town_276_124') else 'th'} Ave",
                                     "axis": "v", "type": "paved", "c": x, "lo": ys[number('region.make_town_numbers', 'make_town_277_84')], "hi": ys[-number('region.make_town_numbers', 'make_town_277_98')], "w": sw, "surface_ft": number('region.make_town_numbers', 'make_town_277_125')})
    for k, y in enumerate(ys):
        if y != Yc: streets.append({"id": f"T-S{k}", "name": names_h[k % number('region.make_town_numbers', 'make_town_279_73')], "axis": "h", "type": "paved", "c": y,
                                     "lo": xs[number('region.make_town_numbers', 'make_town_280_46')], "hi": xs[-number('region.make_town_numbers', 'make_town_280_60')], "w": sw, "surface_ft": number('region.make_town_numbers', 'make_town_280_87')})
    hw = lambda v, c: ROW // number('region.make_town_numbers', 'make_town_281_29') if v == c else sw // number('region.make_town_numbers', 'make_town_281_52')
    sb = {k: FT(v[number('region.make_town_numbers', 'make_town_282_18')] + d.int("sb" + k, v[number('region.make_town_numbers', 'make_town_282_41')] - v[number('region.make_town_numbers', 'make_town_282_48')] + number('region.make_town_numbers', 'make_town_282_53'))) for k, v in TOWN_SB.items()}
    blocks, alleys, lots, faces = [], [], [], []
    for i in range(cols):
        for j in range(rows):
            outer = i in (number('region.make_town_numbers', 'make_town_286_26'), cols - number('region.make_town_numbers', 'make_town_286_36')) or j in (number('region.make_town_numbers', 'make_town_286_48'), rows - number('region.make_town_numbers', 'make_town_286_58'))
            on_main = Yc in (ys[j], ys[j + number('region.make_town_numbers', 'make_town_287_43')]) and min(abs(xs[i] - Xc), abs(xs[i + number('region.make_town_numbers', 'make_town_287_83')] - Xc)) < number('region.make_town_numbers', 'make_town_287_95') * cx
            if size != "tiny" and outer and not on_main and rng(seed, "ragged", i, j).chance("skip", RAGGED_PCT): continue
            bx0, bx1 = xs[i] + hw(xs[i], Xc), xs[i + number('region.make_town_numbers', 'make_town_289_53')] - hw(xs[i + number('region.make_town_numbers', 'make_town_289_68')], Xc)
            by0, by1 = ys[j] + hw(ys[j], Yc), ys[j + number('region.make_town_numbers', 'make_town_290_53')] - hw(ys[j + number('region.make_town_numbers', 'make_town_290_68')], Yc)
            if rail_hits(list(creeks), (bx0, by0, bx1 - bx0, by1 - by0)): continue        # town stops at the floodplain
            # ribbon growth: blocks on the main paved roads always grow; others thin out with distance from both roads
            ra = min(abs(xs[i] - Xc), abs(xs[i + number('region.make_town_numbers', 'make_town_293_49')] - Xc)) / cx; rb = min(abs(ys[j] - Yc), abs(ys[j + number('region.make_town_numbers', 'make_town_293_102')] - Yc)) / cy
            if min(ra, rb) >= number('region.make_town_numbers', 'make_town_294_30') and not rng(seed, "ribbon", i, j).chance("keep", max(number('region.make_town_numbers', 'make_town_294_85'), number('region.make_town_numbers', 'make_town_294_89') - RIBBON_DECAY * number('region.make_town_numbers', 'make_town_294_110') / max(cols, rows, number('region.make_town_numbers', 'make_town_294_130')) * (ra + rb - number('region.make_town_numbers', 'make_town_294_146')))): continue
            bld = (by1 - by0 - al) // number('region.make_town_numbers', 'make_town_295_38')
            blocks.append({"id": f"b{i}{j}", "ij": (i, j), "rect": (bx0, by0, bx1 - bx0, by1 - by0), "lot_d": bld})
            alleys.append((bx0, by0 + bld, bx1 - bx0, al))
            for half, y0, fa in (("S", by0, "S"), ("N", by1 - bld, "N")):
                front = ys[j] if half == "S" else ys[j + number('region.make_town_numbers', 'make_town_299_57')]
                edge = (half == "S" and j == number('region.make_town_numbers', 'make_town_300_45')) or (half == "N" and j == rows - number('region.make_town_numbers', 'make_town_300_80'))
                if edge and front != Yc and rng(seed, "halfblock", i, j).chance("drop", EDGE_HALF_PCT): continue   # uneven edge depth
                main = front == Yc and Xc in (xs[i], xs[i + number('region.make_town_numbers', 'make_town_302_60')])
                faces.append({"run": f"town:b{i}{j}:{half}", "y0": y0, "fa": fa, "bx0": bx0, "bx1": bx1, "d": bld,
                              "front_y": by0 if half == "S" else by1, "main": front == Yc,
                              "dist": min(abs(xs[i] - Xc), abs(xs[i + number('region.make_town_numbers', 'make_town_305_70')] - Xc)), "from_lo": xs[i] == Xc or (xs[i + number('region.make_town_numbers', 'make_town_305_115')] != Xc and xs[i] < Xc)})
    kept = {b["ij"] for b in blocks}; streets = []; ds = rng(seed, "streets")
    def segs(cells):
        cells = sorted(cells); out = []
        for a, b in cells:
            if out and out[-number('region.make_town_numbers', 'make_town_310_28')][number('region.make_town_numbers', 'make_town_310_31')] >= a: out[-number('region.make_town_numbers', 'make_town_310_45')][number('region.make_town_numbers', 'make_town_310_48')] = max(out[-number('region.make_town_numbers', 'make_town_310_62')][number('region.make_town_numbers', 'make_town_310_65')], b)
            else: out.append([a, b])
        return out
    for k, x in enumerate(xs):
        if x == Xc: continue
        for n_, (lo, hi) in enumerate(segs([(ys[j], ys[j + number('region.make_town_numbers', 'make_town_315_59')]) for (i, j) in kept if i in (k - number('region.make_town_numbers', 'make_town_315_95'), k)])):
            if ds.chance(f"v{k}{n_}", FRINGE_PCT) and hi + cy <= ys[-number('region.make_town_numbers', 'make_town_316_69')] + cy: hi += cy // number('region.make_town_numbers', 'make_town_316_90')       # dead-end fringe stub
            streets.append({"id": f"T-A{k}-{n_}", "name": f"{k + number('region.make_town_numbers', 'make_town_317_65')}{'st' if k == number('region.make_town_numbers', 'make_town_317_81') else 'nd' if k == number('region.make_town_numbers', 'make_town_317_101') else 'rd' if k == number('region.make_town_numbers', 'make_town_317_121') else 'th'} Ave",
                            "axis": "v", "type": "paved", "c": x, "lo": lo, "hi": hi, "w": sw, "surface_ft": number('region.make_town_numbers', 'make_town_318_109')})
    for k, y in enumerate(ys):
        if y == Yc: continue
        for n_, (lo, hi) in enumerate(segs([(xs[i], xs[i + number('region.make_town_numbers', 'make_town_321_59')]) for (i, j) in kept if j in (k - number('region.make_town_numbers', 'make_town_321_95'), k)])):
            if ds.chance(f"h{k}{n_}", FRINGE_PCT): lo -= cx // number('region.make_town_numbers', 'make_town_322_63')
            streets.append({"id": f"T-S{k}-{n_}", "name": names_h[k % number('region.make_town_numbers', 'make_town_323_70')], "axis": "h", "type": "paved", "c": y,
                            "lo": lo, "hi": hi, "w": sw, "surface_ft": number('region.make_town_numbers', 'make_town_324_71')})
    def face_lots(F, bx0, bx1):
        run, y0, fa, bld = F["run"], F["y0"], F["fa"], F["d"]; out = []
        nb = (bx1 - bx0) // BASE_LOT; rest = (bx1 - bx0) - nb * BASE_LOT
        if nb == number('region.make_town_numbers', 'make_town_328_17'): return out
        dr = rng(seed, run)
        tr = set()
        if dr.chance("trailers", TRAILER_RUN_PCT):        # seeded trailer cluster on adjacent single lots
            n_tr = TRAILER_RUN[number('region.make_town_numbers', 'make_town_332_31')] + dr.int("ntr", TRAILER_RUN[number('region.make_town_numbers', 'make_town_332_62')] - TRAILER_RUN[number('region.make_town_numbers', 'make_town_332_79')] + number('region.make_town_numbers', 'make_town_332_84'))
            st = dr.int("st", max(number('region.make_town_numbers', 'make_town_333_34'), nb - n_tr + number('region.make_town_numbers', 'make_town_333_49'))); tr = set(range(st, min(nb, st + n_tr)))
        takes, used = [], number('region.make_town_numbers', 'make_town_334_26')
        while used < nb:
            k = number('region.make_town_numbers', 'make_town_336_16') if used in tr else min(rng(seed, run, len(takes)).weighted("take", BASE_TAKE), nb - used)
            if k > number('region.make_town_numbers', 'make_town_337_19') and any(u in tr for u in range(used, used + k)): k = min(u for u in tr if u >= used) - used
            if k == number('region.make_town_numbers', 'make_town_338_20') and used not in tr:
                if takes and (used - number('region.make_town_numbers', 'make_town_339_37')) not in tr: takes[-number('region.make_town_numbers', 'make_town_339_58')] += number('region.make_town_numbers', 'make_town_339_64')
                elif used + number('region.make_town_numbers', 'make_town_340_28') < nb and (used + number('region.make_town_numbers', 'make_town_340_47')) not in tr: k = number('region.make_town_numbers', 'make_town_340_65'); takes.append(number('region.make_town_numbers', 'make_town_340_81'))
                else: takes.append(number('region.make_town_numbers', 'make_town_341_35')); tr.add(used)
            else: takes.append(k)
            used += k
        x = bx0; used = number('region.make_town_numbers', 'make_town_344_24')
        for n, k in enumerate(takes):
            w = k * BASE_LOT + (rest if used + k == nb else number('region.make_town_numbers', 'make_town_346_60'))
            px = x if fa == "S" else bx1 - (x - bx0) - w
            out.append({"id": f"{run}:{n}", "w": w, "d": bld, "setting": "town", "kind": "town",
                        "place": {"x0": px, "y0": y0, "facing": fa}, "along": px, "base_lots": k,
                        "house_type": "trailer" if (k == number('region.make_town_numbers', 'make_town_350_57') and used in tr) else "stick", "use": "house"})
            x += w; used += k
        return out
    houses = sum(len(face_lots(F, F["bx0"], F["bx1"])) for F in faces)          # pass 1: houses before businesses
    pop = round(houses * PEOPLE_PER_HOUSE); band = town_band(force_pop if force_pop else pop)
    year = region_year(seed); prog = choose_businesses(seed, band, year); db = rng(seed, "bizplace")
    mains = sorted((F for F in faces if F["main"]), key=lambda F: (F["dist"], F["run"]))[:MAIN_FACES]
    off = sorted((F for F in faces if not F["main"] and abs((F["front_y"]) - Yc) < number('region.make_town_numbers', 'make_town_357_83') * cy), key=lambda F: (F["dist"], F["run"]))
    for F in faces: F["lo"], F["hi"] = F["bx0"], F["bx1"]
    def take(F, w, far=False):
        if F["hi"] - F["lo"] < w: return None
        lo_end = F["from_lo"] != far
        if lo_end: x0 = F["lo"]; F["lo"] += w
        else: x0 = F["hi"] - w; F["hi"] -= w
        return x0
    commercial, unplaced, n = [], [], number('region.make_town_numbers', 'make_town_365_38')
    def put(typ, faces_, w, **k):
        nonlocal n
        for F in faces_:
            x0 = take(F, w)
            if x0 is not None: commercial.append(_main_biz(typ, x0, F, n, seed, **k)); n += number('region.make_town_numbers', 'make_town_370_92'); return True
        unplaced.append(typ); return False
    if "gas station" in prog: put("gas station", mains[:number('region.make_town_numbers', 'make_town_372_56')], FT(number('region.make_town_numbers', 'make_town_372_63')))
    if "bank" in prog and put("bank", mains[number('region.make_town_numbers', 'make_town_373_44'):number('region.make_town_numbers', 'make_town_373_46')] + mains, FT(number('region.make_town_numbers', 'make_town_373_61'))):
        bk = commercial[-number('region.make_town_numbers', 'make_town_374_25')]; bk["branch"] = db.chance("branch", branch_pct(year))
        if bk["branch"]: bk["type"] = "bank (branch)"
    shops = [t for t in prog if t in STOREFRONT]
    nvac = sum(db.chance(f"vac{i}", vacant_pct(year)) for i in range(len(shops) + number('region.make_town_numbers', 'make_town_377_82'))) if shops or band > number('region.make_town_numbers', 'make_town_377_105') else number('region.make_town_numbers', 'make_town_377_112')
    for t in shops: put(t, mains[number('region.make_town_numbers', 'make_town_378_33'):] + mains[:number('region.make_town_numbers', 'make_town_378_46')], FT(number('region.make_town_numbers', 'make_town_378_53')))
    for i in range(nvac): put("vacant", mains[number('region.make_town_numbers', 'make_town_379_46'):] + mains[:number('region.make_town_numbers', 'make_town_379_59')], FT(number('region.make_town_numbers', 'make_town_379_66')), vacant_of=db.choice(f"vf{i}", FADING))
    for t in ("post office", "city hall / fire"):
        if t in prog: put(t, mains + off, FT(number('region.make_town_numbers', 'make_town_381_45') if t == "post office" else number('region.make_town_numbers', 'make_town_381_75')))
    res = [F for F in faces if not F["main"]]
    for c in range(prog.count("church")):
        cnt = number('region.make_town_numbers', 'make_town_384_14') + db.int(f"cl{c}", number('region.make_town_numbers', 'make_town_384_35'))
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
    box = (min(r[number('region.make_town_numbers', 'make_town_401_17')] for r in bx) - sw, min(r[number('region.make_town_numbers', 'make_town_401_45')] for r in bx) - sw, number('region.make_town_numbers', 'make_town_401_67'), number('region.make_town_numbers', 'make_town_401_70'))
    box = (box[number('region.make_town_numbers', 'make_town_402_15')], box[number('region.make_town_numbers', 'make_town_402_23')], max(r[number('region.make_town_numbers', 'make_town_402_33')] + r[number('region.make_town_numbers', 'make_town_402_40')] for r in bx) + sw - box[number('region.make_town_numbers', 'make_town_402_67')], max(r[number('region.make_town_numbers', 'make_town_402_77')] + r[number('region.make_town_numbers', 'make_town_402_84')] for r in bx) + sw - box[number('region.make_town_numbers', 'make_town_402_111')])
    return {"Xc": Xc, "Yc": Yc, "cols": cols, "rows": rows, "cx": cx, "cy": cy, "street_w": sw, "alley_w": al,
            "setbacks": sb, "blocks": blocks, "alleys": alleys, "streets": streets, "lots": lots, "box": box, "rail_town": rail_town, "commercial": commercial,
            "year": year, "size": size, "pop_est": pop, "houses_est": houses, "band": band, "program": prog, "edge_want": edge_want, "unplaced": unplaced}

def place_rail_features(seed, town, rail, occupied, bar=None):
    """Rail towns only: depot between road and track, elevator 3-6 ac across the track with a siding beside it."""
    d = rng(seed, "rail"); sd, rx, Yc = rail["side"], rail["x"], town["Yc"]
    depot = (int(rx - sd * (RAIL_ROW // number('region.place_rail_features_numbers', 'place_rail_features_410_40') - FT(number('region.place_rail_features_numbers', 'place_rail_features_410_47'))) - FT(number('region.place_rail_features_numbers', 'place_rail_features_410_57'))), Yc + ROW // number('region.place_rail_features_numbers', 'place_rail_features_410_75') + FT(number('region.place_rail_features_numbers', 'place_rail_features_410_82')), FT(number('region.place_rail_features_numbers', 'place_rail_features_410_91')), FT(number('region.place_rail_features_numbers', 'place_rail_features_410_99')))
    for attempt in range(option("generation.max_feature_attempts")):
        w, h = FT(d.choice("ew", list(range(number('region.place_rail_features_numbers', 'place_rail_features_412_44'), number('region.place_rail_features_numbers', 'place_rail_features_412_49'), number('region.place_rail_features_numbers', 'place_rail_features_412_54'))))), FT(d.choice("eh", list(range(number('region.place_rail_features_numbers', 'place_rail_features_412_91'), number('region.place_rail_features_numbers', 'place_rail_features_412_96'), number('region.place_rail_features_numbers', 'place_rail_features_412_101')))))
        if ELEV_AC[number('region.place_rail_features_numbers', 'place_rail_features_413_19')] <= acres(w, h) <= ELEV_AC[number('region.place_rail_features_numbers', 'place_rail_features_413_48')]: break
        d = Draw(H(d.seed, "again"))
    else: return None, depot, None
    ex = rx + sd * (RAIL_ROW // number('region.place_rail_features_numbers', 'place_rail_features_415_32') + FT(number('region.place_rail_features_numbers', 'place_rail_features_415_39'))); ex = ex if sd > number('region.place_rail_features_numbers', 'place_rail_features_415_61') else ex - w
    rect = None
    for y0 in (Yc - ROW // number('region.place_rail_features_numbers', 'place_rail_features_417_27') - FT(number('region.place_rail_features_numbers', 'place_rail_features_417_34')) - h, Yc + ROW // number('region.place_rail_features_numbers', 'place_rail_features_417_55') + FT(number('region.place_rail_features_numbers', 'place_rail_features_417_62')), Yc - ROW // number('region.place_rail_features_numbers', 'place_rail_features_417_79') - FT(number('region.place_rail_features_numbers', 'place_rail_features_417_86')) - h):
        r = (ex, y0, w, h)
        if not rail_hits(bar or rail, r) and not any(overlaps(r, o) for o in occupied): rect = r; break
    if rect is None: return None, depot, None
    sx = rx + sd * (RAIL_ROW // number('region.place_rail_features_numbers', 'place_rail_features_421_32') + FT(number('region.place_rail_features_numbers', 'place_rail_features_421_39')))
    siding = [(sx, rect[number('region.place_rail_features_numbers', 'place_rail_features_422_24')] - FT(number('region.place_rail_features_numbers', 'place_rail_features_422_32'))), (sx, rect[number('region.place_rail_features_numbers', 'place_rail_features_422_49')] + h + FT(number('region.place_rail_features_numbers', 'place_rail_features_422_61')))]
    return {"kind": "elevator", "name": "Co-op elevator", "rect": rect, "acres": acres(w, h)}, depot, siding


def _edge_slot(town, ew, lw, ld, rail, occupied, order=(-number('region.invariants_numbers', '_edge_slot_426_57'), number('region.invariants_numbers', '_edge_slot_426_60'))):
    b = town["box"]; top = N_MI * MILE
    for step in range(number('region.invariants_numbers', '_edge_slot_428_22')):
        for sx in order if order != (number('region.invariants_numbers', '_edge_slot_429_37'), number('region.invariants_numbers', '_edge_slot_429_40')) else (number('region.invariants_numbers', '_edge_slot_429_49'), -number('region.invariants_numbers', '_edge_slot_429_53')):
            for sy in (number('region.invariants_numbers', '_edge_slot_430_23'), -number('region.invariants_numbers', '_edge_slot_430_27')):
                gap = FT(number('region.invariants_numbers', '_edge_slot_431_25')) + step * FT(number('region.invariants_numbers', '_edge_slot_431_41'))
                x0 = b[number('region.invariants_numbers', '_edge_slot_432_23')] - gap - lw if sx < number('region.invariants_numbers', '_edge_slot_432_45') else b[number('region.invariants_numbers', '_edge_slot_432_54')] + b[number('region.invariants_numbers', '_edge_slot_432_61')] + gap
                y0 = ew["c"] + ROW // number('region.invariants_numbers', '_edge_slot_433_38') if sy > number('region.invariants_numbers', '_edge_slot_433_48') else ew["c"] - ROW // number('region.invariants_numbers', '_edge_slot_433_72') - ld
                lot = (x0, y0, lw, ld)
                if x0 < number('region.invariants_numbers', '_edge_slot_435_24') or x0 + lw > top or y0 < number('region.invariants_numbers', '_edge_slot_435_51') or y0 + ld > top: continue
                if rail_hits(rail, lot) or any(overlaps(lot, o) for o in occupied): continue
                return lot, (lambda x, u, w, h, x0=x0, y0=y0, sy=sy: (x0 + x, y0 + u, w, h) if sy > number('region.invariants_numbers', '_edge_slot_437_100') else (x0 + x, y0 + ld - u - h, w, h)), sy
    return None

def place_edge_biz(seed, town, roads, rail, occupied):
    """Edge-of-town / highway businesses fronting the E-W blacktop just past the town edge (CRG siting; PLACEHOLDER sizes)."""
    ew = next(r for r in roads if r["type"] == "paved" and r["axis"] == "h"); b = town["box"]; top = N_MI * MILE
    for k, typ in enumerate(town["edge_want"]):
        lw, ld, bw, bd, sb = (FT(v) for v in EDGE_FP[typ]); done = False
        for step in range(number('region.place_edge_biz_numbers', 'place_edge_biz_445_26')):
            for sx in (-number('region.place_edge_biz_numbers', 'place_edge_biz_446_24'), number('region.place_edge_biz_numbers', 'place_edge_biz_446_27')):
                for sy in (number('region.place_edge_biz_numbers', 'place_edge_biz_447_27'), -number('region.place_edge_biz_numbers', 'place_edge_biz_447_31')):
                    gap = FT(number('region.place_edge_biz_numbers', 'place_edge_biz_448_29')) + step * FT(number('region.place_edge_biz_numbers', 'place_edge_biz_448_45'))
                    x0 = b[number('region.place_edge_biz_numbers', 'place_edge_biz_449_27')] - gap - lw if sx < number('region.place_edge_biz_numbers', 'place_edge_biz_449_49') else b[number('region.place_edge_biz_numbers', 'place_edge_biz_449_58')] + b[number('region.place_edge_biz_numbers', 'place_edge_biz_449_65')] + gap
                    y0 = ew["c"] + ROW // number('region.place_edge_biz_numbers', 'place_edge_biz_450_42') if sy > number('region.place_edge_biz_numbers', 'place_edge_biz_450_52') else ew["c"] - ROW // number('region.place_edge_biz_numbers', 'place_edge_biz_450_76') - ld
                    lot = (x0, y0, lw, ld)
                    if x0 < number('region.place_edge_biz_numbers', 'place_edge_biz_452_28') or x0 + lw > top or y0 < number('region.place_edge_biz_numbers', 'place_edge_biz_452_55') or y0 + ld > top: continue
                    if rail_hits(rail, lot) or any(overlaps(lot, o) for o in occupied): continue
                    L = lambda x, u, w, h: (x0 + x, y0 + u, w, h) if sy > number('region.place_edge_biz_numbers', 'place_edge_biz_454_74') else (x0 + x, y0 + ld - u - h, w, h)
                    bx = (lw - bw) // number('region.place_edge_biz_numbers', 'place_edge_biz_455_38')
                    rec = {"id": f"edge{k}", "type": typ, "site": "edge of town on " + ew["name"], "lot": lot, "bldg": L(bx, sb, bw, bd),
                           "parking": [L(number('region.place_edge_biz_numbers', 'place_edge_biz_457_41'), number('region.place_edge_biz_numbers', 'place_edge_biz_457_44'), lw, sb)] if typ in ("implement dealer", "lumberyard") else [L(max(number('region.place_edge_biz_numbers', 'place_edge_biz_457_113'), bx - FT(number('region.place_edge_biz_numbers', 'place_edge_biz_457_124'))), number('region.place_edge_biz_numbers', 'place_edge_biz_457_130'), min(lw, bw + FT(number('region.place_edge_biz_numbers', 'place_edge_biz_457_149'))), sb)],
                           "curb": None, "canopy": None, "pumps": [], "storefront": False, "vacant": False, "front_y": ew["c"] + sy * (ROW // number('region.place_edge_biz_numbers', 'place_edge_biz_458_142'))}
                    if typ == "school": rec["field"] = L(FT(number('region.place_edge_biz_numbers', 'place_edge_biz_459_60')), sb + bd + FT(number('region.place_edge_biz_numbers', 'place_edge_biz_459_78')), FT(number('region.place_edge_biz_numbers', 'place_edge_biz_459_86')), min(FT(number('region.place_edge_biz_numbers', 'place_edge_biz_459_99')), ld - sb - bd - FT(number('region.place_edge_biz_numbers', 'place_edge_biz_459_123'))))
                    if typ in ("implement dealer", "lumberyard"): rec["yard"] = "gravel"
                    town["commercial"].append(rec); occupied.append(lot); done = True; break
                if done: break
            if done: break
        if not done: town["unplaced"].append(typ)



# ---------- region ----------
def make_region(seed, force_town=False, overrides=None, force_rail=None, force_pop=None, road_factory=None, rail_factory=None):
    roads, pct = (road_factory or make_roads)(seed, force_town)
    rail = (rail_factory or make_rail)(seed, roads, force_rail)
    creeks = make_creeks(seed, roads)
    bar = [rail] + creeks if rail else list(creeks)     # rail corridor + floodplains: no lots or parcels
    town = make_town(seed, roads, rail, force_town, force_pop, creeks)
    d = rng(seed, "region")
    clear = FT(CLEAR[number('region.make_region_numbers', 'make_region_476_21')] + d.int("clear", CLEAR[number('region.make_region_numbers', 'make_region_476_47')] - CLEAR[number('region.make_region_numbers', 'make_region_476_58')] + number('region.make_region_numbers', 'make_region_476_63')))
    allroads = roads + (town["streets"] if town else [])
    occupied = [road_rect(r) for r in allroads]
    parcels, depot, siding = [], None, None
    if town:
        occupied.append(town["box"])
        if town["rail_town"]:
            el, depot, siding = place_rail_features(seed, town, rail, occupied, bar)
            if el: parcels.append(el); occupied.append(el["rect"])
            if el:   # co-op office inside the elevator parcel, at the corner nearest the blacktop, with a parking pad
                e = el["rect"]; ow, oh = COOP_OFFICE; near_top = abs(e[number('region.make_region_numbers', 'make_region_486_71')] + e[number('region.make_region_numbers', 'make_region_486_78')] - town["Yc"]) < abs(e[number('region.make_region_numbers', 'make_region_486_103')] - town["Yc"])
                ox = e[number('region.make_region_numbers', 'make_region_487_23')] + FT(number('region.make_region_numbers', 'make_region_487_31')); oy = e[number('region.make_region_numbers', 'make_region_487_43')] + e[number('region.make_region_numbers', 'make_region_487_50')] - FT(number('region.make_region_numbers', 'make_region_487_58')) - oh if near_top else e[number('region.make_region_numbers', 'make_region_487_86')] + FT(number('region.make_region_numbers', 'make_region_487_94'))
                el["office"] = (ox, oy, ow, oh)
                el["office_parking"] = (ox + ow, oy, FT(number('region.make_region_numbers', 'make_region_489_56')), oh)
            if el:   # industry (feed mill / scales) lining the corridor beside the elevator
                e = el["rect"]; iw, ih = INDUSTRY
                for iy in (e[number('region.make_region_numbers', 'make_region_492_29')] + e[number('region.make_region_numbers', 'make_region_492_36')] + FT(number('region.make_region_numbers', 'make_region_492_44')), e[number('region.make_region_numbers', 'make_region_492_51')] - FT(number('region.make_region_numbers', 'make_region_492_59')) - ih):
                    ir = (e[number('region.make_region_numbers', 'make_region_493_28')], iy, iw, ih)
                    if not rail_hits(bar, ir) and not any(overlaps(ir, o) for o in occupied):
                        parcels.append({"kind": "industry", "name": "Feed mill / scales", "rect": ir, "acres": acres(iw, ih)}); occupied.append(ir); break
    if town and not town["rail_town"] and "elevator" in town["program"]:   # truck-served co-op elevator at the research rate
        ew_ = next(r for r in roads if r["type"] == "paved" and r["axis"] == "h")
        slot = _edge_slot(town, ew_, FT(number('region.make_region_numbers', 'make_region_498_40')), FT(number('region.make_region_numbers', 'make_region_498_49')), bar, occupied, (number('region.make_region_numbers', 'make_region_498_71'), number('region.make_region_numbers', 'make_region_498_74')))
        if slot:
            lot, L, sy = slot; ow, oh = COOP_OFFICE
            el = {"kind": "elevator", "name": "Co-op elevator (truck)", "rect": lot, "acres": acres(lot[number('region.make_region_numbers', 'make_region_501_104')], lot[number('region.make_region_numbers', 'make_region_501_112')]), "truck": True,
                  "office": L(FT(number('region.make_region_numbers', 'make_region_502_33')), FT(number('region.make_region_numbers', 'make_region_502_41')), ow, oh), "office_parking": L(FT(number('region.make_region_numbers', 'make_region_502_78')) + ow, number('region.make_region_numbers', 'make_region_502_88'), FT(number('region.make_region_numbers', 'make_region_502_94')), FT(number('region.make_region_numbers', 'make_region_502_102')) + oh)}
            parcels.append(el); occupied.append(lot)
        else: town["unplaced"].append("elevator")
    if town: place_edge_biz(seed, town, roads, bar, occupied)
    ew = next((r for r in roads if r["type"] == "paved" and r["axis"] == "h"), None)
    # cemetery on the E-W blacktop just outside town (Struble pattern), or on any road
    cr = ew or roads[number('region.make_region_numbers', 'make_region_508_21')]
    base_x = (town["box"][number('region.make_region_numbers', 'make_region_509_26')] if town else cr["c"] + MILE) - FT(number('region.make_region_numbers', 'make_region_509_63') + number('region.make_region_numbers', 'make_region_509_69') * d.int("cem", number('region.make_region_numbers', 'make_region_509_88'))) - CEM[number('region.make_region_numbers', 'make_region_509_98')]
    cem = (base_x, cr["c"] + ROW // number('region.make_region_numbers', 'make_region_510_36'), *CEM) if cr["axis"] == "h" else None
    if cem and not rail_hits(bar, cem) and not any(overlaps(cem, o) for o in occupied):
        parcels.append({"kind": "cemetery", "name": "St. Joseph Cemetery", "rect": cem, "acres": acres(*CEM), "road": cr["name"]})
        occupied.append(cem)
    # fuel station on a blacktop corner outside town
    paved = [r for r in roads if r["type"] == "paved"]
    crossings = [(p, q) for p in paved for q in roads if q["axis"] != p["axis"] and not (town and q["type"] == "paved")]
    gas = next((c for c in town["commercial"] if c["type"] == "gas station"), None) if town else None
    if gas:   # the region's fuel station IS the town gas station (no duplicate)
        parcels.append({"kind": "fuel", "name": "Gas station (town)", "rect": gas["lot"], "acres": acres(gas["lot"][number('region.make_region_numbers', 'make_region_519_116')], gas["lot"][number('region.make_region_numbers', 'make_region_519_131')]),
                        "road": ew["name"] if ew else "", "in_town": True})
    elif crossings:
        p, q = crossings[d.int("fuel", len(crossings))]
        X, Y = (q["c"], p["c"]) if p["axis"] == "h" else (p["c"], q["c"])
        sx, sy = (number('region.make_region_numbers', 'make_region_524_18'), number('region.make_region_numbers', 'make_region_524_21')) if d.chance("fq1", number('region.make_region_numbers', 'make_region_524_43')) else (-number('region.make_region_numbers', 'make_region_524_54'), number('region.make_region_numbers', 'make_region_524_57'))
        if X == number('region.make_region_numbers', 'make_region_525_16'): sx = number('region.make_region_numbers', 'make_region_525_24')
        if X == N_MI * MILE: sx = -number('region.make_region_numbers', 'make_region_526_35')
        if Y == N_MI * MILE: sy = -number('region.make_region_numbers', 'make_region_527_35')
        r = (X + ROW // number('region.make_region_numbers', 'make_region_528_24') if sx > number('region.make_region_numbers', 'make_region_528_34') else X - ROW // number('region.make_region_numbers', 'make_region_528_52') - FUEL[number('region.make_region_numbers', 'make_region_528_61')], Y + ROW // number('region.make_region_numbers', 'make_region_528_76') if sy > number('region.make_region_numbers', 'make_region_528_86') else Y - ROW // number('region.make_region_numbers', 'make_region_528_104') - FUEL[number('region.make_region_numbers', 'make_region_528_113')], *FUEL)
        if not any(overlaps(r, o) for o in occupied) and not rail_hits(bar, r):
            parcels.append({"kind": "fuel", "name": "Fuel / co-op station", "rect": r, "acres": acres(*FUEL), "road": p["name"]})
            occupied.append(r)
    # country church on a gravel road
    gr = [r for r in roads if r["type"] == "gravel" and number('region.make_region_numbers', 'make_region_533_56') < r["c"] < N_MI * MILE]
    for t in range(number('region.make_region_numbers', 'make_region_534_19')):
        dd = rng(seed, "church", t)
        w, h = FT(dd.choice("w", list(range(number('region.make_region_numbers', 'make_region_536_44'), number('region.make_region_numbers', 'make_region_536_49'), number('region.make_region_numbers', 'make_region_536_54'))))), FT(dd.choice("h", list(range(number('region.make_region_numbers', 'make_region_536_91'), number('region.make_region_numbers', 'make_region_536_96'), number('region.make_region_numbers', 'make_region_536_101')))))
        if not CHURCH_AC[number('region.make_region_numbers', 'make_region_537_25')] <= acres(w, h) <= CHURCH_AC[number('region.make_region_numbers', 'make_region_537_56')]: continue
        r0 = gr[dd.int("road", len(gr))]; m = dd.int("m", N_MI)
        pos = m * MILE + clear + ROW // number('region.make_region_numbers', 'make_region_539_40') + dd.int("pos", number('region.make_region_numbers', 'make_region_539_58')) * FT(number('region.make_region_numbers', 'make_region_539_67'))
        rect = (pos, r0["c"] + ROW // number('region.make_region_numbers', 'make_region_540_38'), w, h) if r0["axis"] == "h" else (r0["c"] + ROW // number('region.make_region_numbers', 'make_region_540_91'), pos, h, w)
        if not any(overlaps(rect, o) for o in occupied) and not rail_hits(bar, rect):
            parcels.append({"kind": "church", "name": "Country church", "rect": rect, "acres": acres(w, h), "road": r0["name"]})
            occupied.append(rect); break
    # rural lots: farmsteads ~1.2 per road mile, plus acreages
    raw = []
    for r in roads:
        for m in range(N_MI):
            dm = rng(seed, r["id"], m)
            items = [("f", k) for k in range(dm.weighted("farms", FARM_PER_MILE))] + ([("a", number('region.make_region_numbers', 'make_region_549_93'))] if dm.chance("acre", ACREAGE_PCT) else [])
            for typ, k in items:
                lid = f"{r['id']}:{m}:{typ}{k}"
                for t in range(number('region.make_region_numbers', 'make_region_552_31')):
                    dl = rng(seed, lid, t)
                    if typ == "f": W, Dp, lo_ac, hi_ac = FT(dl.choice("w", list(FARM_W))), FT(dl.choice("d", list(FARM_D))), *FARM_AC
                    else: W, Dp, lo_ac, hi_ac = FT(dl.choice("w", list(ACRE_W))), FT(dl.choice("d", list(ACRE_D))), *ACRE_AC
                    if not lo_ac <= acres(W, Dp) <= hi_ac: continue
                    side = number('region.make_region_numbers', 'make_region_557_27') if dl.chance("side", number('region.make_region_numbers', 'make_region_557_50')) else -number('region.make_region_numbers', 'make_region_557_60')
                    if (r["c"] == number('region.make_region_numbers', 'make_region_558_34') and side < number('region.make_region_numbers', 'make_region_558_47')) or (r["c"] == N_MI * MILE and side > number('region.make_region_numbers', 'make_region_558_87')): side = -side
                    a0 = m * MILE + ROW // number('region.make_region_numbers', 'make_region_559_43') + clear; a1 = (m + number('region.make_region_numbers', 'make_region_559_64')) * MILE - ROW // number('region.make_region_numbers', 'make_region_559_83') - clear - W
                    pos = a0 + dl.int("pos", max(number('region.make_region_numbers', 'make_region_560_49'), (a1 - a0) // FT(number('region.make_region_numbers', 'make_region_560_68')))) * FT(number('region.make_region_numbers', 'make_region_560_79'))
                    if r["axis"] == "h":
                        place = {"x0": pos, "y0": r["c"] + ROW // number('region.make_region_numbers', 'make_region_562_66') if side > number('region.make_region_numbers', 'make_region_562_78') else r["c"] - ROW // number('region.make_region_numbers', 'make_region_562_101') - Dp, "facing": "S" if side > number('region.make_region_numbers', 'make_region_562_133') else "N"}
                    else:
                        place = {"x0": r["c"] + ROW // number('region.make_region_numbers', 'make_region_564_55') if side > number('region.make_region_numbers', 'make_region_564_67') else r["c"] - ROW // number('region.make_region_numbers', 'make_region_564_90') - Dp, "y0": pos, "facing": "W" if side > number('region.make_region_numbers', 'make_region_564_133') else "E"}
                    wr = world_rect(place, W, Dp)
                    if wr[number('region.make_region_numbers', 'make_region_566_26')] < number('region.make_region_numbers', 'make_region_566_31') or wr[number('region.make_region_numbers', 'make_region_566_39')] < number('region.make_region_numbers', 'make_region_566_44') or wr[number('region.make_region_numbers', 'make_region_566_52')] + wr[number('region.make_region_numbers', 'make_region_566_60')] > N_MI * MILE or wr[number('region.make_region_numbers', 'make_region_566_83')] + wr[number('region.make_region_numbers', 'make_region_566_91')] > N_MI * MILE: continue
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
            fx, fy = to_world(L["place"], L["w"], L["d"], L["w"] // number('region.make_region_numbers', 'make_region_577_68'), -FT(number('region.make_region_numbers', 'make_region_577_75')))
            L["road"] = next(x for x in allroads if inside((fx, fy), road_rect(x)))
            raw.append(L)
    year = region_year(seed)
    lots = []
    for L in raw:
        if overrides and L["id"] in overrides: L["w"] = FT(overrides[L["id"]])
        W, Dp, pl = L["w"], L["d"], L["place"]
        others = [x for x in allroads if x is not L["road"]]
        corner, side_street = None, None
        for side, x in (("left", -FT(number('region.make_region_numbers', 'make_region_587_37'))), ("right", W + FT(number('region.make_region_numbers', 'make_region_587_59')))):
            hit = [x2 for x2 in others if inside(to_world(pl, W, Dp, x, Dp // number('region.make_region_numbers', 'make_region_588_78')), road_rect(x2))]
            if hit: corner, side_street = side, hit[number('region.make_region_numbers', 'make_region_589_52')]["name"]
        spec = make_lot(L["id"], W / FT(number('region.make_region_numbers', 'make_region_590_40')), seed, Dp / FT(number('region.make_region_numbers', 'make_region_590_58')), corner_side=corner, year=year, address_street=L["road"]["name"],
                        side_street=side_street, road_type=L["road"]["type"], setting=L["setting"])
        ov = {"kind": L["kind"]}
        dl = rng(seed, L["id"], "sb")
        if L["kind"] == "town":
            sb = town["setbacks"]
            ov.update(front_setback=sb["front"], front_min=FT(TOWN_SB["front"][number('region.make_region_numbers', 'make_region_596_79')]), rear_setback=sb["rear"], rear_min=FT(TOWN_SB["rear"][number('region.make_region_numbers', 'make_region_596_137')]),
                      acc_rear=FT(number('region.make_region_numbers', 'make_region_597_34') + dl.int("ar", number('region.make_region_numbers', 'make_region_597_51'))), base_lots=L["base_lots"], house_type=L["house_type"], alley_rear=True, use=L.get("use", "house"))
            for k in ("left", "right"):
                c = corner == k
                ov[f"side_{k}"] = sb["corner"] if c else sb["side"]
                ov[f"side_{k}_min"] = FT(TOWN_SB["corner" if c else "side"][number('region.make_region_numbers', 'make_region_601_76')])
        else:
            # grove on whichever lot edges face north and west (never the road side)
            gs = []
            for k, (x, y) in (("left", (-FT(number('region.make_region_numbers', 'make_region_605_44')), Dp // number('region.make_region_numbers', 'make_region_605_55'))), ("right", (W + FT(number('region.make_region_numbers', 'make_region_605_78')), Dp // number('region.make_region_numbers', 'make_region_605_89'))), ("rear", (W // number('region.make_region_numbers', 'make_region_605_109'), Dp + FT(number('region.make_region_numbers', 'make_region_605_120'))))):
                mx, my = to_world(pl, W, Dp, W // number('region.make_region_numbers', 'make_region_606_50'), Dp // number('region.make_region_numbers', 'make_region_606_59')); px, py = to_world(pl, W, Dp, x, y)
                if py - my > abs(px - mx) or mx - px > abs(py - my): gs.append(k)
            back = FT(ACRE_BACK) if L["kind"] == "acreage" else min(FT(FARM_BACK[number('region.make_region_numbers', 'make_region_608_81')] + number('region.make_region_numbers', 'make_region_608_86') * dl.int("back", number('region.make_region_numbers', 'make_region_608_106'))), Dp - FT(number('region.make_region_numbers', 'make_region_608_120') + number('region.make_region_numbers', 'make_region_608_125')))
            back = max(back, FT(FARM_BACK[number('region.make_region_numbers', 'make_region_609_42')])) if L["kind"] == "farmstead" else back
            ov.update(front_setback=back, front_min=back, rear_setback=spec["depth"] // number('region.make_region_numbers', 'make_region_610_88'), rear_min=spec["depth"] // number('region.make_region_numbers', 'make_region_610_118'))
            if L["kind"] == "farmstead":
                ov["grove_sides"] = gs
                for k in ("left", "right"):
                    if k in gs: ov[f"side_{k}"] = ov[f"side_{k}_min"] = max(spec[f"side_{k}"], GROVE_W + FT(number('region.make_region_numbers', 'make_region_614_108')))
                if "rear" in gs: ov["rear_setback"] = ov["rear_min"] = GROVE_W + FT(number('region.make_region_numbers', 'make_region_615_84'))
        spec.update(ov)
        lots.append({"spec": spec, "place": pl, "road_id": L["road"]["id"], "along": L["along"]})
    lots.sort(key=lambda l: (l["road_id"], l["along"], l["spec"]["id"]))
    paved_len = sum(r["hi"] - r["lo"] for r in roads if r["type"] == "paved"); tot = sum(r["hi"] - r["lo"] for r in roads)
    result = {"seed": seed, "creeks": creeks, "rail": rail, "roads": allroads, "grid_roads": roads, "town": town, "lots": lots, "parcels": parcels,
            "depot": depot, "siding": siding, "clear": clear, "paved_pct_drawn": pct, "paved_share": paved_len / tot,
            "road_miles": tot / MILE}
    from commercial_layout import arrange
    arrange(result)
    return result

# ---------- rule 35 ----------
def signature(plan):
    P = plan.program
    att = [r for r in plan.rooms if not (r.kind == "garage" and plan.site.get("detached_garage"))]
    fy = min(r.y for r in att); x0 = min(r.x for r in att); x1 = max(r.x + r.w for r in att)
    wins = tuple(w.ro_w // IN(number('region.invariants_numbers', 'signature_629_30')) for w in sorted((w for w in plan.windows if w.y1 == w.y2 == fy), key=lambda w: w.x1))
    return {"roof": P["roof"], "roof_h": P["roof_h"], "front_w": (x1 - x0) // FT(number('region.invariants_numbers', 'signature_630_81')),
            "silhouette": ((max(r.y + r.h for r in att) - fy) // FT(number('region.invariants_numbers', 'signature_631_68')), bool(plan.site.get("porch"))),
            "front_windows": min(wins, wins[::-number('region.invariants_numbers', 'signature_632_47')]),
            "garage_pos": {"double": "front", "single": "front", "detached": "rear", "none": "none"}[P["garage_built"]],
            "siding": P["siding"]}

def check35(plan, prev):
    s = signature(plan); worst = None
    for pid, p in prev:
        diff = sum(s[k] != v for k, v in signature(p).items())
        if diff < number('region.invariants_numbers', 'check35_640_18') and (worst is None or diff < worst[number('region.invariants_numbers', 'check35_640_55')]): worst = (pid, diff)
    if worst: return {"35": ("fail", f"only {worst[number('region.invariants_numbers', 'check35_641_51')]} of 7 features differ from {worst[number('region.invariants_numbers', 'check35_641_88')]}")}
    return {"35": ("pass", f"differs in >=3 features from {len(prev)} earlier lot(s) on this road")}

def spawn_region(region, rule35=True, max_attempts=number('region.invariants_numbers', 'spawn_region_644_51')):
    out, done = {}, []
    for L in region["lots"]:
        prev = [(d["spec"]["id"], out[d["spec"]["id"]]["plan"]) for d in done
                if d["road_id"] == L["road_id"] and out[d["spec"]["id"]]["plan"]][-number('region.invariants_numbers', 'spawn_region_648_83'):]
        extra = (lambda p, prev=prev: check35(p, prev)) if rule35 else None
        plan, rep, att, fails = spawn(L["spec"], max_attempts, extra=extra)
        out[L["spec"]["id"]] = {"plan": plan, "report": rep, "attempts": att, "fails": fails}
        done.append(L)
    return out

from settings import configurable as _configurable
make_region = _configurable(make_region)
spawn_region = _configurable(spawn_region)
