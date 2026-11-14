"""Builder Guy building spawner, 2D prototype (research only).

Lot spec + seed -> one-story floor plan on a module grid -> validator -> retry.
All geometry is integer 1/16-in ticks. y grows away from the road (road is at y < 0).
Method: fixed adjacency template (public half + bedroom wing), seeded splits on a
2 ft grid, footprint snapped to 4 ft, child seed = hash(parent, stable key).
"""
import copy, hashlib, json
from collections import defaultdict
from dataclasses import dataclass, field, asdict

T = 16                       # ticks per inch
def IN(x): return int(round(x * T))
def FT(x): return int(round(x * 12 * T))
SF = FT(1) ** 2              # ticks^2 per square foot
CLR = IN(4.5)                # rule 11 corner clearance each side of a door RO
HEAD = IN(83)                # rule 28 common head height (AH73 6'-10"..6'-11")
GRID, MODULE = FT(2), FT(4)  # rule 20

def H(*parts):
    """Stable 64-bit hash of a key path (rule 34)."""
    s = "|".join(str(p) for p in parts).encode()
    return int.from_bytes(hashlib.blake2b(s, digest_size=8).digest(), "big")

class Draw:
    """Stateless seeded choices: each choice is hash(seed, key), so adding a choice never shifts another."""
    def __init__(self, seed): self.seed = seed
    def int(self, key, n): return H(self.seed, key) % n
    def choice(self, key, seq): return seq[self.int(key, len(seq))]
    def chance(self, key, pct): return self.int(key, 100) < pct
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
    ceiling: int = IN(96)
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
    def digest(self): return hashlib.sha256(self.to_json().encode()).hexdigest()[:16]

HABITABLE = {"living", "kitchen", "dining", "family", "bedroom"}
PUBLIC = {"living", "kitchen", "dining", "family"}

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
        cuts = [(s[2], s[3]) for o in rooms if o is not room
                for s in [shared(room, o)] if s and s[0] == axis and s[1] == c]
        out += [(side, axis, c, a, b) for a, b in _subtract(lo, hi, cuts)]
    return out

def seg_xy(axis, c, a, b): return (a, c, b, c) if axis == "h" else (c, a, c, b)

