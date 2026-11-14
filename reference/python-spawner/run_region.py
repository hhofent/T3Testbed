"""One small region (seed 30, 3 x 3 mi, natural rail town): roads -> rail -> town/lots -> houses; stats + PNGs.
Rail town with a mobile home park; park close-up."""
import json, math
from collections import Counter
import matplotlib; matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Polygon
from region import make_region, spawn_region, road_rect, to_world, world_rect, MILE, N_MI, FT, RAIL_ROW, ROW
from render import draw, draw_house, f

import sys
SEED = 31          # mid rail town
ONLY_TOWN = True   # task 8: rerender town.png only
reg = make_region(SEED)
res = spawn_region(reg)
kind = {l["spec"]["id"]: l["spec"]["kind"] for l in reg["lots"]}
fails, warns = Counter(), Counter()
for v in res.values():
    fails.update(v["fails"])
    if v["report"]: warns.update(k for k, (s, _) in v["report"].items() if s == "warn")
def grp(ks): return {"lots": len(ks), "built": sum(bool(res[k]["plan"]) for k in ks),
                     "avg_attempts": round(sum(res[k]["attempts"] for k in ks) / max(1, len(ks)), 2)}
t = reg["town"]
town_lots = [l["spec"] for l in reg["lots"] if l["spec"]["kind"] == "town"]
stats = {"seed": SEED, "region_mi": f"{N_MI} x {N_MI}", "road_miles": reg["road_miles"],
         "lots": len(res), "built": sum(bool(v["plan"]) for v in res.values()),
         "success_rate": round(sum(bool(v["plan"]) for v in res.values()) / len(res), 3),
         "avg_attempts": round(sum(v["attempts"] for v in res.values()) / len(res), 2),
         "by_kind": {k: grp([i for i in res if kind[i] == k]) for k in ("farmstead", "acreage", "town")},
         "town_by_base_lots": {n: grp([s["id"] for s in town_lots if s["base_lots"] == n]) for n in (1, 2, 3, 4)},
         "trailers": grp([s["id"] for s in town_lots if s["house_type"] == "trailer"]),
         "rail": {"along": reg["rail"]["along"], "parallel_ft": (reg["rail"]["parallel"][1] - reg["rail"]["parallel"][0]) / FT(1),
                  "grade_crossings": len(reg["rail"]["crossings"]), "rail_town": bool(t and t["rail_town"])} if reg["rail"] else None,
         "park": {"pattern": reg["park"]["pattern"], "pads": len(reg["park"]["pads"]), "acres": round(reg["park"]["acres"], 2),
                  "pitch_ft": reg["park"]["pitch"] / FT(1), "lanes": len(reg["park"]["lanes"]), "windbreak": reg["park"]["windbreak_sides"],
                  "road": reg["park"]["road_name"]} if reg["park"] else None,
         "park_check": t.get("park_check") if t else None,
         "commercial": [(c["type"], c["site"]) for c in t["commercial"]] if t else [],
         "pop_est": t and t["pop_est"], "band": t and t["band"], "biz_program": t and t["program"], "biz_unplaced": t and t["unplaced"],
         "drive_check_3": Counter(v["report"]["3"][0] for v in res.values() if v["report"]),
         "paved_pct_drawn": reg["paved_pct_drawn"], "paved_share_built": reg["paved_share"],
         "farmsteads_per_road_mile": round(sum(1 for k in kind.values() if k == "farmstead") / reg["road_miles"], 2),
         "intersection_clear_ft": reg["clear"] / FT(1),
         "town": {"blocks": t["cols"] * t["rows"], "grid": f"{t['cols']}x{t['rows']}", "block_cx_ft": t["cx"] / FT(1),
                  "block_cy_ft": t["cy"] / FT(1), "street_ft": t["street_w"] / FT(1), "alley_ft": t["alley_w"] / FT(1),
                  "lot_depths_ft": sorted({b["lot_d"] / FT(1) for b in t["blocks"]}),
                  "setbacks_ft": {k: v / FT(1) for k, v in t["setbacks"].items()},
                  "lots_planned": len(t["lots"]), "lots_dropped_by_rail": len(t["lots"]) - len(town_lots)} if t else None,
         "parcels": [(p["kind"], round(p["acres"], 2)) for p in reg["parcels"]],
         "retry_causes": fails.most_common(), "warn_counts": warns.most_common(),
         "failed_lots": {k: v["fails"] for k, v in res.items() if not v["plan"]},
         "roads": [(r["id"], r["name"], r["type"], r["surface_ft"]) for r in reg["grid_roads"]]}
