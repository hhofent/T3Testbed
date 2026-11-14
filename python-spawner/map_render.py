"""Reference map drawing, with parks removed. No server is needed for PNG output."""
import io
import matplotlib; matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Polygon
from region import road_rect, to_world, world_rect, MILE, N_MI, FT, RAIL_ROW, ROW
from render import draw_house, f
ROADC = {'gravel':'#c8a26a','paved':'#333','dirt':'#99794e'}
PC = {'industry':'#c9b48a','cemetery':'#9bb59b','church':'#e0c0e0','fuel':'#f0a040','elevator':'#b0b0d0'}
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
    p = res.get(s["id"],{}).get("plan")
    if p: draw_house(ax, p, xf_for(L), fs=fs, labels=labels)
    elif s["id"] in res: ax.add_patch(Rectangle((f(x), f(y)), f(w), f(h), fill=False, hatch="xx", ec="r", lw=0.3))

def png(region,results,town_only=True,framed=False):
    global reg,res,t
    reg,res,t=region,results,region['town']
    extent=max(r['hi'] for r in reg['grid_roads']); b=t['box'] if t and town_only else (0,0,extent,extent)
    if t and town_only:
        for e in [c['lot'] for c in t['commercial']]:
            x,y=min(b[0],e[0]),min(b[1],e[1]); b=(x,y,max(b[0]+b[2],e[0]+e[2])-x,max(b[1]+b[3],e[1]+e[3])-y)
    margin=FT(120)
    fig,ax=plt.subplots(figsize=(12,12*(b[3]+2*margin)/(b[2]+2*margin)))
    try:
        draw_roads(ax); draw_parcels(ax,5); draw_commercial(ax,labels=False)
        for lot in reg['lots']:
            if not town_only or lot['spec']['kind']=='town': draw_lot(ax,lot,False,2.6)
        ax.set_xlim(f(b[0]-margin),f(b[0]+b[2]+margin)); ax.set_ylim(f(b[1]-margin),f(b[1]+b[3]+margin)); ax.set_aspect('equal')
        ax.set_axis_off()
        if framed: ax.set_position([.01,.01,.98,.98])
        else: fig.tight_layout()
        buf=io.BytesIO(); fig.savefig(buf,format='png',dpi=110,metadata={'Software':'Python spawner'}); return buf.getvalue()
    finally: plt.close(fig)


def world_png(scene,width=1440):
    """Same geometry, colors, LOD rules and pixel minima as the browser SVG."""
    from world import WORLD_STYLE,feature_style
    from matplotlib.collections import LineCollection,PatchCollection
    from matplotlib.patches import Circle
    x,y,w,h=scene['window']; height=max(1,round(width*h/w)); view=max(w,h)/MILE
    fig=plt.figure(figsize=(width/100,height/100),dpi=100,facecolor=WORLD_STYLE['background'])
    ax=fig.add_axes([0,0,1,1]); ax.set_facecolor(WORLD_STYLE['background'])
    try:
        for layer,kind in enumerate(WORLD_STYLE['order']):
            for ftr in scene['features']:
                if ftr['kind']!=kind: continue
                style=feature_style(ftr); color=style['color']; minimum=style['minimum_px']
                if style['hide_above_mi'] and view>style['hide_above_mi']: continue
                if kind in ('trees','farms'):
                    a,b,c,d=ftr['rect']; radius=FT(45 if kind=='farms' else __import__('settings').option('world.tree_radius_ft'))
                    ax.add_patch(Circle((a+c/2,b+d/2),radius,facecolor=color,edgecolor='none',snap=False,zorder=layer+1))
                elif 'shape' in ftr:
                    ax.add_patch(Polygon(ftr['shape'],closed=True,facecolor='none',edgecolor=WORLD_STYLE['parcel_outline'],linewidth=WORLD_STYLE['outline_px']*.72,snap=False,zorder=layer+1))
                elif 'rect' in ftr:
                    a,b,c,d=ftr['rect']; edge=WORLD_STYLE['parcel_outline'] if kind in ('towns','lots') else WORLD_STYLE['outline']
                    line=minimum or WORLD_STYLE['outline_px']
                    ax.add_patch(Rectangle((a,b),c,d,facecolor=color,edgecolor=edge,linewidth=line*.72,snap=False,zorder=layer+1))
                else:
                    points=ftr['points']; widths=ftr.get('widths')
                    raw=ftr.get('width',FT(1))
                    if kind=='rail' or (kind=='bridges' and ftr.get('surface')=='rail'): raw=FT(__import__('settings').option('world.rail_draw_ft'))
                    if widths:
                        segments=list(zip(points,points[1:])); strokes=[max(minimum,(a+b)/2/w*width)*.72 for a,b in zip(widths,widths[1:])]
                    else: segments=[points]; strokes=[max(minimum,raw/w*width)*.72]
                    ax.add_collection(LineCollection(segments,colors=color,linewidths=strokes,capstyle='round' if widths else 'butt',snap=False,zorder=layer+1))
        ax.set_xlim(x,x+w); ax.set_ylim(y+h,y); ax.set_aspect('equal',adjustable='box');ax.set_axis_off()
        buf=io.BytesIO();fig.savefig(buf,format='png',dpi=100,facecolor=fig.get_facecolor(),metadata={'Software':'Python spawner'});return buf.getvalue()
    finally: plt.close(fig)