def slot(lo, hi, ro, d, key, centered=False):
    """Place an opening of width ro inside [lo, hi] keeping CLR each side. Returns (a, b) or None."""
    clo, chi = lo + CLR + ro // 2, hi - CLR - ro // 2
    if chi < clo: return None
    if centered: c = (clo + chi) // 2
    else:
        step = IN(2); c = clo + d.int(key, (chi - clo) // step + 1) * step
    return (c - ro // 2, c - ro // 2 + ro)

# ---------- program draws (rules 21-24, 4, 30) ----------
def by_year(table, y): return table[min(table, key=lambda k: (abs(k - y), k))]
STYLE = {1985: (47, 39, 13), 1990: (44, 45, 11), 1995: (44, 50, 6), 2000: (45, 50, 5)}
BEDS = {1985: (24, 55, 20), 1990: (15, 57, 27), 1995: (17, 54, 28), 2000: (17, 53, 31)}
BATHS = {1990: (18, 82), 1995: (18, 82), 2000: (11, 89)}          # 1-1/2 or less vs 2+
FOUND = {1985: (77, 11, 13), 1990: (81, 8, 11), 1995: (79, 10, 11), 2000: (79, 13, 8)}
GARAGE = {1985: (13, 13, 73), 1990: (9, 8, 83), 1995: (9, 6, 84), 2000: (7, 4, 88)}
OUTDOOR = {1995: (42, 38, 27), 2000: (47, 36, 31)}                # porch, deck, patio (series starts 1992)

def draw_program(d, lot, force=None):
    y = lot["year"]
    p = {"year": y,
         "style": d.weighted("style", list(zip(["ranch", "two_story", "split_level"], by_year(STYLE, y)))),
         "beds": d.weighted("beds", list(zip(["le2", 3, 4], by_year(BEDS, y)))),
         "baths": d.weighted("baths", list(zip([1, 2], by_year(BATHS, y)))),
         "area_sf": 1600 + d.int("area", 401),
         "foundation": d.weighted("found", list(zip(["basement", "slab", "crawl"], by_year(FOUND, y)))),
         "garage": d.weighted("garage", list(zip(["none", "single", "double"], by_year(GARAGE, y)))),
         "dining": d.chance("dining", 85), "mudroom": d.chance("mud", 50)}
    if p["beds"] == "le2": p["beds"] = 1 if d.chance("onebed", 20) else 2   # 1-bed share NOT VERIFIED
    po, de, pa = by_year(OUTDOOR, y)
    p["porch"], p["deck"] = d.chance("porch", po), d.chance("deck", de)
    p["patio"] = (not p["deck"]) and d.chance("patio", pa)
    p["mirror"] = d.chance("mirror", 50)
    # exterior draws used only by rule 35 (look-alikes). PLACEHOLDER choice lists.
    p["roof"] = d.choice("roof", ["gable", "hip"])
    p["roof_h"] = d.choice("roof_h", ["4/12", "5/12", "6/12"])
    p["siding"] = d.choice("siding", ["vinyl lap", "wood lap", "brick front"])
    p.update(force or {})
    return p

GARAGE_CHAIN = {"double": ["double", "single", "detached"], "single": ["single", "detached"], "none": ["none"], "detached": ["detached"]}
GW = {"double": FT(22), "single": FT(12), "none": 0, "detached": 0}

# ---------- layout ----------
def build(d, lot, prog, gtype):
    """One layout for a given garage type, or None if it doesn't fit the lot."""
    # PLACEHOLDER size scaling: lots with < 70 ft buildable width get the smaller room sizes
    narrow = lot["width"] - lot["side_left"] - lot["side_right"] < FT(70)
    KW, LW, BW = ([FT(10), FT(12)], [FT(14), FT(16)], [FT(10), FT(12)]) if narrow else \
                 ([FT(10), FT(12), FT(14)], [FT(14), FT(16), FT(18)], [FT(10), FT(12), FT(14)])
    Kw = d.choice("kw", KW); Lw = d.choice("lw", LW)
    nb = prog["beds"]; nf = (nb + 1) // 2; nr = nb - nf
    front = [d.choice(f"bw{i}", BW) for i in range(nf)]
    rear_beds = [d.choice(f"bw{nf+i}", BW) for i in range(nr)]
    baths = [("bath1", d.choice("b1w", [FT(6), FT(8)]))] + ([("bath2", FT(6))] if prog["baths"] >= 2 else [])
    Wp = Kw + Lw
    Ww = max(sum(front), sum(w for _, w in baths) + sum(rear_beds))
    Ww += (-(Wp + Ww)) % MODULE
    front[-1] += Ww - sum(front)
    rest = Ww - sum(w for _, w in baths) - sum(rear_beds)
    if rear_beds: rear_beds[-1] += rest
    else: baths[-1] = (baths[-1][0], baths[-1][1] + rest)
    Wh = Wp + Ww
    target = prog["area_sf"] * SF
    Df, Dr = min(((a, b) for a in (FT(16), FT(20)) for b in (FT(12), FT(16))),
                 key=lambda ab: (abs(Wh * (ab[0] + ab[1]) - target), ab))
    D = Df + Dr
    attached = gtype in ("single", "double")
    P0 = GW[gtype] if attached else 0
    need_util = prog["foundation"] != "basement"
    U = "utility" if need_util else ("mudroom" if prog["mudroom"] and attached else None)
    Uw = d.choice("uw", [FT(6), FT(8)]) if U else 0

    R = []
    if attached: R.append(Room("garage", "garage", 0, 0, GW[gtype], FT(22)))
    R += [Room("kitchen", "kitchen", P0, 0, Kw, Df), Room("living", "living", P0 + Kw, 0, Lw, Df)]
    if U: R.append(Room(U, U, P0, Df, Uw, Dr))
    pub = "dining" if prog["dining"] else "family"
    R.append(Room(pub, pub, P0 + Uw, Df, Wp - Uw, Dr))
    x0 = P0 + Wp
    R.append(Room("hall", "hall", x0, Df - FT(4), Ww, FT(4)))
    beds, x, n = [], x0, 0
    for w in front:
        n += 1; beds.append(Room(f"bed{n}", "bedroom", x, 0, w, Df - FT(4))); x += w
    x = x0
    for bid, w in baths:
        R.append(Room(bid, "bath", x, Df, w, Dr)); x += w
    for w in rear_beds:
        n += 1; beds.append(Room(f"bed{n}", "bedroom", x, Df, w, Dr)); x += w
    for b in beds:   # closet: 2 ft deep on the hall wall, leaving >= 4 ft for the door
        cw = min(b.w - IN(48), IN(96)); left = d.chance(f"closet:{b.id}", 50)
        cy = b.y + b.h - IN(24) if b.y == 0 else b.y
        b.closet = (b.x if left else b.x + b.w - cw, cy, cw, IN(24))
    R += beds
    master = max(beds, key=lambda b: (b.area, [-ord(c) for c in b.id])).id
    prog = dict(prog, master=master, garage_built=gtype, dining_room=pub == "dining")
    plan = Plan(lot=copy.deepcopy(lot), program=prog, rooms=R)
    by = {r.id: r for r in R}

    def connect(a, b, role, dw, ro, lim=None):
        s = shared(by[a], by[b])
        if not s: return None
        axis, c, lo, hi = s
        if lim: lo, hi = max(lo, lim[0]), min(hi, lim[1])
        sl = slot(lo, hi, ro, d, f"door:{a}:{b}")
        if not sl: return None
        dr = Door(f"{a}-{b}", a, b, *seg_xy(axis, c, *sl), dw, IN(80), ro, role)
        plan.doors.append(dr); return dr

    def ext(room, side, role=None, dw=IN(36), ro=IN(38.5), win=None, half=None):
        segs = [s for s in ext_segments(by[room], R) if s[0] == side]
        if not segs: return None
        _, axis, c, lo, hi = max(segs, key=lambda s: s[4] - s[3])
        if half == 0: hi = (lo + hi) // 2
        if half == 1: lo = (lo + hi) // 2
        if win:
            sl = slot(lo, hi, win[0], d, "", centered=True)
            if sl: plan.windows.append(Window(f"w:{room}:{side}", room, *seg_xy(axis, c, *sl), win[0], win[1]))
            return sl
        sl = slot(lo, hi, ro, d, f"ext:{room}:{role}")
        if sl: plan.doors.append(Door(f"{room}-{role}", room, "outside", *seg_xy(axis, c, *sl), dw, IN(80), ro, role))
        return sl

    # interior doors (adjacency template)
    connect("kitchen", "living", "interior", IN(30), IN(32))
    connect("living", pub, "interior", IN(30), IN(32))
    connect("kitchen", pub, "interior", IN(30), IN(32))
    if U: connect("kitchen", U, "interior", IN(30), IN(32))
    connect("living", "hall", "interior", IN(30), IN(32))
    for b in beds:
        cx, _, cw, _ = b.closet
        lim = (cx + cw, b.x + b.w) if cx == b.x else (b.x, cx)
        connect("hall", b.id, "interior", IN(30), IN(32), lim)
    connect("hall", "bath1", "interior", IN(28), IN(30))
    if "bath2" in by:
        nxt = next((b for b in beds if b.y == Df and b.x == by["bath2"].x + by["bath2"].w), None)
        if nxt and nxt.id == master: connect(master, "bath2", "interior", IN(28), IN(30))
        else: connect("hall", "bath2", "interior", IN(28), IN(30))
    if attached and not (U and connect("garage", U, "garage_house", IN(36), IN(38.5))):
        connect("garage", "kitchen", "garage_house", IN(36), IN(38.5))
    # exterior doors
    fd = ext("living", "front", "front", half=0)
    rear_room = pub if (prog["deck"] or prog["patio"]) else (U or pub)
    rd = ext(rear_room, "rear", "rear", half=0)
    if attached:
        g = by["garage"]; ow = FT(16) if gtype == "double" else FT(9)
        a = g.x + (g.w - ow) // 2
        plan.doors.append(Door("garage-overhead", "garage", "outside", a, 0, a + ow, 0, ow, IN(84), ow, "overhead", "overhead"))
    # windows (RO w x h); same head everywhere (rule 28)
    ext("kitchen", "front", win=(IN(48), IN(42)))
    ext("living", "front", win=(IN(72), IN(60)), half=1)
    ext(pub, "rear", win=(IN(60), IN(60)), half=1 if rear_room == pub else None)
    for b in beds: ext(b.id, "front" if b.y == 0 else "rear", win=(IN(36), IN(54)))
    for bid, _ in baths:
        if not ext(bid, "rear", win=(IN(24), IN(36))): by[bid].fan = True
    if U: by[U].fan = True
    # site: porch, deck/patio, smoke detector spots (local coords)
    site = {"porch": None, "deck": None, "deck_kind": None, "driveway": None}
    if prog["porch"] and fd:
        lv = by["living"]; cx = (fd[0] + fd[1]) // 2
        px = min(max(cx - FT(4), lv.x), lv.x + lv.w - FT(8))
        site["porch"] = (px, -FT(6), FT(8), FT(6))
    if (prog["deck"] or prog["patio"]) and rd:
        cx = (rd[0] + rd[1]) // 2
        site["deck"] = (cx - FT(6), D, FT(12), FT(8)); site["deck_kind"] = "deck" if prog["deck"] else "patio"
    hl = by["hall"]
    site["smoke"] = [(hl.x + hl.w // 2, hl.y + hl.h // 2)]
    plan.site = site
    return place_on_lot(plan, lot, gtype, Wh, D)

def _move(plan, dx, dy):
    for r in plan.rooms:
        r.x += dx; r.y += dy
        if r.closet: r.closet = (r.closet[0] + dx, r.closet[1] + dy, *r.closet[2:])
    for o in plan.doors + plan.windows:
        o.x1 += dx; o.x2 += dx; o.y1 += dy; o.y2 += dy
    for k in ("porch", "deck"):
        if plan.site[k]: x, y, w, h = plan.site[k]; plan.site[k] = (x + dx, y + dy, w, h)
    plan.site["smoke"] = [(x + dx, y + dy) for x, y in plan.site["smoke"]]

def place_on_lot(plan, lot, gtype, Wh, D):
    """Fit the house between the lot's setbacks; sides may shrink to the minimums (rule 2 warn)."""
    W = lot["width"]; mir = plan.program["mirror"]
    req = (lot["side_left"], lot["side_right"]); mins = (lot["side_left_min"], lot["side_right_min"])
    if mir: req, mins = req[::-1], mins[::-1]          # placement happens before the mirror flip
    porch_push = FT(6) if plan.site["porch"] and not lot["porch_may_project"] else 0
    hy = lot["front_setback"] + porch_push
    if hy + D > lot["depth"] - lot["rear_min"]: return None
    acc_l = lot["acc_side"]                          # detached garage sits acc_side from the (pre-mirror) left line
    alley = lot.get("alley_rear", False)             # town lot with an alley behind: garage takes access from the alley
    hx = None
    for l, r in (req, mins):
        if gtype == "detached" and alley:
            if Wh <= W - l - r: hx = l + (W - l - r - Wh) // 2; break
        elif gtype == "detached":
            x = max(acc_l + FT(15), l)               # garage 12 ft + 3 ft to the house; drive runs beside it
            if x + Wh <= W - r: hx = x; break
        else:
            Wt = GW[gtype] + Wh
            if Wt <= W - l - r: hx = l + (W - l - r - Wt) // 2; break
    if hx is None: return None
    _move(plan, hx, hy)
    if gtype == "detached":
        gy = lot["depth"] - lot["acc_rear"] - FT(22)     # back by the alley / rear line
        if gy < hy + D + FT(8): return None
        g = Room("garage", "garage", acc_l, gy, FT(12), FT(22)); plan.rooms.append(g)
        a = g.x + IN(18); dy = gy + FT(22) if alley else gy     # alley garages open to the rear
        plan.doors.append(Door("garage-overhead", "garage", "outside", a, dy, a + FT(9), dy, FT(9), IN(84), FT(9), "overhead", "overhead"))
    plan.site["detached_garage"] = gtype == "detached"
    if plan.program["mirror"]: mirror(plan)
    site_works(plan, lot)
    return plan

def _hits(a, b): return a[0] < b[0] + b[2] and b[0] < a[0] + a[2] and a[1] < b[1] + b[3] and b[1] < a[1] + a[3]

def site_works(plan, L):
    """Drive for every house: ends at the garage door (alley garages from the alley), else at a parking pad
    beside the house. Farmsteads: lane + farmyard apron touching every vehicle building; grove on N/W edges."""
    s = plan.site; W, Dp = L["width"], L["depth"]; kind = L["kind"]
    det = s.get("detached_garage")
    att = [r for r in plan.rooms if not (r.kind == "garage" and det)]
    x0 = min(r.x for r in att); x1 = max(r.x + r.w for r in att); y0 = min(r.y for r in att); y1 = max(r.y + r.h for r in att)
    od = next((d for d in plan.doors if d.role == "overhead"), None)
    gw = L["grove_w"]; gs = L["grove_sides"]
    s["grove"] = [{"left": (0, 0, gw, Dp), "right": (W - gw, 0, gw, Dp), "rear": (0, Dp - gw, W, gw)}[k] for k in gs]
    s["farm"] = []
    drive = []
    pl = L["pad_len"]
    def pad_beside(pw):
        """Parking pad touching the house side with the most room, joined to the road by a strip of the same width."""
        if W - x1 >= x0 and W - x1 >= pw + FT(1): px = x1
        elif x0 >= pw + FT(1): px = x0 - pw
        else: px = x0                                       # no room: pad lands in front (rule 3 rejects it)
        return (px, 0, pw, y0 + pl)
    if kind in ("farmstead", "acreage"):
        lw, fw = L["lane_w"], L["flare_w"]
        if kind == "farmstead":
            yx0 = (gw + FT(20)) if "left" in gs else FT(20); yx1 = W - ((gw + FT(20)) if "right" in gs else FT(20))
            yy0 = y1 + FT(40); yy1 = Dp - ((gw + FT(20)) if "rear" in gs else FT(20))
            lx = x1 + FT(20)
            if lx + lw > yx1: lx = x0 - FT(20) - lw
            cur = yx0
            for name, w, h in (("barn", FT(40), FT(60)), ("machine shed", FT(50), FT(80)), ("bin", FT(30), FT(30)), ("bin", FT(30), FT(30))):
                if cur < lx + lw + FT(20) and cur + w > lx - FT(20): cur = lx + lw + FT(20)
                r = (cur, yy0 + FT(40), w, h)
                if r[0] + w <= yx1 and r[1] + h <= yy1: s["farm"].append((name, r)); cur += w + FT(30)
            s["yard"] = (yx0, yy0, yx1 - yx0, yy1 - yy0)
            drive.append((lx + lw // 2 - fw // 2, 0, fw, FT(20)))                     # flare at the road
            drive.append((lx, 0, lw, yy0 + FT(20)))                                   # lane to the farmyard
            bx = [r for _, r in s["farm"]]
            a = min([lx] + [r[0] for r in bx]); b = max([lx + lw] + [r[0] + r[2] for r in bx])
            drive.append((a, yy0, b - a, FT(40)))                                     # farmyard apron: every building fronts it
            if od:
                a, b = min(od.x1, lx), max(od.x2, lx + lw); drive.append((a, od.y1 - FT(30), b - a, FT(30)))
            else:
                drive.append((x1, y0, lx - x1, pl) if lx > x1 else (lx + lw, y0, x0 - lx - lw, pl))   # pad between house and lane
        else:
            if od:
                lx = (od.x1 + od.x2) // 2 - lw // 2
                drive += [(lx + lw // 2 - fw // 2, 0, fw, FT(20)), (lx, 0, lw, od.y1)]
                a, b = min(od.x1, lx), max(od.x2, lx + lw); drive.append((a, od.y1 - FT(30), b - a, FT(30)))
            else:
                p = pad_beside(lw); drive += [(p[0] + lw // 2 - fw // 2, 0, fw, FT(20)), p]
    else:
        if od and det and L.get("alley_rear"): drive.append((od.x1, od.y1, od.x2 - od.x1, Dp - od.y1))   # from the alley
        elif od: drive.append((od.x1, 0, od.x2 - od.x1, od.y1))
        else: drive.append(pad_beside(L["pad_w"]))
    s["drive"] = drive

def mirror(plan):
    W = plan.lot["width"]
    for r in plan.rooms:
        r.x = W - r.x - r.w
        if r.closet: r.closet = (W - r.closet[0] - r.closet[2], *r.closet[1:])
    for o in plan.doors + plan.windows:
        o.x1, o.x2 = sorted((W - o.x1, W - o.x2))
    for k in ("porch", "deck", "driveway"):
        if plan.site.get(k): x, y, w, h = plan.site[k]; plan.site[k] = (W - x - w, y, w, h)
    plan.site["smoke"] = [(W - x, y) for x, y in plan.site["smoke"]]

def area_band(lot):
    """PLACEHOLDER: stick-built size scales with the lot, inside the approved rule-23 band (1600-2000 sf)."""
    if lot.get("kind") in ("farmstead", "acreage"): return (1800, 2000)
    if lot["width"] >= FT(150): return (1700, 1900)
    return (1600, 1750)

# ---------- trailer (single-wide mobile home) ----------
TRAILER_ROOF = ["low-slope metal", "low-slope membrane", "shingled gable"]   # PLACEHOLDER
TRAILER_SIDING = ["metal lap", "vinyl lap", "T1-11 plywood", "aluminum skirt+lap"]   # PLACEHOLDER
TRAILER_W = [14, 16]          # PLACEHOLDER ft (single-wide)
TRAILER_L = (60, 76)          # PLACEHOLDER ft overall length
TRAILER_NA = {"1": "door on the long side facing the pad, not the road end",
              "4": "no garage expected", "5": "no garage expected", "8": "no dining room or garage",
              "20": "factory-built; 14/16 ft widths are off the 4 ft module", "23": "single-wide area is about 840-1,200 sf",
              "24": "set on piers/skirting, no foundation draw", "25": "furnace/water heater closet, no utility room",
              "30": "no porch/deck drawn", "21": "style = single-wide"}

def generate_trailer(lot, seed, force=None):
    d = Draw(H(seed, "trailer"))
    prog = draw_program(Draw(H(seed, "program")), lot, force)
    beds = 3 if d.chance("beds3", 35) else 2
    prog.update(type="trailer", style="single-wide", beds=beds, baths=1, foundation="piers", garage="none",
                dining=False, porch=False, deck=False, patio=False, roof=d.choice("roof", TRAILER_ROOF), roof_h=d.choice("rh", ["1/12", "2/12", "3/12"]),
                siding=d.choice("sid", TRAILER_SIDING))
    Tw = FT(d.choice("tw", TRAILER_W)); Lk = FT(d.choice("lk", [12, 14])); Lm = FT(d.choice("lm", [12, 14]))
    wing = [("bath1", "bath", FT(8))] + [(f"bed{i + 2}", "bedroom", FT(10)) for i in range(beds - 1)]
    Lwing = sum(h for _, _, h in wing)
    Ll = min(max(FT(d.choice("len", list(range(TRAILER_L[0], TRAILER_L[1] + 1, 2)))) - Lk - Lm - Lwing, FT(14)), FT(22))
    L = Lk + Ll + Lwing + Lm
    if not FT(TRAILER_L[0]) <= L <= FT(TRAILER_L[1]): return None
    R = [Room("kitchen", "kitchen", 0, 0, Tw, Lk), Room("living", "living", 0, Lk, Tw, Ll),
         Room("hall", "hall", 0, Lk + Ll, FT(4), Lwing)]
    y = Lk + Ll
    for rid, k, h in wing: R.append(Room(rid, k, FT(4), y, Tw - FT(4), h)); y += h
    R.append(Room("bed1", "bedroom", 0, y, Tw, Lm))
    for r in R:
        if r.kind == "bedroom": r.closet = (r.x + (FT(4) if r.id == "bed1" else IN(12)), r.y if r.id != "bed1" else r.y, r.w - (FT(4) if r.id == "bed1" else IN(24)), IN(24))
    prog = dict(prog, master="bed1", garage_built="none", dining_room=False)
    plan = Plan(lot=copy.deepcopy(lot), program=prog, rooms=R)
    by = {r.id: r for r in R}
    def door(a, b, dw, ro, lim=None):
        sh = shared(by[a], by[b]); axis, c, lo, hi = sh
        if lim: lo, hi = max(lo, lim[0]), min(hi, lim[1])
        sl = slot(lo, hi, ro, d, f"door:{a}:{b}")
        if sl: plan.doors.append(Door(f"{a}-{b}", a, b, *seg_xy(axis, c, *sl), dw, IN(80), ro, "interior"))
    door("kitchen", "living", IN(30), IN(32)); door("living", "hall", IN(30), IN(32)); door("hall", "bed1", IN(30), IN(32))
    for rid, k, h in wing:
        r = by[rid]; door("hall", rid, IN(28 if k == "bath" else 30), IN(30 if k == "bath" else 32),
                          (r.y + IN(24), r.y + r.h) if k == "bedroom" else None)
    def ext(room, side, role=None, win=None):
        segs = [x for x in ext_segments(by[room], R) if x[0] == side]
        _, axis, c, lo, hi = max(segs, key=lambda x: x[4] - x[3])
        if win:
            sl = slot(lo, hi, win[0], d, "", centered=True)
            if sl: plan.windows.append(Window(f"w:{room}:{side}", room, *seg_xy(axis, c, *sl), *win))
        else:
            sl = slot(lo, hi, IN(38.5), d, f"ext:{room}")
            if sl: plan.doors.append(Door(f"{room}-{role}", room, "outside", *seg_xy(axis, c, *sl), IN(36), IN(80), IN(38.5), role))
    ext("living", "right", "front"); ext("kitchen", "left", "rear")
    ext("kitchen", "front", win=(IN(36), IN(36))); ext("living", "left", win=(IN(60), IN(48)))
    ext("bed1", "rear", win=(IN(36), IN(54)))
    for rid, k, _ in wing:
        if k == "bedroom": ext(rid, "right", win=(IN(36), IN(54)))
        elif not ext(rid, "right", win=(IN(24), IN(36))): by[rid].fan = True
    hl = by["hall"]
    plan.site = {"porch": None, "deck": None, "deck_kind": None, "smoke": [(hl.x + hl.w // 2, hl.y + hl.h // 2)]}
    # place: road end at the front setback, extra room on the pad side (local right)
    W = lot["width"]; mir = prog["mirror"]
    l, r = (lot["side_right"], lot["side_left"]) if mir else (lot["side_left"], lot["side_right"])
    room = W - l - r - Tw - lot["pad_w"] - FT(1)
    if room < 0 or lot["front_setback"] + L > lot["depth"] - lot["rear_min"]: return None
    _move(plan, l + room // 2, lot["front_setback"])
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
        lo, hi = area_band(lot); prog["area_sf"] = lo + Draw(H(seed, "program")).int("area2", hi - lo + 1)
    for g in GARAGE_CHAIN[prog["garage"]]:
        plan = build(Draw(H(seed, "layout", g)), lot, prog, g)
        if plan:
            if g != prog["garage"]: plan.notes.append(f"garage re-chosen {prog['garage']} -> {g} to fit the lot")
            return plan
    return None

# ---------- lots / street ----------
# PLACEHOLDER setback model (user: setbacks scale with lot size; values not researched).
SIDE_PCT, SIDE_MIN_PCT = 10, 5          # % of lot width, each side
FRONT_PCT, FRONT_MIN_PCT = 20, 15       # % of lot depth (125 ft lot -> 25 ft front)
REAR_PCT, REAR_MIN_PCT = 25, 20         # % of lot depth
CORNER_SIDE_X = 2                       # street-side yard on a corner lot = 2x the side setback
PORCH_PROJECT_MAX_W = FT(70)            # lots this wide or narrower may project a porch...
PORCH_PROJECT = FT(6)                   # ...up to 6 ft into the front setback

def pct(v, p): return (v * p // 100) // T * T     # snap to whole inches

def make_lot(lot_id, width_ft, neighborhood_seed=1985, depth_ft=130, corner_side=None, year=None,
             address_street="Main St", side_street=None, road_type="paved", setting="town", overrides=None):
    s = H(neighborhood_seed, lot_id); d = Draw(s)
    W, D = FT(width_ft), FT(depth_ft)
    side, side_min = pct(W, SIDE_PCT), pct(W, SIDE_MIN_PCT)
    lot = {"id": lot_id, "seed": s, "width": W, "depth": D, "road": "front",
           "address_street": address_street, "side_street": side_street, "corner_side": corner_side,
           "road_type": road_type, "setting": setting,
           "drive_surface": "gravel" if setting == "rural" else "concrete",   # PLACEHOLDER
           "year": year or d.choice("year", [1985, 1990, 1995, 2000]),
           "front_setback": pct(D, FRONT_PCT), "front_min": pct(D, FRONT_MIN_PCT),
           "rear_setback": pct(D, REAR_PCT), "rear_min": pct(D, REAR_MIN_PCT),
           "porch_may_project": W <= PORCH_PROJECT_MAX_W, "porch_max_projection": PORCH_PROJECT}
    for k in ("left", "right"):
        x = CORNER_SIDE_X if corner_side == k else 1
        lot[f"side_{k}"], lot[f"side_{k}_min"] = side * x, side_min * x
    # accessory (detached garage) setbacks and drive sizes; CRG values, seeded where CRG gave a range
    lot.update({"kind": "town" if setting == "town" else "acreage", "acc_side": FT(5), "acc_rear": FT(5 + d.int("accr", 6)),
                "pad_w": FT(9 + d.int("padw", 2)), "pad_len": FT(20 + 5 * d.int("padl", 5)), "alley_rear": False,
                "lane_w": FT(12 + d.int("lanew", 5)), "flare_w": FT(24 + 2 * d.int("flare", 9)),
                "grove_sides": [], "grove_w": FT(80)})
    lot.update(overrides or {})
    return lot

def spawn(lot, max_attempts=25, force=None, extra=None):
    """Retry loop: attempt seed = hash(lotSeed, attempt). Returns (plan, report, attempts, fail_counts).
    extra(plan) -> {rule: (status, detail)} adds neighborhood-level checks (rule 35)."""
    from validate import validate, failing
    fails = defaultdict(int)
    for a in range(max_attempts):
        plan = generate_attempt(lot, H(lot["seed"], "attempt", a), force)
        if plan is None:
            fails["2 (lot fit)"] += 1; continue
        rep = validate(plan)
        if extra: rep.update(extra(plan))
        bad = failing(rep)
        if not bad:
            plan.program["attempt"] = a
            return plan, rep, a + 1, dict(fails)
        for r in bad: fails[r] += 1
    return None, None, max_attempts, dict(fails)