json.dump(stats, open("out/region_stats.json", "w"), indent=1)
print(json.dumps({k: stats[k] for k in stats if k != "roads"}, indent=1))

ROADC = {"gravel": "#c8a26a", "paved": "#333"}
PC = {"industry": "#c9b48a", "cemetery": "#9bb59b", "church": "#e0c0e0", "fuel": "#f0a040", "elevator": "#b0b0d0"}
def draw_roads(ax):
    for r in reg["roads"]:
        x, y, w, h = road_rect(r)
        ax.add_patch(Rectangle((f(x), f(y)), f(w), f(h), color=ROADC[r["type"]], alpha=0.3, lw=0))
        s = FT(r["surface_ft"])
        rr = (r["lo"], r["c"] - s // 2, r["hi"] - r["lo"], s) if r["axis"] == "h" else (r["c"] - s // 2, r["lo"], s, r["hi"] - r["lo"])
        ax.add_patch(Rectangle((f(rr[0]), f(rr[1])), f(rr[2]), f(rr[3]), color=ROADC[r["type"]], lw=0))
    if t:
        for a in t["alleys"]: ax.add_patch(Rectangle((f(a[0]), f(a[1])), f(a[2]), f(a[3]), color="#ddd", lw=0))
    for c in reg.get("creeks", []):
        X = [f(p[0]) for p in c["pts"]]; Y = [f(p[1]) for p in c["pts"]]
        ax.plot(X, Y, color="#9cd", alpha=0.35, lw=max(2, f(c["w"]) / 6), solid_capstyle="round")
        ax.plot(X, Y, color="#2a7fc0", lw=max(1, f(c["chan_w"]) / 12))
        for _, (bx, by) in c["bridges"]: ax.plot(f(bx), f(by), "s", mfc="none", mec="#036", ms=7)
    rl = reg["rail"]
    if rl:
        P = rl["pts"]; ax.plot([f(p[0]) for p in P], [f(p[1]) for p in P], color="#aa9", alpha=0.35, lw=max(1, f(RAIL_ROW) / 40), solid_capstyle="butt")
        ax.plot([f(p[0]) for p in P], [f(p[1]) for p in P], color="#552", lw=1.4, ls=(0, (6, 2)))
        for _, (cx, cy) in rl["crossings"]: ax.plot(f(cx), f(cy), "x", color="#c00", ms=5)
    if reg["siding"]:
        (a, b), (c, d) = reg["siding"]; ax.plot([f(a), f(c)], [f(b), f(d)], color="#552", lw=1.0)
    if reg["depot"]:
        x, y, w, h = reg["depot"]; ax.add_patch(Rectangle((f(x), f(y)), f(w), f(h), color="#a33"))
        ax.text(f(x + w) + 10, f(y + h / 2), "depot", fontsize=6, color="#a33")

def draw_park(ax, fs=6):
    p = reg["park"]
    if not p: return
    x, y, w, h = p["rect"]; ax.add_patch(Rectangle((f(x), f(y)), f(w), f(h), fc="#f4f1e6", ec="#a60", lw=0.8, ls="--"))
    for r in p["windbreak"]: ax.add_patch(Rectangle((f(r[0]), f(r[1])), f(r[2]), f(r[3]), color="#5a8f4a", alpha=0.6, lw=0))
    if p["pond"]: r = p["pond"]; ax.add_patch(Rectangle((f(r[0]), f(r[1])), f(r[2]), f(r[3]), color="#7fb0c8", lw=0))
    for r in p["lanes"]: ax.add_patch(Rectangle((f(r[0]), f(r[1])), f(r[2]), f(r[3]), color="#999", lw=0))
    for q in p["pads"]:
        for r, c in ((q["parking"], "#cbbf9f"), (q["trailer"], "#e8e8f8")):
            ax.add_patch(Rectangle((f(r[0]), f(r[1])), f(r[2]), f(r[3]), fc=c, ec="#335", lw=0.4))
    ax.text(f(x + w / 2), f(y + h) + 8, f"mobile home park ({p['pattern']}, {len(p['pads'])} pads, placeholder)", ha="center", fontsize=fs, color="#a60")

def draw_commercial(ax, fs=5, labels=True):
    if not t: return
    for c in t["commercial"]:
        R = lambda r, **k: ax.add_patch(Rectangle((f(r[0]), f(r[1])), f(r[2]), f(r[3]), **k))
        R(c["lot"], fill=False, ec="#c60", lw=0.6)
        for p in c["parking"]: R(p, fc="#d8d8d8", ec="#888", lw=0.3)
        if c["canopy"]: R(c["canopy"], fc="none", ec="#c00", lw=0.6, ls="--")
        for p in c["pumps"]: R(p, color="#c00")
        if c.get("curb"): R(c["curb"], fc="#bbb", ec="#777", lw=0.3, hatch="||")
        if c.get("field"): R(c["field"], fc="#9c6", ec="#585", lw=0.4)
        if c.get("parsonage"): R(c["parsonage"], fc="#e8d0a0", ec="k", lw=0.4)
        b = c["bldg"] or c["lot"]
        if c["bldg"]: R(b, fc="#ccc" if c.get("vacant") else "#f0b070", ec="k", lw=0.5, hatch="//" if c.get("vacant") else None)
        if labels:
            ax.text(f(b[0] + b[2] / 2), f(b[1] + b[3] / 2), c["type"], ha="center", va="center", fontsize=fs,
                    rotation=90 if b[2] < FT(45) else 0, color="#a40" if not c["bldg"] else "k")

def draw_parcels(ax, fs=7):
    for p in reg["parcels"]:
        if p.get("in_town"): continue
        x, y, w, h = p["rect"]
        ax.add_patch(Rectangle((f(x), f(y)), f(w), f(h), fc=PC[p["kind"]], ec="k", lw=0.6))
        ax.text(f(x + w / 2), f(y + h / 2), f"{p['name']}\n{p['acres']:.1f} ac", ha="center", va="center", fontsize=fs)
    if reg["siding"]: pass

def xf_for(L):
    s, pl = L["spec"], L["place"]; return lambda x, y: to_world(pl, s["width"], s["depth"], x, y)

def draw_lot(ax, L, labels, fs):
    s = L["spec"]; x, y, w, h = world_rect(L["place"], s["width"], s["depth"])
    ax.add_patch(Rectangle((f(x), f(y)), f(w), f(h), fill=False, lw=0.6, ec="#2a6"))
    p = res[s["id"]]["plan"]
    if p: draw_house(ax, p, xf_for(L), fs=fs, labels=labels)
    else: ax.add_patch(Rectangle((f(x), f(y)), f(w), f(h), fill=False, hatch="xx", ec="r", lw=0.3))

# region overview
fig, ax = plt.subplots(figsize=(13, 13))
draw_roads(ax); draw_parcels(ax, 5); draw_park(ax, 5); draw_commercial(ax, labels=False)
for L in reg["lots"]: draw_lot(ax, L, False, 3)
for r in reg["grid_roads"]:
    if r["axis"] == "h": ax.text(f(MILE * 0.03), f(r["c"] + FT(80)), f"{r['name']} ({r['type']})", fontsize=7)
    else: ax.text(f(r["c"] + FT(60)), f(MILE * 0.03), f"{r['name']} ({r['type']})", fontsize=7, rotation=90)
if t:
    b = t["box"]; ax.add_patch(Rectangle((f(b[0]), f(b[1])), f(b[2]), f(b[3]), fill=False, ec="purple", ls=":", lw=1.2))
    ax.text(f(b[0]), f(b[1] + b[3]) + 60, "town", color="purple", fontsize=8)
ax.set_aspect("equal"); ax.set_xlim(-200, f(N_MI * MILE) + 200); ax.set_ylim(-200, f(N_MI * MILE) + 200)
ax.set_title(f"Region seed {SEED}: {N_MI} x {N_MI} mi. Tan = gravel, black = paved (lettered county routes), dashed = rail. "
             f"{stats['built']}/{stats['lots']} lots built; green = groves, brown = farm buildings", fontsize=8)
fig.tight_layout(); (fig.savefig("out/region.png", dpi=130) if not ONLY_TOWN else None); plt.close(fig)

# rural zoom: 2 farmsteads + 2 acreages, whole lot on top, house/yard zoom below
pick = [L for L in reg["lots"] if L["spec"]["kind"] == "farmstead" and res[L["spec"]["id"]]["plan"]][:2] + \
       [L for L in reg["lots"] if L["spec"]["kind"] == "acreage" and res[L["spec"]["id"]]["plan"]][:2]
fig, axes = plt.subplots(2, len(pick), figsize=(5 * len(pick), 13), gridspec_kw={"height_ratios": [1.3, 1.7]})
for top, ax, L in zip(axes[0], axes[1], pick):
    p = res[L["spec"]["id"]]["plan"]; draw(top, p, res[L["spec"]["id"]]["report"], )
    s = L["spec"]; top.set_xlabel(f"{s['kind']} on {s['address_street']} ({s['road_type']}), house {f(s['front_setback']):.0f} ft back; grove sides {s['grove_sides']}", fontsize=6)
    draw(ax, p); lane = p.site["drive"][1]
    ax.set_title(f"zoom: lane {f(lane[2]):g} ft wide x {f(lane[3]):.0f} ft, flare {f(p.site['drive'][0][2]):g} ft", fontsize=7)
    rs = [r.rect() for r in p.rooms] + [r for _, r in p.site.get("farm", [])]
    x0 = min(r[0] for r in rs); x1 = max(r[0] + r[2] for r in rs); y1 = max(r[1] + r[3] for r in rs)
    ax.set_xlim(f(x0) - 30, f(x1) + 30); ax.set_ylim(-14, f(y1) + 30)
fig.tight_layout(); (fig.savefig("out/neighborhood.png", dpi=140) if not ONLY_TOWN else None); plt.close(fig)

# town zoom
def town_png(path, note):
    b = t["box"]; m = FT(120)
    el = next((p["rect"] for p in reg["parcels"] if p["kind"] == "elevator"), b)
    if reg["park"]:
        pr = reg["park"]["rect"]; x0, y0 = min(b[0], pr[0]), min(b[1], pr[1]); b = (x0, y0, max(b[0] + b[2], pr[0] + pr[2]) - x0, max(b[1] + b[3], pr[1] + pr[3]) - y0)
    for e in [el] + [c["lot"] for c in t["commercial"]]:
        x0, y0 = min(b[0], e[0]), min(b[1], e[1]); b = (x0, y0, max(b[0] + b[2], e[0] + e[2]) - x0, max(b[1] + b[3], e[1] + e[3]) - y0)
    fig, ax = plt.subplots(figsize=(16, 16 * (b[3] + 2 * m) / (b[2] + 2 * m)))
    draw_roads(ax); draw_parcels(ax, 7); draw_park(ax, 7); draw_commercial(ax, 5)
    for L in reg["lots"]:
        if L["spec"]["kind"] == "town": draw_lot(ax, L, True, 2.6)
    for r in reg["roads"]:
        if r["axis"] == "h" and b[1] - m < r["c"] < b[1] + b[3] + m:
            ax.text(f(b[0] - m + FT(20)), f(r["c"]), f"{r['name']} ({r['type']})", fontsize=7, color="w", va="center")
        if r["axis"] == "v" and b[0] - m < r["c"] < b[0] + b[2] + m:
            ax.text(f(r["c"]), f(b[1] + b[3] + m - FT(40)), r["name"], fontsize=7, rotation=90, ha="center", color="w")
    ax.set_xlim(f(b[0] - m), f(b[0] + b[2] + m)); ax.set_ylim(f(b[1] - m), f(b[1] + b[3] + m)); ax.set_aspect("equal")
    ntr = sum(1 for L in reg["lots"] if L["spec"].get("house_type") == "trailer")
    ax.set_title(f"Town (seed {SEED}, natural, {t['size']}): {t['cols']}x{t['rows']} blocks, alleys, {note}; {ntr} trailers on town lots; " + (f"{len(reg['park']['pads'])} trailers in the park; " if reg["park"] else f"no park ({t.get('park_check', {}).get('reason', 'not drawn')}); ") +
                 f" creeks: {len(reg.get('creeks', []))} (blue, pale = floodplain, square = bridge);\nsnapshot year {t['year']}; pop est {t['pop_est']} ({t['houses_est']} houses x 2.4, placeholder), band {['<250', '250-749', '750-1499'][t['band']]}; businesses (CRG mix, placeholder): "
                 + ", ".join(c["type"] for c in t["commercial"]) + "; hatched grey = curb parking", fontsize=9)
    import textwrap
    ax.set_title(textwrap.fill(ax.get_title(), 200), fontsize=8)
    for tx in ax.texts: tx.set_clip_on(True)
    fig.tight_layout(); fig.savefig(path, dpi=150); plt.close(fig)
RENDER_SMALL = True    # second render: big town (seed 14) -> town_big.png
if t: town_png("out/town.png", "rail along the road + depot + siding + elevator" if t["rail_town"] else "no rail")

# park close-up
p = reg["park"]
if p:
    x, y, w, h = p["rect"]; m = FT(60); rd = next(r for r in reg["grid_roads"] if r["id"] == p["road"])
    y0 = min(y, rd["c"] - ROW); y1 = max(y + h, rd["c"] + ROW)
    fig, ax = plt.subplots(figsize=(10, 10 * (y1 - y0 + 2 * m) / (w + 2 * m)))
    draw_roads(ax); draw_park(ax, 8)
    ax.text(f(x), f(rd["c"]), f"{rd['name']} ({rd['type']})", fontsize=8, color="w", va="center")
    ax.set_xlim(f(x - m), f(x + w + m)); ax.set_ylim(f(y0 - m), f(y1 + m)); ax.set_aspect("equal")
    ax.set_title(f"Mobile home park (seed {SEED}), {p['pattern']} layout, {len(p['pads'])} pads at {f(p['pitch']):g} ft pitch, "
                 f"{p['acres']:.1f} ac; windbreak {p['windbreak_sides']}. Grey = private lanes, tan = parking pads. ALL PLACEHOLDER", fontsize=8)
    fig.tight_layout(); fig.savefig("out/park.png", dpi=150); plt.close(fig)

# big town: seed 14, town lots only
if not RENDER_SMALL: raise SystemExit
SEED = 14
reg = make_region(SEED); reg["lots"] = [L for L in reg["lots"] if L["spec"]["kind"] == "town"]
res = spawn_region(reg); t = reg["town"]
town_png("out/town_big.png", "rail along the road + depot + siding + elevator" if t["rail_town"] else "no rail")
print("seed 14", t["size"], t["year"], t["pop_est"], [c["type"] for c in t["commercial"]], sum(bool(v["plan"]) for v in res.values()), len(res))
