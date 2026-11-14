"""Builder Guy building spawner, 2D prototype (research only).

Lot spec + seed -> one-story floor plan on a module grid -> validator -> retry.
All geometry is integer 1/16-in ticks. y grows away from the road (road is at y < 0).
Method: fixed adjacency template (public half + bedroom wing), seeded splits on a
2 ft grid, footprint snapped to 4 ft, child seed = hash(parent, stable key).
"""
from settings import DEFAULTS, number, option
import copy, hashlib, json
from collections import defaultdict
from dataclasses import dataclass, field, asdict

T = DEFAULTS['spawner.t']
def IN(x): return int(round(x * T))
def FT(x): return int(round(x * number('spawner.invariants_numbers', 'FT_15_32') * T))
SF = DEFAULTS['spawner.sf']
CLR = DEFAULTS['spawner.clr']
HEAD = DEFAULTS['spawner.head']
GRID = DEFAULTS['spawner.grid']
MODULE = DEFAULTS['spawner.module']

def H(*parts):
    """Stable 64-bit hash of a key path (rule 34)."""
    s = "|".join(str(p) for p in parts).encode()
    return int.from_bytes(hashlib.blake2b(s, digest_size=number('spawner.invariants_numbers', 'H_25_57')).digest(), "big")

class Draw:
    """Stateless seeded choices: each choice is hash(seed, key), so adding a choice never shifts another."""
    def __init__(self, seed): self.seed = seed
    def int(self, key, n): return H(self.seed, key) % n
    def choice(self, key, seq): return seq[self.int(key, len(seq))]
    def chance(self, key, pct): return self.int(key, number('spawner.invariants_numbers', 'Draw_32_53')) < pct
    def weighted(self, key, items):
        r = H(self.seed, key) % sum(w for _, w in items)
        for v, w in items:
            if r < w: return v
            r -= w

# ---------- data model ----------
@dataclass
class Room:
    id: str; kind: str; x: int; y: int; w: int; h: int
    closet: tuple = None     # (x, y, w, h) inside the room, bedrooms only
    fan: bool = False
    ceiling: int = IN(number('spawner.invariants_numbers', 'Room_45_22'))
    @property
    def area(self): return self.w * self.h
    def rect(self): return (self.x, self.y, self.w, self.h)

@dataclass
class Door:
    id: str; a: str; b: str  # b == "outside" for exterior doors
    x1: int; y1: int; x2: int; y2: int
    w: int; h: int; ro: int; role: str   # front, rear, garage_house, interior, overhead
    hinge: str = "side"

@dataclass
class Window:
    id: str; room: str; x1: int; y1: int; x2: int; y2: int
    ro_w: int; ro_h: int; head: int = HEAD
    @property
    def sill(self): return self.head - self.ro_h

@dataclass
class Plan:
    lot: dict; program: dict
    rooms: list = field(default_factory=list)
    doors: list = field(default_factory=list)
    windows: list = field(default_factory=list)
    site: dict = field(default_factory=dict)
    notes: list = field(default_factory=list)
    def room(self, rid): return next((r for r in self.rooms if r.id == rid), None)
    def to_json(self): return json.dumps(asdict(self), sort_keys=True)
    def digest(self): return hashlib.sha256(self.to_json().encode()).hexdigest()[:number('spawner.invariants_numbers', 'Plan_74_82')]

HABITABLE = DEFAULTS['spawner.habitable']
PUBLIC = DEFAULTS['spawner.public']

# ---------- geometry ----------
def shared(a, b):
    """Shared wall between two rects: (axis, coord, lo, hi) or None. axis 'h' = wall along x."""
    if a.x + a.w == b.x or b.x + b.w == a.x:
        c = a.x + a.w if a.x + a.w == b.x else a.x
        lo, hi = max(a.y, b.y), min(a.y + a.h, b.y + b.h)
        if hi > lo: return ("v", c, lo, hi)
    if a.y + a.h == b.y or b.y + b.h == a.y:
        c = a.y + a.h if a.y + a.h == b.y else a.y
        lo, hi = max(a.x, b.x), min(a.x + a.w, b.x + b.w)
        if hi > lo: return ("h", c, lo, hi)
    return None

def _subtract(lo, hi, cuts):
    out, cur = [], lo
    for a, b in sorted(cuts):
        if a > cur: out.append((cur, min(a, hi)))
        cur = max(cur, b)
    if cur < hi: out.append((cur, hi))
    return [(a, b) for a, b in out if b > a]

def ext_segments(room, rooms):
    """Exterior wall pieces of a room: (side, axis, coord, lo, hi). 'front' faces the road."""
    edges = [("front", "h", room.y, room.x, room.x + room.w), ("rear", "h", room.y + room.h, room.x, room.x + room.w),
             ("left", "v", room.x, room.y, room.y + room.h), ("right", "v", room.x + room.w, room.y, room.y + room.h)]
    out = []
    for side, axis, c, lo, hi in edges:
        cuts = [(s[number('spawner.invariants_numbers', 'ext_segments_106_19')], s[number('spawner.invariants_numbers', 'ext_segments_106_25')]) for o in rooms if o is not room
                for s in [shared(room, o)] if s and s[number('spawner.invariants_numbers', 'ext_segments_107_54')] == axis and s[number('spawner.invariants_numbers', 'ext_segments_107_71')] == c]
        out += [(side, axis, c, a, b) for a, b in _subtract(lo, hi, cuts)]
    return out

def seg_xy(axis, c, a, b): return (a, c, b, c) if axis == "h" else (c, a, c, b)

