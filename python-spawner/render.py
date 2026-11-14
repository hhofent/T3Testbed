"""PNG renderer for 2D plans (matplotlib). Units drawn in feet."""
import matplotlib; matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle
from spawner import FT, SF
from settings import option
C = {"garage": "#d9d9d9", "kitchen": "#ffe8a3", "living": "#c9e7c2", "dining": "#f6d2b8", "family": "#f6d2b8",
     "bedroom": "#cfe0f7", "bath": "#bfeef0", "hall": "#f2f2f2", "utility": "#e6d5f2", "mudroom": "#e6d5f2"}
f = lambda t: t / FT(1)

def _rect(ax, xf, x, y, w, h, **kw):
    (ax0, ay0), (ax1, ay1) = xf(x, y), xf(x + w, y + h)
    ax.add_patch(Rectangle((f(min(ax0, ax1)), f(min(ay0, ay1))), f(abs(ax1 - ax0)), f(abs(ay1 - ay0)), **kw))

def _line(ax, xf, x1, y1, x2, y2, **kw):
    (a, b), (c, d) = xf(x1, y1), xf(x2, y2)
    ax.plot([f(a), f(c)], [f(b), f(d)], solid_capstyle="butt", **kw)

IDENT = lambda x, y: (x, y)

def draw_house(ax, plan, xf=IDENT, fs=5.5, labels=True):
    """Setback box, site features, rooms, doors and windows. xf maps lot-local ticks to drawing ticks."""
    L = plan.lot
    x0, x1 = L["side_left"], L["width"] - L["side_right"]
    _rect(ax, xf, x0, L["front_setback"], x1 - x0, L["depth"] - L["front_setback"] - L["rear_setback"],
          fill=False, ls="--", ec="#c33", lw=0.8, zorder=5)      # drawn above rooms (old bug: rooms hid it)
    if option('stages.trees') and not option('generation.parity'):
        from trees import local_markers
        for x,y in local_markers(plan):
            px,py=xf(x,y); ax.plot(f(px),f(py),'.',color='#42633b',markersize=2)
    elif option('generation.parity'):
        for g in plan.site.get("grove", []): _rect(ax, xf, *g, color="#5a9e4b", alpha=0.45, lw=0)
    for r in plan.site.get("drive", []): _rect(ax, xf, *r, color="#c8b48a" if L.get("drive_surface") == "gravel" else "#bbb", lw=0)
    for n, r in plan.site.get("farm", []):
        _rect(ax, xf, *r, fc="#7a4a2a", ec="k", lw=0.4)
        if labels:
            cx, cy = xf(r[0] + r[2] / 2, r[1] + r[3] / 2); ax.text(f(cx), f(cy), n, fontsize=fs, ha="center", va="center", color="w")
    for k, col in (("porch", "#d8b98a"), ("deck", "#b08a5a")):
        r = plan.site.get(k)
        if r: _rect(ax, xf, *r, color=col, alpha=0.8)
    for r in plan.rooms:
        _rect(ax, xf, r.x, r.y, r.w, r.h, fc=C.get(r.kind, "w"), ec="k", lw=0.5, hatch="//" if r.kind == "garage" else None)
        if r.closet: _rect(ax, xf, *r.closet, fc="#9fb7d9", ec="k", lw=0.3)
        if labels:
            cx, cy = xf(r.x + r.w / 2, r.y + r.h / 2)
            name = r.id + ("*" if r.id == plan.program.get("master") else "")
            ax.text(f(cx), f(cy), f"{name}\n{f(r.w):g}x{f(r.h):g}", ha="center", va="center", fontsize=fs)
    for d in plan.doors:
        col = {"overhead": "#e67e22", "interior": "#8b4513"}.get(d.role, "#d62728")
        _line(ax, xf, d.x1, d.y1, d.x2, d.y2, color=col, lw=2.2 if d.role != "interior" else 1.6)
    for w in plan.windows:
        _line(ax, xf, w.x1, w.y1, w.x2, w.y2, color="#1f77b4", lw=1.8)

def draw(ax, plan, report=None, title=None):
    L = plan.lot
    road_col = "#c8b48a" if L.get("road_type") == "gravel" else "#666"
    ax.add_patch(Rectangle((0, -12), f(L["width"]), 12, color=road_col))
    ax.text(f(L["width"]) / 2, -6, f"{L.get('address_street', 'road')} ({L.get('road_type', '?')})", ha="center", va="center", color="w", fontsize=7)
    ax.add_patch(Rectangle((0, 0), f(L["width"]), f(L["depth"]), fill=False, lw=1.2))
    draw_house(ax, plan, fs=5.5 if L["width"] <= FT(120) else 4)
    P = plan.program
    area = sum(r.area for r in plan.rooms if r.kind != "garage") / SF
    acres = L["width"] * L["depth"] / SF / 43560
    t = title or f"{L['id']}  {f(L['width']):g}'x{f(L['depth']):g}' ({acres:.1f} ac)  {P['beds']}bd/{P['baths']}ba  {area:.0f} sf  garage={P['garage_built']}"
    if report:
        warns = [k for k, (st, _) in report.items() if st == "warn"]
        t += f"\nattempt {P.get('attempt', 0) + 1}; warns: {','.join(warns) or 'none'}"
    ax.set_title(t, fontsize=7)
    ax.set_xlim(-3, f(L["width"]) + 3); ax.set_ylim(-14, f(L["depth"]) + 3)
    ax.set_aspect("equal"); ax.tick_params(labelsize=5)

def save(plan, report, path):
    fig, ax = plt.subplots(figsize=(5, 8)); draw(ax, plan, report); fig.tight_layout(); fig.savefig(path, dpi=150); plt.close(fig)

def contact_sheet(items, path):
    fig, axes = plt.subplots(1, len(items), figsize=(4 * len(items), 7.5))
    for ax, (p, r) in zip(axes, items): draw(ax, p, r)
    fig.tight_layout(); fig.savefig(path, dpi=130); plt.close(fig)
