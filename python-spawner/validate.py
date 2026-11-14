"""Per-rule validator. Each rule -> (status, detail); status in pass / fail / warn / not-2D."""
from settings import DEFAULTS
from collections import defaultdict
from spawner import (shared, ext_segments, CLR, HEAD, IN, FT, SF, GRID, MODULE, HABITABLE, PUBLIC)

APPROVED = DEFAULTS['validate.approved']
WARN_ONLY = DEFAULTS['validate.warn_only']
ALL = DEFAULTS['validate.all']

def _ov(a, b): return a[0] < b[0] + b[2] and b[0] < a[0] + a[2] and a[1] < b[1] + b[3] and b[1] < a[1] + a[3]

def failing(rep): return [r for r, (s, _) in rep.items() if s == "fail"]

def graph(plan):
    g = defaultdict(set)
    for d in plan.doors: g[d.a].add(d.b); g[d.b].add(d.a)
    return g

def bfs(g, start, blocked=()):
    dist, q = {start: 0}, [start]
    for n in q:
        for m in sorted(g[n]):
            if m not in dist:
                dist[m] = dist[n] + 1
                if m not in blocked: q.append(m)
    return dist

def ft(t): return f"{t / FT(1):g}'"

def validate(plan):
    P, L = plan.program, plan.lot
    rooms = plan.rooms
    by = {r.id: r for r in rooms}
    house = [r for r in rooms if r.kind != "garage"]
    garage = by.get("garage")
    attached = garage is not None and not plan.site.get("detached_garage")
    attached_rooms = house + ([garage] if attached else [])
    beds = [r for r in house if r.kind == "bedroom"]
    baths = [r for r in house if r.kind == "bath"]
    g = graph(plan)
    front = [d for d in plan.doors if d.role == "front"]
    front_y = min(r.y for r in attached_rooms) if attached_rooms else 0
    def kids(rid): return {d.b if d.a == rid else d.a for d in plan.doors if rid in (d.a, d.b)}
    def on_road(o): return o.y1 == o.y2 == front_y
    rep = {}
    def put(rule, ok, detail="", soft=False):
        sev = "warn" if (rule in WARN_ONLY or soft) else "fail"
        rep[rule] = ("pass" if ok else sev, detail)

    # U1-U3
    put("U1", L.get("road") == "front" and (any(on_road(d) for d in front) or (P.get("type") == "trailer" and any(on_road(w) for w in plan.windows))),
        "road side + road-facing front door (trailer: road-facing end wall)")
    put("U2", "kitchen" in by and baths and P.get("master") in by, f"kitchen, {len(baths)} bath(s), master={P.get('master')}")
    kw = [w for w in plan.windows if w.room == "kitchen" and on_road(w)]
    put("U3", bool(kw), f"{len(kw)} kitchen window(s) on the road wall")
    # 1 front door
    put("1", len(front) >= 1 and all(on_road(d) and by.get(d.a) and by[d.a].kind in ("living", "entry", "kitchen") for d in front),
        "front door on road wall into entry/living/kitchen")
    # 2 setbacks (scaled PLACEHOLDER values from the lot; below required but >= minimum -> warn)
    bx0 = min(r.x for r in attached_rooms); bx1 = max(r.x + r.w for r in attached_rooms)
    by0 = min(r.y for r in attached_rooms); by1 = max(r.y + r.h for r in attached_rooms)
    hard, soft = [], []
    for name, val, req, mn in (("front", by0, L["front_setback"], L["front_min"]),
                               ("rear", L["depth"] - by1, L["rear_setback"], L["rear_min"]),
                               ("left", bx0, L["side_left"], L["side_left_min"]),
                               ("right", L["width"] - bx1, L["side_right"], L["side_right_min"])):
        if val < req: (soft if val >= mn else hard).append(f"{name} {ft(val)}<{ft(req)} (min {ft(mn)})")
    po = plan.site.get("porch")
    if po:
        lim = L["front_setback"] - (L["porch_max_projection"] if L["porch_may_project"] else 0)
        if po[1] < lim: hard.append(f"porch at {ft(po[1])} < {ft(lim)}")
    if garage is not None and plan.site.get("detached_garage"):   # accessory setbacks (CRG): sides and rear/alley
        gap = (garage.x, L["width"] - garage.x - garage.w, L["depth"] - garage.y - garage.h)
        if min(gap[:2]) < L.get("acc_side", 0) or gap[2] < L.get("acc_rear", 0):
            hard.append(f"detached garage {ft(gap[0])}/{ft(gap[1])} side, {ft(gap[2])} rear < accessory {ft(L.get('acc_side', 0))}/{ft(L.get('acc_rear', 0))}")
    det = "; ".join(hard + soft) or f"front {ft(by0)}, rear {ft(L['depth'] - by1)}, sides {ft(bx0)}/{ft(L['width'] - bx1)}"
    if hard: put("2", False, det)
    else: put("2", not soft, det, soft=True)
    # 3 driveway, every house: ends at the garage door (alley garages may start at the alley), else at a pad beside
    # the house; on farmsteads it also reaches every vehicle building (machine shed, barn, bins)
    od = next((d for d in plan.doors if d.role == "overhead"), None)
    dr = list(plan.site.get("drive") or [])
    touch = lambda r, c: r[0] <= c[0] + c[2] and c[0] <= r[0] + r[2] and r[1] <= c[1] + c[3] and c[1] <= r[1] + r[3]
    comp = [r for r in dr if r[1] == 0 or (L.get("alley_rear") and r[1] + r[3] == L["depth"])]
    for _ in range(len(dr)):
        comp += [r for r in dr if r not in comp and any(touch(r, c) for c in comp)]
    hx0 = min(r.x for r in house); hx1 = max(r.x + r.w for r in house)
    hy0 = min(r.y for r in house); hy1 = max(r.y + r.h for r in house)
    why3 = []
    if od: ok3 = any(r[0] <= od.x1 and od.x2 <= r[0] + r[2] and r[1] <= od.y1 <= r[1] + r[3] for r in comp); why3.append("ends at garage door" if ok3 else "does not reach garage door")
    else:
        ok3 = any((r[0] == hx1 or r[0] + r[2] == hx0) and r[1] < hy1 and hy0 < r[1] + r[3] for r in comp)
        why3.append("ends at pad beside house" if ok3 else "no pad beside the house")
    veh = [n for n, r in plan.site.get("farm", []) if not any(touch(r, c) for c in comp)]
    if veh: ok3 = False; why3.append(f"buildings off the drive {veh}")
    kind = L.get("kind", "town")
    if kind in ("farmstead", "acreage") and len(dr) >= 2:
        fl, ln = dr[0], dr[1]
        if not (FT(24) <= fl[2] <= FT(40)): ok3 = False; why3.append("flare")
        if not (FT(12) <= ln[2] <= FT(16)): ok3 = False; why3.append("lane width")
        if kind == "farmstead" and not (FT(150) <= ln[3] <= FT(800)): ok3 = False; why3.append(f"lane {ft(ln[3])} not 150-800'")
    elif dr and min(r[2] for r in comp or dr) < FT(9): ok3 = False; why3.append("narrower than 9'")
    put("3", bool(dr) and ok3, f"{len(dr)} drive piece(s); " + "; ".join(why3))
    if garage is None:
        for r in ("4", "5"): put(r, True, "n/a: no garage")
    else:
        dbl = garage.w >= FT(18)
        inside_w, inside_d = garage.w - IN(9), garage.h - IN(9)
        if dbl: ok4 = garage.w >= FT(22) and garage.h >= FT(22) and od and od.w >= FT(16)
        else: ok4 = inside_w >= FT(11) and inside_d >= FT(21) and od and od.w >= FT(9)
        if plan.site.get("detached_garage"):
            gap = garage.y - max(r.y + r.h for r in house)
            ok4 = ok4 and gap >= FT(8)
        elif not dbl and not garage.w >= FT(12): ok4 = False
        put("4", bool(ok4), f"garage {ft(garage.w)} x {ft(garage.h)} outside, door {ft(od.w) if od else '-'}")
        into = [d for d in plan.doors if "garage" in (d.a, d.b) and d.role != "overhead"]
        put("5", not any(by.get(d.b if d.a == "garage" else d.a, garage).kind == "bedroom" for d in into), "no garage door into a bedroom")
    # 6 master = largest bedroom
    m = P.get("master")
    best = max(beds, key=lambda b: (b.area, [-ord(c) for c in b.id])).id if beds else None
    put("6", m is not None and m == best, f"master={m}, largest={best}")
    # 7 bath reachable without passing through a bedroom; lone bath off a hall
    reach = bfs(g, "outside", blocked={b.id for b in beds})
    hall_bath = [b for b in baths if b.id in reach]
    ok7 = bool(hall_bath) and (len(baths) > 1 or "hall" in kids(baths[0].id) if baths else False)
    put("7", ok7, f"{len(hall_bath)} bath(s) reachable without a bedroom")
    # 8 kitchen next to dining / garage entry (only if those exist)
    ok8, why = True, []
    if P.get("dining_room") and "dining" in by:
        ok8 &= "dining" in kids("kitchen"); why.append("kitchen-dining door")
    if attached:
        gh = [d for d in plan.doors if d.role == "garage_house"]
        land = [d.b if d.a == "garage" else d.a for d in gh]
        ok8 &= bool(land) and all(x == "kitchen" or ((by[x].kind if x in by else "") in ("mudroom", "utility") and "kitchen" in kids(x)) for x in land)
        why.append(f"garage lands in {land}")
    put("8", ok8, "; ".join(why) or "n/a")
    # 9 privacy gradient (door steps from the front door)
    if front:
        g2 = defaultdict(set, {k: set(v) for k, v in g.items()}); g2["FRONT"].add(front[0].a)
        st = bfs(g2, "FRONT", blocked={"outside"})
        pub = [st.get(r.id, 99) for r in house if r.kind in PUBLIC]
        bst = [st.get(b.id, 99) for b in beds]
        put("9", not bst or max(pub) < min(bst), f"public max {max(pub)} steps, bedroom min {min(bst) if bst else '-'}")
    else: put("9", False, "no front door")
    # 10 fixtures + bath light/vent
    bath_ok = all(b.fan or any(w.room == b.id and w.ro_w * w.ro_h >= 3 * SF for w in plan.windows) for b in baths)
    put("10", "kitchen" in by and bool(baths) and bath_ok, "kitchen+bath present; each bath window>=3sf or fan (fixture list: MEP layer)")
    # 11 reachability + door wall length
    reach_all = bfs(g, "outside")
    unreached = [r.id for r in rooms if r.id not in reach_all]
    bad_doors = []
    for d in plan.doors:
        if d.role == "overhead": continue
        axis = "h" if d.y1 == d.y2 else "v"
        lo, hi = (d.x1, d.x2) if axis == "h" else (d.y1, d.y2)
        c = d.y1 if axis == "h" else d.x1
        if d.b == "outside":
            segs = [(s[3], s[4]) for s in ext_segments(by[d.a], rooms) if s[1] == axis and s[2] == c]
        else:
            s = shared(by[d.a], by[d.b]) if d.a in by and d.b in by else None
            segs = [(s[2], s[3])] if s and s[0] == axis and s[1] == c else []
        if not any(a + CLR <= lo and hi <= b - CLR for a, b in segs): bad_doors.append(d.id)
    put("11", not unreached and not bad_doors, f"unreached={unreached} bad-door-walls={bad_doors}")
    # 12 halls >= 3 ft
    halls = [r for r in house if r.kind == "hall"]
    put("12", all(min(h.w, h.h) >= IN(36) for h in halls), f"hall widths {[ft(min(h.w, h.h)) for h in halls]}")
    # 13 room sizes (warn only)
    hab = [r for r in house if r.kind in HABITABLE]
    small = [r.id for r in hab if r.area < (50 if r.kind == "kitchen" else 70) * SF or (r.kind != "kitchen" and min(r.w, r.h) < FT(7))]
    put("13", not small and any(r.area >= 150 * SF for r in hab), f"undersized={small}")
    put("14", all(r.ceiling >= (IN(90) if r.kind in HABITABLE - {"kitchen"} else IN(84)) for r in house), "flat 8' ceilings (sloped: roof layer)")
    lowlight = [r.id for r in hab if sum(w.ro_w * w.ro_h for w in plan.windows if w.room == r.id) < 0.08 * r.area]
    put("15", not lowlight, f"RO glazing < 8% in {lowlight} (net glazing: window-unit layer)")
    # 16 egress
    noegress = [b.id for b in beds if not any(w.room == b.id and w.sill <= IN(44) and w.ro_w >= IN(24) and w.ro_h >= IN(30)
                                             and w.ro_w * w.ro_h >= 5.7 * SF for w in plan.windows)]
    put("16", not noegress, f"no egress window in {noegress} (clear opening needs window units)")
    put("17", any(d.b == "outside" and d.hinge == "side" and d.w == IN(36) and d.h == IN(80) for d in plan.doors),
        "3'-0\" x 6'-8\" side-hinged exit door (landing: site layer)")
    rep["18"] = ("pass" if halls and plan.site.get("smoke") else "warn", "detector spot reserved in hall (wiring: electrical layer)")
    rep["19"] = ("not-2D", "one-story 2D plans have no stairs")
    # 20 module
    hx0 = min(r.x for r in house); hy0 = min(r.y for r in house)
    hw = max(r.x + r.w for r in house) - hx0; hh = max(r.y + r.h for r in house) - hy0
    offgrid = [r.id for r in house if (r.x - hx0) % GRID or (r.y - hy0) % GRID or r.w % GRID or r.h % GRID]
    put("20", hw % MODULE == 0 and hh % MODULE == 0 and not offgrid,
        f"footprint {ft(hw)} x {ft(hh)}, off-grid={offgrid}; spans/headers/studs: wall+floor layers")
    # 21-24 draws recorded and realized
    put("21", P.get("style") in ("ranch", "two_story", "split_level"), f"style drawn={P.get('style')} (2D builds one story)")
    put("22", len(beds) == P.get("beds") and len(baths) == P.get("baths"), f"beds {len(beds)}/{P.get('beds')}, baths {len(baths)}/{P.get('baths')}")
    area = sum(r.area for r in house) / SF
    put("23", 1600 <= area <= 2000, f"{area:.0f} sf (band 1600-2000, target {P.get('area_sf')})")
    put("24", P.get("foundation") in ("basement", "slab", "crawl"), f"foundation={P.get('foundation')} (footings/anchors: foundation layer)")
    # 25 utility room when no basement
    if P.get("foundation") != "basement":
        u = [r for r in house if r.kind == "utility"]
        ok = bool(u) and all(min(r.w, r.h) >= IN(60) and not any(by.get(k) and by[k].kind in ("bedroom", "bath") for k in kids(r.id))
                             and all(d.w >= IN(20) for d in plan.doors if r.id in (d.a, d.b)) for r in u)
        put("25", ok, f"utility rooms {[r.id for r in u]}")
    else: put("25", True, "basement holds mechanical/laundry")
    # 26 closets
    def inside(c, r): return c and r.x <= c[0] and r.y <= c[1] and c[0] + c[2] <= r.x + r.w and c[1] + c[3] <= r.y + r.h
    noclos = [b.id for b in beds if not (inside(b.closet, b) and b.closet[3] >= IN(24) and b.closet[2] >= IN(48))]
    put("26", not noclos, f"bedrooms without closet {noclos}")
    # 27 secondary entries
    def kitchen_side(rid): return rid in by and (by[rid].kind in ("mudroom", "utility", "kitchen")
                                        or (by[rid].kind in ("dining", "family") and "kitchen" in kids(rid)))
    sec = [d for d in plan.doors if d.role in ("rear", "garage_house")]
    bad27 = [d.id for d in sec if not kitchen_side(d.a if d.b in ("outside", "garage") else d.b)]
    put("27", not bad27, f"secondary doors into non-kitchen-side rooms {bad27}")
    heads = {w.head for w in plan.windows} | {d.h + IN(3) for d in plan.doors if d.role in ("front", "rear", "garage_house")}
    put("28", len(heads) <= 1, f"{len(heads)} head height(s): {sorted(h / 16 for h in heads)} in")
    rep["29"] = ("not-2D", "roof form/pitch: roof layer (style suggests gable)")
    # 30 porch / deck placement
    ok30, why = True, []
    po, de = plan.site.get("porch"), plan.site.get("deck")
    if po:
        ok30 &= bool(front) and po[1] + po[3] == front[0].y1 and po[0] <= front[0].x1 and front[0].x2 <= po[0] + po[2]; why.append("porch at front door")
    if de:
        rd = next((d for d in plan.doors if d.role == "rear"), None)
        ok30 &= bool(rd) and de[1] == rd.y1 and de[0] <= rd.x1 and rd.x2 <= de[0] + de[2] and by[rd.a].kind in ("kitchen", "dining")
        why.append(f"{plan.site.get('deck_kind')} off {rd.a if rd else '-'}")
    put("30", ok30, "; ".join(why) or "n/a: none drawn")
    # 31 wet wall grouping
    wet = [r for r in house if r.kind in ("bath", "utility", "kitchen")]
    lone = [b.id for b in baths if len(baths) > 1 and not any(o is not b and (s := shared(b, o)) and s[3] - s[2] >= IN(24) for o in wet)]
    kd = min((max(b.x - (by["kitchen"].x + by["kitchen"].w), by["kitchen"].x - (b.x + b.w), 0) for b in baths), default=0) if "kitchen" in by else 0
    put("31", not lone, f"baths not on a shared wet wall {lone}; kitchen-to-nearest-bath gap {ft(kd)} (not enforced)")
    # 32 bedroom wing
    if beds:
        bx = (min(b.x for b in beds), min(b.y for b in beds), max(b.x + b.w for b in beds), max(b.y + b.h for b in beds))
        mixed = [r.id for r in house if r.kind in PUBLIC and r.x < bx[2] and bx[0] < r.x + r.w and r.y < bx[3] and bx[1] < r.y + r.h]
        offpub = [b.id for b in beds if any(by.get(k) and by[k].kind in PUBLIC for k in kids(b.id))]
        put("32", not mixed and not offpub, f"public rooms inside wing {mixed}; bedrooms off public rooms {offpub}")
    else: put("32", True, "n/a")
    s = plan.site; inlot = lambda r: r[0] >= 0 and r[1] >= 0 and r[0] + r[2] <= L["width"] and r[1] + r[3] <= L["depth"]
    hr = [r.rect() for r in rooms]
    bad = [n for n, r in s.get("farm", []) if not inlot(r) or any(_ov(r, h) for h in hr + s.get("grove", []))]
    bad += [r.id for r in rooms if any(_ov(r.rect(), gr) for gr in s.get("grove", []))]
    rep["FB"] = ("pass" if not bad else "fail", f"farm buildings inside lot, off house and grove; problems {bad}" if s.get("farm") else "n/a: no farm buildings" if not bad else f"{bad} in grove")
    rep["34"] = ("pass", "checked by tests (same seed -> same digest; neighbor isolation)")
    rep["35"] = ("not-2D", "skipped: needs neighborhood layer")
    rep["33"] = ("pass" if L.get("setting") == "rural" or L["width"] % FT(50) == 0 or L["width"] in (FT(60), FT(70), FT(80), FT(100)) else "warn", f"lot {ft(L['width'])} ({L.get('setting', 'town')})")
    if P.get("type") == "trailer":
        from spawner import TRAILER_NA
        for k, why in TRAILER_NA.items():
            if k == "21": rep[k] = ("pass" if P.get("style") == "single-wide" else "fail", why)
            else: rep[k] = ("n/a", "trailer: " + why)
    return {k: rep[k] for k in ALL + ["FB"]}

from settings import configurable as _configurable
validate = _configurable(validate)