def slot(lo, hi, ro, d, key, centered=False):
    """Place an opening of width ro inside [lo, hi] keeping CLR each side. Returns (a, b) or None."""
    clo, chi = lo + CLR + ro // number('spawner.invariants_numbers', 'slot_115_32'), hi - CLR - ro // number('spawner.invariants_numbers', 'slot_115_52')
    if chi < clo: return None
    if centered: c = (clo + chi) // number('spawner.invariants_numbers', 'slot_117_36')
    else:
        step = IN(number('spawner.invariants_numbers', 'slot_119_18')); c = clo + d.int(key, (chi - clo) // step + number('spawner.invariants_numbers', 'slot_119_65')) * step
    return (c - ro // number('spawner.invariants_numbers', 'slot_120_22'), c - ro // number('spawner.invariants_numbers', 'slot_120_35') + ro)

# ---------- program draws (rules 21-24, 4, 30) ----------
def by_year(table, y): return table[min(table, key=lambda k: (abs(k - y), k))]
STYLE = DEFAULTS['spawner.style']
BEDS = DEFAULTS['spawner.beds']
BATHS = DEFAULTS['spawner.baths']
FOUND = DEFAULTS['spawner.found']
GARAGE = DEFAULTS['spawner.garage']
OUTDOOR = DEFAULTS['spawner.outdoor']

def draw_program(d, lot, force=None):
    y = lot["year"]
    p = {"year": y,
         "style": d.weighted("style", list(zip(["ranch", "two_story", "split_level"], by_year(STYLE, y)))),
         "beds": d.weighted("beds", list(zip(["le2", number('spawner.draw_program_numbers', 'draw_program_135_53'), number('spawner.draw_program_numbers', 'draw_program_135_56')], by_year(BEDS, y)))),
         "baths": d.weighted("baths", list(zip([number('spawner.draw_program_numbers', 'draw_program_136_48'), number('spawner.draw_program_numbers', 'draw_program_136_51')], by_year(BATHS, y)))),
         "area_sf": number('spawner.draw_program_numbers', 'draw_program_137_20') + d.int("area", number('spawner.draw_program_numbers', 'draw_program_137_41')),
         "foundation": d.weighted("found", list(zip(["basement", "slab", "crawl"], by_year(FOUND, y)))),
         "garage": d.weighted("garage", list(zip(["none", "single", "double"], by_year(GARAGE, y)))),
         "dining": d.chance("dining", number('spawner.draw_program_numbers', 'draw_program_140_38')), "mudroom": d.chance("mud", number('spawner.draw_program_numbers', 'draw_program_140_70'))}
    if p["beds"] == "le2": p["beds"] = number('spawner.draw_program_numbers', 'draw_program_141_39') if d.chance("onebed", number('spawner.draw_program_numbers', 'draw_program_141_63')) else number('spawner.draw_program_numbers', 'draw_program_141_72')   # 1-bed share NOT VERIFIED
    po, de, pa = by_year(OUTDOOR, y)
    p["porch"], p["deck"] = d.chance("porch", po), d.chance("deck", de)
    p["patio"] = (not p["deck"]) and d.chance("patio", pa)
    p["mirror"] = d.chance("mirror", number('spawner.draw_program_numbers', 'draw_program_145_37'))
    # exterior draws used only by rule 35 (look-alikes). PLACEHOLDER choice lists.
    p["roof"] = d.choice("roof", ["gable", "hip"])
    p["roof_h"] = d.choice("roof_h", ["4/12", "5/12", "6/12"])
    p["siding"] = d.choice("siding", ["vinyl lap", "wood lap", "brick front"])
    p.update(force or {})
    return p

GARAGE_CHAIN = DEFAULTS['spawner.garage_chain']
GW = DEFAULTS['spawner.gw']

# ---------- layout ----------
def build(d, lot, prog, gtype):
    """One layout for a given garage type, or None if it doesn't fit the lot."""
    # PLACEHOLDER size scaling: lots with < 70 ft buildable width get the smaller room sizes
    narrow = lot["width"] - lot["side_left"] - lot["side_right"] < FT(number('spawner.build_numbers', 'build_160_70'))
    KW, LW, BW = ([FT(number('spawner.build_numbers', 'build_161_22')), FT(number('spawner.build_numbers', 'build_161_30'))], [FT(number('spawner.build_numbers', 'build_161_40')), FT(number('spawner.build_numbers', 'build_161_48'))], [FT(number('spawner.build_numbers', 'build_161_58')), FT(number('spawner.build_numbers', 'build_161_66'))]) if narrow else \
                 ([FT(number('spawner.build_numbers', 'build_162_22')), FT(number('spawner.build_numbers', 'build_162_30')), FT(number('spawner.build_numbers', 'build_162_38'))], [FT(number('spawner.build_numbers', 'build_162_48')), FT(number('spawner.build_numbers', 'build_162_56')), FT(number('spawner.build_numbers', 'build_162_64'))], [FT(number('spawner.build_numbers', 'build_162_74')), FT(number('spawner.build_numbers', 'build_162_82')), FT(number('spawner.build_numbers', 'build_162_90'))])
    Kw = d.choice("kw", KW); Lw = d.choice("lw", LW)
    nb = prog["beds"]; nf = (nb + number('spawner.build_numbers', 'build_164_34')) // number('spawner.build_numbers', 'build_164_40'); nr = nb - nf
    front = [d.choice(f"bw{i}", BW) for i in range(nf)]
    rear_beds = [d.choice(f"bw{nf+i}", BW) for i in range(nr)]
    baths = [("bath1", d.choice("b1w", [FT(number('spawner.build_numbers', 'build_167_43')), FT(number('spawner.build_numbers', 'build_167_50'))]))] + ([("bath2", FT(number('spawner.build_numbers', 'build_167_74')))] if prog["baths"] >= number('spawner.build_numbers', 'build_167_99') else [])
    Wp = Kw + Lw
    Ww = max(sum(front), sum(w for _, w in baths) + sum(rear_beds))
    Ww += (-(Wp + Ww)) % MODULE
    front[-number('spawner.build_numbers', 'build_171_11')] += Ww - sum(front)
    rest = Ww - sum(w for _, w in baths) - sum(rear_beds)
    if rear_beds: rear_beds[-number('spawner.build_numbers', 'build_173_29')] += rest
    else: baths[-number('spawner.build_numbers', 'build_174_17')] = (baths[-number('spawner.build_numbers', 'build_174_30')][number('spawner.build_numbers', 'build_174_33')], baths[-number('spawner.build_numbers', 'build_174_44')][number('spawner.build_numbers', 'build_174_47')] + rest)
    Wh = Wp + Ww
    target = prog["area_sf"] * SF
    Df, Dr = min(((a, b) for a in (FT(number('spawner.build_numbers', 'build_177_38')), FT(number('spawner.build_numbers', 'build_177_46'))) for b in (FT(number('spawner.build_numbers', 'build_177_64')), FT(number('spawner.build_numbers', 'build_177_72')))),
                 key=lambda ab: (abs(Wh * (ab[number('spawner.build_numbers', 'build_178_46')] + ab[number('spawner.build_numbers', 'build_178_54')]) - target), ab))
    D = Df + Dr
    attached = gtype in ("single", "double")
    P0 = GW[gtype] if attached else number('spawner.build_numbers', 'build_181_36')
    need_util = prog["foundation"] != "basement"
    U = "utility" if need_util else ("mudroom" if prog["mudroom"] and attached else None)
    Uw = d.choice("uw", [FT(number('spawner.build_numbers', 'build_184_28')), FT(number('spawner.build_numbers', 'build_184_35'))]) if U else number('spawner.build_numbers', 'build_184_50')

    R = []
    if attached: R.append(Room("garage", "garage", number('spawner.build_numbers', 'build_187_51'), number('spawner.build_numbers', 'build_187_54'), GW[gtype], FT(number('spawner.build_numbers', 'build_187_71'))))
    R += [Room("kitchen", "kitchen", P0, number('spawner.build_numbers', 'build_188_41'), Kw, Df), Room("living", "living", P0 + Kw, number('spawner.build_numbers', 'build_188_87'), Lw, Df)]
    if U: R.append(Room(U, U, P0, Df, Uw, Dr))
    pub = "dining" if prog["dining"] else "family"
    R.append(Room(pub, pub, P0 + Uw, Df, Wp - Uw, Dr))
    x0 = P0 + Wp
    R.append(Room("hall", "hall", x0, Df - FT(number('spawner.build_numbers', 'build_193_46')), Ww, FT(number('spawner.build_numbers', 'build_193_57'))))
    beds, x, n = [], x0, number('spawner.build_numbers', 'build_194_25')
    for w in front:
        n += number('spawner.build_numbers', 'build_196_13'); beds.append(Room(f"bed{n}", "bedroom", x, number('spawner.build_numbers', 'build_196_58'), w, Df - FT(number('spawner.build_numbers', 'build_196_72')))); x += w
    x = x0
    for bid, w in baths:
        R.append(Room(bid, "bath", x, Df, w, Dr)); x += w
    for w in rear_beds:
        n += number('spawner.build_numbers', 'build_201_13'); beds.append(Room(f"bed{n}", "bedroom", x, Df, w, Dr)); x += w
    for b in beds:   # closet: 2 ft deep on the hall wall, leaving >= 4 ft for the door
        cw = min(b.w - IN(number('spawner.build_numbers', 'build_203_26')), IN(number('spawner.build_numbers', 'build_203_34'))); left = d.chance(f"closet:{b.id}", number('spawner.build_numbers', 'build_203_74'))
        cy = b.y + b.h - IN(number('spawner.build_numbers', 'build_204_28')) if b.y == number('spawner.build_numbers', 'build_204_42') else b.y
        b.closet = (b.x if left else b.x + b.w - cw, cy, cw, IN(number('spawner.build_numbers', 'build_205_64')))
    R += beds
    master = max(beds, key=lambda b: (b.area, [-ord(c) for c in b.id])).id
    prog = dict(prog, master=master, garage_built=gtype, dining_room=pub == "dining")
    plan = Plan(lot=copy.deepcopy(lot), program=prog, rooms=R)
    by = {r.id: r for r in R}

    def connect(a, b, role, dw, ro, lim=None):
        s = shared(by[a], by[b])
        if not s: return None
        axis, c, lo, hi = s
        if lim: lo, hi = max(lo, lim[number('spawner.build_numbers', 'build_216_37')]), min(hi, lim[number('spawner.build_numbers', 'build_216_54')])
        sl = slot(lo, hi, ro, d, f"door:{a}:{b}")
        if not sl: return None
        dr = Door(f"{a}-{b}", a, b, *seg_xy(axis, c, *sl), dw, IN(number('spawner.build_numbers', 'build_219_66')), ro, role)
        plan.doors.append(dr); return dr

    def ext(room, side, role=None, dw=IN(number('spawner.build_numbers', 'build_222_41')), ro=IN(number('spawner.build_numbers', 'build_222_52')), win=None, half=None):
        segs = [s for s in ext_segments(by[room], R) if s[number('spawner.build_numbers', 'build_223_58')] == side]
        if not segs: return None
        _, axis, c, lo, hi = max(segs, key=lambda s: s[number('spawner.build_numbers', 'build_225_55')] - s[number('spawner.build_numbers', 'build_225_62')])
        if half == number('spawner.build_numbers', 'build_226_19'): hi = (lo + hi) // number('spawner.build_numbers', 'build_226_40')
        if half == number('spawner.build_numbers', 'build_227_19'): lo = (lo + hi) // number('spawner.build_numbers', 'build_227_40')
        if win:
            sl = slot(lo, hi, win[number('spawner.build_numbers', 'build_229_34')], d, "", centered=True)
            if sl: plan.windows.append(Window(f"w:{room}:{side}", room, *seg_xy(axis, c, *sl), win[number('spawner.build_numbers', 'build_230_99')], win[number('spawner.build_numbers', 'build_230_107')]))
            return sl
        sl = slot(lo, hi, ro, d, f"ext:{room}:{role}")
        if sl: plan.doors.append(Door(f"{room}-{role}", room, "outside", *seg_xy(axis, c, *sl), dw, IN(number('spawner.build_numbers', 'build_233_103')), ro, role))
        return sl

    # interior doors (adjacency template)
    connect("kitchen", "living", "interior", IN(number('spawner.build_numbers', 'build_237_48')), IN(number('spawner.build_numbers', 'build_237_56')))
    connect("living", pub, "interior", IN(number('spawner.build_numbers', 'build_238_42')), IN(number('spawner.build_numbers', 'build_238_50')))
    connect("kitchen", pub, "interior", IN(number('spawner.build_numbers', 'build_239_43')), IN(number('spawner.build_numbers', 'build_239_51')))
    if U: connect("kitchen", U, "interior", IN(number('spawner.build_numbers', 'build_240_47')), IN(number('spawner.build_numbers', 'build_240_55')))
    connect("living", "hall", "interior", IN(number('spawner.build_numbers', 'build_241_45')), IN(number('spawner.build_numbers', 'build_241_53')))
    for b in beds:
        cx, _, cw, _ = b.closet
        lim = (cx + cw, b.x + b.w) if cx == b.x else (b.x, cx)
        connect("hall", b.id, "interior", IN(number('spawner.build_numbers', 'build_245_45')), IN(number('spawner.build_numbers', 'build_245_53')), lim)
    connect("hall", "bath1", "interior", IN(number('spawner.build_numbers', 'build_246_44')), IN(number('spawner.build_numbers', 'build_246_52')))
    if "bath2" in by:
        nxt = next((b for b in beds if b.y == Df and b.x == by["bath2"].x + by["bath2"].w), None)
        if nxt and nxt.id == master: connect(master, "bath2", "interior", IN(number('spawner.build_numbers', 'build_249_77')), IN(number('spawner.build_numbers', 'build_249_85')))
        else: connect("hall", "bath2", "interior", IN(number('spawner.build_numbers', 'build_250_54')), IN(number('spawner.build_numbers', 'build_250_62')))
    if attached and not (U and connect("garage", U, "garage_house", IN(number('spawner.build_numbers', 'build_251_71')), IN(number('spawner.build_numbers', 'build_251_79')))):
        connect("garage", "kitchen", "garage_house", IN(number('spawner.build_numbers', 'build_252_56')), IN(number('spawner.build_numbers', 'build_252_64')))
    # exterior doors
    fd = ext("living", "front", "front", half=number('spawner.build_numbers', 'build_254_46'))
    rear_room = pub if (prog["deck"] or prog["patio"]) else (U or pub)
    rd = ext(rear_room, "rear", "rear", half=number('spawner.build_numbers', 'build_256_45'))
    if attached:
        g = by["garage"]; ow = FT(number('spawner.build_numbers', 'build_258_34')) if gtype == "double" else FT(number('spawner.build_numbers', 'build_258_67'))
        a = g.x + (g.w - ow) // number('spawner.build_numbers', 'build_259_32')
        plan.doors.append(Door("garage-overhead", "garage", "outside", a, number('spawner.build_numbers', 'build_260_74'), a + ow, number('spawner.build_numbers', 'build_260_85'), ow, IN(number('spawner.build_numbers', 'build_260_95')), ow, "overhead", "overhead"))
    # windows (RO w x h); same head everywhere (rule 28)
    ext("kitchen", "front", win=(IN(number('spawner.build_numbers', 'build_262_36')), IN(number('spawner.build_numbers', 'build_262_44'))))
    ext("living", "front", win=(IN(number('spawner.build_numbers', 'build_263_35')), IN(number('spawner.build_numbers', 'build_263_43'))), half=number('spawner.build_numbers', 'build_263_54'))
    ext(pub, "rear", win=(IN(number('spawner.build_numbers', 'build_264_29')), IN(number('spawner.build_numbers', 'build_264_37'))), half=number('spawner.build_numbers', 'build_264_48') if rear_room == pub else None)
    for b in beds: ext(b.id, "front" if b.y == number('spawner.build_numbers', 'build_265_47') else "rear", win=(IN(number('spawner.build_numbers', 'build_265_70')), IN(number('spawner.build_numbers', 'build_265_78'))))
    for bid, _ in baths:
        if not ext(bid, "rear", win=(IN(number('spawner.build_numbers', 'build_267_40')), IN(number('spawner.build_numbers', 'build_267_48')))): by[bid].fan = True
    if U: by[U].fan = True
    # site: porch, deck/patio, smoke detector spots (local coords)
    site = {"porch": None, "deck": None, "deck_kind": None, "driveway": None}
    if prog["porch"] and fd:
        lv = by["living"]; cx = (fd[number('spawner.build_numbers', 'build_272_36')] + fd[number('spawner.build_numbers', 'build_272_44')]) // number('spawner.build_numbers', 'build_272_51')
        px = min(max(cx - FT(number('spawner.build_numbers', 'build_273_29')), lv.x), lv.x + lv.w - FT(number('spawner.build_numbers', 'build_273_57')))
        site["porch"] = (px, -FT(number('spawner.build_numbers', 'build_274_33')), FT(number('spawner.build_numbers', 'build_274_40')), FT(number('spawner.build_numbers', 'build_274_47')))
    if (prog["deck"] or prog["patio"]) and rd:
        cx = (rd[number('spawner.build_numbers', 'build_276_17')] + rd[number('spawner.build_numbers', 'build_276_25')]) // number('spawner.build_numbers', 'build_276_32')
        site["deck"] = (cx - FT(number('spawner.build_numbers', 'build_277_32')), D, FT(number('spawner.build_numbers', 'build_277_42')), FT(number('spawner.build_numbers', 'build_277_50'))); site["deck_kind"] = "deck" if prog["deck"] else "patio"
    hl = by["hall"]
    site["smoke"] = [(hl.x + hl.w // number('spawner.build_numbers', 'build_279_37'), hl.y + hl.h // number('spawner.build_numbers', 'build_279_55'))]
    plan.site = site
    return place_on_lot(plan, lot, gtype, Wh, D)

def _move(plan, dx, dy):
    for r in plan.rooms:
        r.x += dx; r.y += dy
        if r.closet: r.closet = (r.closet[number('spawner.invariants_numbers', '_move_286_42')] + dx, r.closet[number('spawner.invariants_numbers', '_move_286_60')] + dy, *r.closet[number('spawner.invariants_numbers', '_move_286_79'):])
    for o in plan.doors + plan.windows:
        o.x1 += dx; o.x2 += dx; o.y1 += dy; o.y2 += dy
    for k in ("porch", "deck"):
        if plan.site[k]: x, y, w, h = plan.site[k]; plan.site[k] = (x + dx, y + dy, w, h)
    plan.site["smoke"] = [(x + dx, y + dy) for x, y in plan.site["smoke"]]

def place_on_lot(plan, lot, gtype, Wh, D):
    """Fit the house between the lot's setbacks; sides may shrink to the minimums (rule 2 warn)."""
    W = lot["width"]; mir = plan.program["mirror"]
    req = (lot["side_left"], lot["side_right"]); mins = (lot["side_left_min"], lot["side_right_min"])
    if mir: req, mins = req[::-number('spawner.place_on_lot_numbers', 'place_on_lot_297_31')], mins[::-number('spawner.place_on_lot_numbers', 'place_on_lot_297_43')]          # placement happens before the mirror flip
    porch_push = FT(number('spawner.place_on_lot_numbers', 'place_on_lot_298_20')) if plan.site["porch"] and not lot["porch_may_project"] else number('spawner.place_on_lot_numbers', 'place_on_lot_298_83')
    hy = lot["front_setback"] + porch_push
    if hy + D > lot["depth"] - lot["rear_min"]: return None
    acc_l = lot["acc_side"]                          # detached garage sits acc_side from the (pre-mirror) left line
    alley = lot.get("alley_rear", False)             # town lot with an alley behind: garage takes access from the alley
    hx = None
    for l, r in (req, mins):
        if gtype == "detached" and alley:
            if Wh <= W - l - r: hx = l + (W - l - r - Wh) // number('spawner.place_on_lot_numbers', 'place_on_lot_306_61'); break
        elif gtype == "detached":
            x = max(acc_l + FT(number('spawner.place_on_lot_numbers', 'place_on_lot_308_31')), l)               # garage 12 ft + 3 ft to the house; drive runs beside it
            if x + Wh <= W - r: hx = x; break
        else:
            Wt = GW[gtype] + Wh
            if Wt <= W - l - r: hx = l + (W - l - r - Wt) // number('spawner.place_on_lot_numbers', 'place_on_lot_312_61'); break
    if hx is None: return None
    _move(plan, hx, hy)
    if gtype == "detached":
        gy = lot["depth"] - lot["acc_rear"] - FT(number('spawner.place_on_lot_numbers', 'place_on_lot_316_49'))     # back by the alley / rear line
        if gy < hy + D + FT(number('spawner.place_on_lot_numbers', 'place_on_lot_317_28')): return None
        g = Room("garage", "garage", acc_l, gy, FT(number('spawner.place_on_lot_numbers', 'place_on_lot_318_51')), FT(number('spawner.place_on_lot_numbers', 'place_on_lot_318_59'))); plan.rooms.append(g)
        a = g.x + IN(number('spawner.place_on_lot_numbers', 'place_on_lot_319_21')); dy = gy + FT(number('spawner.place_on_lot_numbers', 'place_on_lot_319_39')) if alley else gy     # alley garages open to the rear
        plan.doors.append(Door("garage-overhead", "garage", "outside", a, dy, a + FT(number('spawner.place_on_lot_numbers', 'place_on_lot_320_85')), dy, FT(number('spawner.place_on_lot_numbers', 'place_on_lot_320_96')), IN(number('spawner.place_on_lot_numbers', 'place_on_lot_320_103')), FT(number('spawner.place_on_lot_numbers', 'place_on_lot_320_111')), "overhead", "overhead"))
    plan.site["detached_garage"] = gtype == "detached"
    if plan.program["mirror"]: mirror(plan)
    site_works(plan, lot)
    return plan

def _hits(a, b): return a[number('spawner.invariants_numbers', '_hits_326_26')] < b[number('spawner.invariants_numbers', '_hits_326_33')] + b[number('spawner.invariants_numbers', '_hits_326_40')] and b[number('spawner.invariants_numbers', '_hits_326_49')] < a[number('spawner.invariants_numbers', '_hits_326_56')] + a[number('spawner.invariants_numbers', '_hits_326_63')] and a[number('spawner.invariants_numbers', '_hits_326_72')] < b[number('spawner.invariants_numbers', '_hits_326_79')] + b[number('spawner.invariants_numbers', '_hits_326_86')] and b[number('spawner.invariants_numbers', '_hits_326_95')] < a[number('spawner.invariants_numbers', '_hits_326_102')] + a[number('spawner.invariants_numbers', '_hits_326_109')]

def site_works(plan, L):
    """Drive for every house: ends at the garage door (alley garages from the alley), else at a parking pad
    beside the house. Farmsteads: lane + farmyard apron touching every vehicle building; grove on N/W edges."""
    s = plan.site; W, Dp = L["width"], L["depth"]; kind = L["kind"]
    det = s.get("detached_garage")
    att = [r for r in plan.rooms if not (r.kind == "garage" and det)]
    x0 = min(r.x for r in att); x1 = max(r.x + r.w for r in att); y0 = min(r.y for r in att); y1 = max(r.y + r.h for r in att)
    od = next((d for d in plan.doors if d.role == "overhead"), None)
    gw = L["grove_w"]; gs = L["grove_sides"]
    s["grove"] = [{"left": (number('spawner.site_works_numbers', 'site_works_337_28'), number('spawner.site_works_numbers', 'site_works_337_31'), gw, Dp), "right": (W - gw, number('spawner.site_works_numbers', 'site_works_337_61'), gw, Dp), "rear": (number('spawner.site_works_numbers', 'site_works_337_82'), Dp - gw, W, gw)}[k] for k in gs]
    s["farm"] = []
    drive = []
    pl = L["pad_len"]
    def pad_beside(pw):
        """Parking pad touching the house side with the most room, joined to the road by a strip of the same width."""
        if W - x1 >= x0 and W - x1 >= pw + FT(number('spawner.site_works_numbers', 'site_works_343_46')): px = x1
        elif x0 >= pw + FT(number('spawner.site_works_numbers', 'site_works_344_27')): px = x0 - pw
        else: px = x0                                       # no room: pad lands in front (rule 3 rejects it)
        return (px, number('spawner.site_works_numbers', 'site_works_346_20'), pw, y0 + pl)
    if kind in ("farmstead", "acreage"):
        lw, fw = L["lane_w"], L["flare_w"]
        if kind == "farmstead":
            yx0 = (gw + FT(number('spawner.site_works_numbers', 'site_works_350_27'))) if "left" in gs else FT(number('spawner.site_works_numbers', 'site_works_350_56')); yx1 = W - ((gw + FT(number('spawner.site_works_numbers', 'site_works_350_81'))) if "right" in gs else FT(number('spawner.site_works_numbers', 'site_works_350_111')))
            yy0 = y1 + FT(number('spawner.site_works_numbers', 'site_works_351_26')); yy1 = Dp - ((gw + FT(number('spawner.site_works_numbers', 'site_works_351_52'))) if "rear" in gs else FT(number('spawner.site_works_numbers', 'site_works_351_81')))
            lx = x1 + FT(number('spawner.site_works_numbers', 'site_works_352_25'))
            if lx + lw > yx1: lx = x0 - FT(number('spawner.site_works_numbers', 'site_works_353_43')) - lw
            cur = yx0
            for name, w, h in (("barn", FT(number('spawner.site_works_numbers', 'site_works_355_43')), FT(number('spawner.site_works_numbers', 'site_works_355_51'))), ("machine shed", FT(number('spawner.site_works_numbers', 'site_works_355_77')), FT(number('spawner.site_works_numbers', 'site_works_355_85'))), ("bin", FT(number('spawner.site_works_numbers', 'site_works_355_102')), FT(number('spawner.site_works_numbers', 'site_works_355_110'))), ("bin", FT(number('spawner.site_works_numbers', 'site_works_355_127')), FT(number('spawner.site_works_numbers', 'site_works_355_135')))):
                if cur < lx + lw + FT(number('spawner.site_works_numbers', 'site_works_356_38')) and cur + w > lx - FT(number('spawner.site_works_numbers', 'site_works_356_64')): cur = lx + lw + FT(number('spawner.site_works_numbers', 'site_works_356_88'))
                r = (cur, yy0 + FT(number('spawner.site_works_numbers', 'site_works_357_35')), w, h)
                if r[number('spawner.site_works_numbers', 'site_works_358_21')] + w <= yx1 and r[number('spawner.site_works_numbers', 'site_works_358_41')] + h <= yy1: s["farm"].append((name, r)); cur += w + FT(number('spawner.site_works_numbers', 'site_works_358_99'))
            s["yard"] = (yx0, yy0, yx1 - yx0, yy1 - yy0)
            drive.append((lx + lw // number('spawner.site_works_numbers', 'site_works_360_37') - fw // number('spawner.site_works_numbers', 'site_works_360_47'), number('spawner.site_works_numbers', 'site_works_360_50'), fw, FT(number('spawner.site_works_numbers', 'site_works_360_60'))))                     # flare at the road
            drive.append((lx, number('spawner.site_works_numbers', 'site_works_361_30'), lw, yy0 + FT(number('spawner.site_works_numbers', 'site_works_361_46'))))                                   # lane to the farmyard
            bx = [r for _, r in s["farm"]]
            a = min([lx] + [r[number('spawner.site_works_numbers', 'site_works_363_30')] for r in bx]); b = max([lx + lw] + [r[number('spawner.site_works_numbers', 'site_works_363_71')] + r[number('spawner.site_works_numbers', 'site_works_363_78')] for r in bx])
            drive.append((a, yy0, b - a, FT(number('spawner.site_works_numbers', 'site_works_364_44'))))                                     # farmyard apron: every building fronts it
            if od:
                a, b = min(od.x1, lx), max(od.x2, lx + lw); drive.append((a, od.y1 - FT(number('spawner.site_works_numbers', 'site_works_366_88')), b - a, FT(number('spawner.site_works_numbers', 'site_works_366_103'))))
            else:
                drive.append((x1, y0, lx - x1, pl) if lx > x1 else (lx + lw, y0, x0 - lx - lw, pl))   # pad between house and lane
        else:
            if od:
                lx = (od.x1 + od.x2) // number('spawner.site_works_numbers', 'site_works_371_40') - lw // number('spawner.site_works_numbers', 'site_works_371_50')
                drive += [(lx + lw // number('spawner.site_works_numbers', 'site_works_372_38') - fw // number('spawner.site_works_numbers', 'site_works_372_48'), number('spawner.site_works_numbers', 'site_works_372_51'), fw, FT(number('spawner.site_works_numbers', 'site_works_372_61'))), (lx, number('spawner.site_works_numbers', 'site_works_372_72'), lw, od.y1)]
                a, b = min(od.x1, lx), max(od.x2, lx + lw); drive.append((a, od.y1 - FT(number('spawner.site_works_numbers', 'site_works_373_88')), b - a, FT(number('spawner.site_works_numbers', 'site_works_373_103'))))
            else:
                p = pad_beside(lw); drive += [(p[number('spawner.site_works_numbers', 'site_works_375_49')] + lw // number('spawner.site_works_numbers', 'site_works_375_60') - fw // number('spawner.site_works_numbers', 'site_works_375_70'), number('spawner.site_works_numbers', 'site_works_375_73'), fw, FT(number('spawner.site_works_numbers', 'site_works_375_83'))), p]
    else:
        if od and det and L.get("alley_rear"): drive.append((od.x1, od.y1, od.x2 - od.x1, Dp - od.y1))   # from the alley
        elif od: drive.append((od.x1, number('spawner.site_works_numbers', 'site_works_378_38'), od.x2 - od.x1, od.y1))
        else: drive.append(pad_beside(L["pad_w"]))
    s["drive"] = drive

def mirror(plan):
    W = plan.lot["width"]
    for r in plan.rooms:
        r.x = W - r.x - r.w
        if r.closet: r.closet = (W - r.closet[number('spawner.invariants_numbers', 'mirror_386_46')] - r.closet[number('spawner.invariants_numbers', 'mirror_386_60')], *r.closet[number('spawner.invariants_numbers', 'mirror_386_74'):])
    for o in plan.doors + plan.windows:
        o.x1, o.x2 = sorted((W - o.x1, W - o.x2))
    for k in ("porch", "deck", "driveway"):
        if plan.site.get(k): x, y, w, h = plan.site[k]; plan.site[k] = (W - x - w, y, w, h)
    plan.site["smoke"] = [(W - x, y) for x, y in plan.site["smoke"]]

def area_band(lot):
    """PLACEHOLDER: stick-built size scales with the lot, inside the approved rule-23 band (1600-2000 sf)."""
    if lot.get("kind") in ("farmstead", "acreage"): return (number('spawner.invariants_numbers', 'area_band_395_60'), number('spawner.invariants_numbers', 'area_band_395_66'))
    if lot["width"] >= FT(number('spawner.invariants_numbers', 'area_band_396_26')): return (number('spawner.invariants_numbers', 'area_band_396_40'), number('spawner.invariants_numbers', 'area_band_396_46'))
    return (number('spawner.invariants_numbers', 'area_band_397_12'), number('spawner.invariants_numbers', 'area_band_397_18'))

# ---------- trailer (single-wide mobile home) ----------
TRAILER_ROOF = DEFAULTS['spawner.trailer_roof']
TRAILER_SIDING = DEFAULTS['spawner.trailer_siding']
TRAILER_W = DEFAULTS['spawner.trailer_w']
TRAILER_L = DEFAULTS['spawner.trailer_l']
TRAILER_NA = DEFAULTS['spawner.trailer_na']

def generate_trailer(lot, seed, force=None):
    d = Draw(H(seed, "trailer"))
    prog = draw_program(Draw(H(seed, "program")), lot, force)
    beds = number('spawner.generate_trailer_numbers', 'generate_trailer_409_11') if d.chance("beds3", number('spawner.generate_trailer_numbers', 'generate_trailer_409_34')) else number('spawner.generate_trailer_numbers', 'generate_trailer_409_43')
    prog.update(type="trailer", style="single-wide", beds=beds, baths=number('spawner.generate_trailer_numbers', 'generate_trailer_410_70'), foundation="piers", garage="none",
                dining=False, porch=False, deck=False, patio=False, roof=d.choice("roof", TRAILER_ROOF), roof_h=d.choice("rh", ["1/12", "2/12", "3/12"]),
                siding=d.choice("sid", TRAILER_SIDING))
    Tw = FT(d.choice("tw", TRAILER_W)); Lk = FT(d.choice("lk", [number('spawner.generate_trailer_numbers', 'generate_trailer_413_64'), number('spawner.generate_trailer_numbers', 'generate_trailer_413_68')])); Lm = FT(d.choice("lm", [number('spawner.generate_trailer_numbers', 'generate_trailer_413_99'), number('spawner.generate_trailer_numbers', 'generate_trailer_413_103')]))
    wing = [("bath1", "bath", FT(number('spawner.generate_trailer_numbers', 'generate_trailer_414_33')))] + [(f"bed{i + number('spawner.generate_trailer_numbers', 'generate_trailer_414_52')}", "bedroom", FT(number('spawner.generate_trailer_numbers', 'generate_trailer_414_71'))) for i in range(beds - number('spawner.generate_trailer_numbers', 'generate_trailer_414_98'))]
    Lwing = sum(h for _, _, h in wing)
    Ll = min(max(FT(d.choice("len", list(range(TRAILER_L[number('spawner.generate_trailer_numbers', 'generate_trailer_416_57')], TRAILER_L[number('spawner.generate_trailer_numbers', 'generate_trailer_416_71')] + number('spawner.generate_trailer_numbers', 'generate_trailer_416_76'), number('spawner.generate_trailer_numbers', 'generate_trailer_416_79'))))) - Lk - Lm - Lwing, FT(number('spawner.generate_trailer_numbers', 'generate_trailer_416_107'))), FT(number('spawner.generate_trailer_numbers', 'generate_trailer_416_116')))
    L = Lk + Ll + Lwing + Lm
    if not FT(TRAILER_L[number('spawner.generate_trailer_numbers', 'generate_trailer_418_24')]) <= L <= FT(TRAILER_L[number('spawner.generate_trailer_numbers', 'generate_trailer_418_49')]): return None
    R = [Room("kitchen", "kitchen", number('spawner.generate_trailer_numbers', 'generate_trailer_419_36'), number('spawner.generate_trailer_numbers', 'generate_trailer_419_39'), Tw, Lk), Room("living", "living", number('spawner.generate_trailer_numbers', 'generate_trailer_419_76'), Lk, Tw, Ll),
         Room("hall", "hall", number('spawner.generate_trailer_numbers', 'generate_trailer_420_30'), Lk + Ll, FT(number('spawner.generate_trailer_numbers', 'generate_trailer_420_45')), Lwing)]
    y = Lk + Ll
    for rid, k, h in wing: R.append(Room(rid, k, FT(number('spawner.generate_trailer_numbers', 'generate_trailer_422_52')), y, Tw - FT(number('spawner.generate_trailer_numbers', 'generate_trailer_422_67')), h)); y += h
    R.append(Room("bed1", "bedroom", number('spawner.generate_trailer_numbers', 'generate_trailer_423_37'), y, Tw, Lm))
    for r in R:
        if r.kind == "bedroom": r.closet = (r.x + (FT(number('spawner.generate_trailer_numbers', 'generate_trailer_425_54')) if r.id == "bed1" else IN(number('spawner.generate_trailer_numbers', 'generate_trailer_425_83'))), r.y if r.id != "bed1" else r.y, r.w - (FT(number('spawner.generate_trailer_numbers', 'generate_trailer_425_131')) if r.id == "bed1" else IN(number('spawner.generate_trailer_numbers', 'generate_trailer_425_160'))), IN(number('spawner.generate_trailer_numbers', 'generate_trailer_425_169')))
    prog = dict(prog, master="bed1", garage_built="none", dining_room=False)
    plan = Plan(lot=copy.deepcopy(lot), program=prog, rooms=R)
    by = {r.id: r for r in R}
    def door(a, b, dw, ro, lim=None):
        sh = shared(by[a], by[b]); axis, c, lo, hi = sh
        if lim: lo, hi = max(lo, lim[number('spawner.generate_trailer_numbers', 'generate_trailer_431_37')]), min(hi, lim[number('spawner.generate_trailer_numbers', 'generate_trailer_431_54')])
        sl = slot(lo, hi, ro, d, f"door:{a}:{b}")
        if sl: plan.doors.append(Door(f"{a}-{b}", a, b, *seg_xy(axis, c, *sl), dw, IN(number('spawner.generate_trailer_numbers', 'generate_trailer_433_86')), ro, "interior"))
    door("kitchen", "living", IN(number('spawner.generate_trailer_numbers', 'generate_trailer_434_33')), IN(number('spawner.generate_trailer_numbers', 'generate_trailer_434_41'))); door("living", "hall", IN(number('spawner.generate_trailer_numbers', 'generate_trailer_434_73')), IN(number('spawner.generate_trailer_numbers', 'generate_trailer_434_81'))); door("hall", "bed1", IN(number('spawner.generate_trailer_numbers', 'generate_trailer_434_111')), IN(number('spawner.generate_trailer_numbers', 'generate_trailer_434_119')))
    for rid, k, h in wing:
        r = by[rid]; door("hall", rid, IN(number('spawner.generate_trailer_numbers', 'generate_trailer_436_42') if k == "bath" else number('spawner.generate_trailer_numbers', 'generate_trailer_436_65')), IN(number('spawner.generate_trailer_numbers', 'generate_trailer_436_73') if k == "bath" else number('spawner.generate_trailer_numbers', 'generate_trailer_436_96')),
                          (r.y + IN(number('spawner.generate_trailer_numbers', 'generate_trailer_437_36')), r.y + r.h) if k == "bedroom" else None)
    def ext(room, side, role=None, win=None):
        segs = [x for x in ext_segments(by[room], R) if x[number('spawner.generate_trailer_numbers', 'generate_trailer_439_58')] == side]
        _, axis, c, lo, hi = max(segs, key=lambda x: x[number('spawner.generate_trailer_numbers', 'generate_trailer_440_55')] - x[number('spawner.generate_trailer_numbers', 'generate_trailer_440_62')])
        if win:
            sl = slot(lo, hi, win[number('spawner.generate_trailer_numbers', 'generate_trailer_442_34')], d, "", centered=True)
            if sl: plan.windows.append(Window(f"w:{room}:{side}", room, *seg_xy(axis, c, *sl), *win))
        else:
            sl = slot(lo, hi, IN(number('spawner.generate_trailer_numbers', 'generate_trailer_445_33')), d, f"ext:{room}")
            if sl: plan.doors.append(Door(f"{room}-{role}", room, "outside", *seg_xy(axis, c, *sl), IN(number('spawner.generate_trailer_numbers', 'generate_trailer_446_103')), IN(number('spawner.generate_trailer_numbers', 'generate_trailer_446_111')), IN(number('spawner.generate_trailer_numbers', 'generate_trailer_446_119')), role))
    ext("living", "right", "front"); ext("kitchen", "left", "rear")
    ext("kitchen", "front", win=(IN(number('spawner.generate_trailer_numbers', 'generate_trailer_448_36')), IN(number('spawner.generate_trailer_numbers', 'generate_trailer_448_44')))); ext("living", "left", win=(IN(number('spawner.generate_trailer_numbers', 'generate_trailer_448_81')), IN(number('spawner.generate_trailer_numbers', 'generate_trailer_448_89'))))
    ext("bed1", "rear", win=(IN(number('spawner.generate_trailer_numbers', 'generate_trailer_449_32')), IN(number('spawner.generate_trailer_numbers', 'generate_trailer_449_40'))))
    for rid, k, _ in wing:
        if k == "bedroom": ext(rid, "right", win=(IN(number('spawner.generate_trailer_numbers', 'generate_trailer_451_53')), IN(number('spawner.generate_trailer_numbers', 'generate_trailer_451_61'))))
        elif not ext(rid, "right", win=(IN(number('spawner.generate_trailer_numbers', 'generate_trailer_452_43')), IN(number('spawner.generate_trailer_numbers', 'generate_trailer_452_51')))): by[rid].fan = True
    hl = by["hall"]
    plan.site = {"porch": None, "deck": None, "deck_kind": None, "smoke": [(hl.x + hl.w // number('spawner.generate_trailer_numbers', 'generate_trailer_454_91'), hl.y + hl.h // number('spawner.generate_trailer_numbers', 'generate_trailer_454_109'))]}
    # place: road end at the front setback, extra room on the pad side (local right)
    W = lot["width"]; mir = prog["mirror"]
    l, r = (lot["side_right"], lot["side_left"]) if mir else (lot["side_left"], lot["side_right"])
    room = W - l - r - Tw - lot["pad_w"] - FT(number('spawner.generate_trailer_numbers', 'generate_trailer_458_46'))
    if room < number('spawner.generate_trailer_numbers', 'generate_trailer_459_14') or lot["front_setback"] + L > lot["depth"] - lot["rear_min"]: return None
    _move(plan, l + room // number('spawner.generate_trailer_numbers', 'generate_trailer_460_28'), lot["front_setback"])
    plan.site["detached_garage"] = False
    if mir: mirror(plan)
    site_works(plan, lot)
    plan.site["drive_surface"] = "gravel"
    return plan

def generate_attempt(lot, seed, force=None):
    if lot.get("house_type") == "trailer": return generate_trailer(lot, seed, force)
    d = Draw(seed)
    prog = draw_program(Draw(H(seed, "program")), lot, force)
    if not force or "area_sf" not in force:
        lo, hi = area_band(lot); prog["area_sf"] = lo + Draw(H(seed, "program")).int("area2", hi - lo + number('spawner.invariants_numbers', 'generate_attempt_472_104'))
    for g in GARAGE_CHAIN[prog["garage"]]:
        plan = build(Draw(H(seed, "layout", g)), lot, prog, g)
        if plan:
            if g != prog["garage"]: plan.notes.append(f"garage re-chosen {prog['garage']} -> {g} to fit the lot")
            return plan
    return None

# ---------- lots / street ----------
# PLACEHOLDER setback model (user: setbacks scale with lot size; values not researched).
SIDE_PCT = DEFAULTS['spawner.side_pct']
SIDE_MIN_PCT = DEFAULTS['spawner.side_min_pct']
FRONT_PCT = DEFAULTS['spawner.front_pct']
FRONT_MIN_PCT = DEFAULTS['spawner.front_min_pct']
REAR_PCT = DEFAULTS['spawner.rear_pct']
REAR_MIN_PCT = DEFAULTS['spawner.rear_min_pct']
CORNER_SIDE_X = DEFAULTS['spawner.corner_side_x']
PORCH_PROJECT_MAX_W = DEFAULTS['spawner.porch_project_max_w']
PORCH_PROJECT = DEFAULTS['spawner.porch_project']

def pct(v, p): return (v * p // number('spawner.invariants_numbers', 'pct_492_32')) // T * T     # snap to whole inches

def make_lot(lot_id, width_ft, neighborhood_seed=number('spawner.make_lot_numbers', 'make_lot_494_49'), depth_ft=number('spawner.make_lot_numbers', 'make_lot_494_64'), corner_side=None, year=None,
             address_street="Main St", side_street=None, road_type="paved", setting="town", overrides=None):
    s = H(neighborhood_seed, lot_id); d = Draw(s)
    W, D = FT(width_ft), FT(depth_ft)
    side, side_min = pct(W, SIDE_PCT), pct(W, SIDE_MIN_PCT)
    lot = {"id": lot_id, "seed": s, "width": W, "depth": D, "road": "front",
           "address_street": address_street, "side_street": side_street, "corner_side": corner_side,
           "road_type": road_type, "setting": setting,
           "drive_surface": "gravel" if setting == "rural" else "concrete",   # PLACEHOLDER
           "year": year or d.choice("year", [number('spawner.make_lot_numbers', 'make_lot_503_45'), number('spawner.make_lot_numbers', 'make_lot_503_51'), number('spawner.make_lot_numbers', 'make_lot_503_57'), number('spawner.make_lot_numbers', 'make_lot_503_63')]),
           "front_setback": pct(D, FRONT_PCT), "front_min": pct(D, FRONT_MIN_PCT),
           "rear_setback": pct(D, REAR_PCT), "rear_min": pct(D, REAR_MIN_PCT),
           "porch_may_project": W <= PORCH_PROJECT_MAX_W, "porch_max_projection": PORCH_PROJECT}
    for k in ("left", "right"):
        x = CORNER_SIDE_X if corner_side == k else number('spawner.make_lot_numbers', 'make_lot_508_51')
        lot[f"side_{k}"], lot[f"side_{k}_min"] = side * x, side_min * x
    # accessory (detached garage) setbacks and drive sizes; CRG values, seeded where CRG gave a range
    lot.update({"kind": "town" if setting == "town" else "acreage", "acc_side": FT(number('spawner.make_lot_numbers', 'make_lot_511_83')), "acc_rear": FT(number('spawner.make_lot_numbers', 'make_lot_511_102') + d.int("accr", number('spawner.make_lot_numbers', 'make_lot_511_120'))),
                "pad_w": FT(number('spawner.make_lot_numbers', 'make_lot_512_28') + d.int("padw", number('spawner.make_lot_numbers', 'make_lot_512_46'))), "pad_len": FT(number('spawner.make_lot_numbers', 'make_lot_512_65') + number('spawner.make_lot_numbers', 'make_lot_512_70') * d.int("padl", number('spawner.make_lot_numbers', 'make_lot_512_88'))), "alley_rear": False,
                "lane_w": FT(number('spawner.make_lot_numbers', 'make_lot_513_29') + d.int("lanew", number('spawner.make_lot_numbers', 'make_lot_513_49'))), "flare_w": FT(number('spawner.make_lot_numbers', 'make_lot_513_68') + number('spawner.make_lot_numbers', 'make_lot_513_73') * d.int("flare", number('spawner.make_lot_numbers', 'make_lot_513_92'))),
                "grove_sides": [], "grove_w": FT(number('spawner.make_lot_numbers', 'make_lot_514_49'))})
    lot.update(overrides or {})
    return lot

def spawn(lot, max_attempts=number('spawner.spawn_numbers', 'spawn_518_28'), force=None, extra=None):
    """Retry loop: attempt seed = hash(lotSeed, attempt). Returns (plan, report, attempts, fail_counts).
    extra(plan) -> {rule: (status, detail)} adds neighborhood-level checks (rule 35)."""
    from validate import validate, failing
    fails = defaultdict(int)
    for a in range(max_attempts):
        plan = generate_attempt(lot, H(lot["seed"], "attempt", a), force)
        if plan is None:
            fails["2 (lot fit)"] += number('spawner.spawn_numbers', 'spawn_526_36'); continue
        rep = validate(plan)
        if extra: rep.update(extra(plan))
        bad = failing(rep)
        if not bad:
            plan.program["attempt"] = a
            return plan, rep, a + number('spawner.spawn_numbers', 'spawn_532_34'), dict(fails)
        for r in bad: fails[r] += number('spawner.spawn_numbers', 'spawn_533_34')
    return None, None, max_attempts, dict(fails)

from settings import configurable as _configurable
make_lot = _configurable(make_lot)
spawn = _configurable(spawn)
