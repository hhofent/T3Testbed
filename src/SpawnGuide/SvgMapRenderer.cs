using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpawnGuide
{

/// <summary>
/// Renders a MapPlan as a top-down SVG: county road or town street, lot boundaries,
/// driveways and walks, houses (shared AppendHouse core), outbuildings, trees, labels.
/// Street edge is at the bottom of the map.
/// </summary>
public static class SvgMapRenderer
{
    /// <param name="bare">World-window mode: no margins, title, footer or north arrow, and
    /// the viewBox is exactly the plan bounds so an SVG maps 1:1 onto a world rectangle.</param>
    /// <param name="targetWidth">Natural SVG width in pixels (bare mode).</param>
    public static string Render(MapPlan map, bool bare = false, float targetWidth = 920f)
    {
        float Margin = bare ? 0f : 30f;
        float TitleH = bare ? 0f : 66f, FootH = bare ? 0f : 26f;

        // Bounding box. ExactBounds windows are fixed by definition; ordinary plans scan
        // all content (fields included — layouts may reach negative z).
        float minX = 0, maxX = map.WidthFeet, minZ = 0, maxZ = map.DepthFeet;
        if (map.ExactBounds)
        {
            // Nothing outside the window may widen the frame.
        }
        else if (map.Fields.Count > 0)
        {
            minX = map.Fields.Min(f => f.X);
            maxX = map.Fields.Max(f => f.X + f.W);
            minZ = map.Fields.Min(f => f.Z);
            maxZ = map.Fields.Max(f => f.Z + f.H);
        }
        void Box(float x, float z, float w = 0, float h = 0)
        {
            if (map.ExactBounds) return;
            minX = MathF.Min(minX, x); maxX = MathF.Max(maxX, x + w);
            minZ = MathF.Min(minZ, z); maxZ = MathF.Max(maxZ, z + h);
        }
        foreach (var r in map.Roads) Box(r.X, r.Z, r.W, r.H);
        foreach (var r in map.Alleys) Box(r.X, r.Z, r.W, r.H);
        foreach (var r in map.LotLines) Box(r.X, r.Z, r.W, r.H);
        foreach (var w in map.Waters)
            foreach (var p in w.Points) Box(p.X - w.WidthFeet, p.Z - w.WidthFeet, 2 * w.WidthFeet, 2 * w.WidthFeet);
        foreach (var h in map.Houses)
            foreach (var p in h.Plan.Footprint) Box(p.X - 1, p.Z - 1, 2, 2);
        foreach (var o in map.Outbuildings)
        {
            float hw = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet / 2f : o.WidthFeet / 2f;
            float hd = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet / 2f : o.DepthFeet / 2f;
            Box(o.Center.X - hw - 2, o.Center.Z - hd - 2, 2 * hw + 4, 2 * hd + 10); // labels hang below buildings
        }
        foreach (var t in map.Trees) Box(t.X - 6, t.Z - 6, 12, 12);
        foreach (var d in map.Driveways.Concat(map.Walks))
            foreach (var p in d.Points) Box(p.X, p.Z, 1, 1);
        foreach (var l in map.Labels) Box(l.Position.X - 30, l.Position.Z - 10, 60, 20);

        float spanX = Math.Max(1f, maxX - minX);
        float spanZ = Math.Max(1f, maxZ - minZ);
        float scale = (targetWidth - 2 * Margin) / spanX;
        const float MaxH = 900f;
        if (!bare && spanZ * scale > MaxH) scale = MaxH / spanZ;

        float widthPx = bare ? MathF.Round(spanX * scale) : MathF.Ceiling(spanX * scale + 2 * Margin);
        float heightPx = bare ? MathF.Round(spanZ * scale) : MathF.Ceiling(spanZ * scale + TitleH + FootH);

        float ox = Margin - minX * scale;
        float oy = bare ? (heightPx - spanZ * scale) : TitleH + (heightPx - TitleH - FootH - spanZ * scale) / 2f;

        SvgPx Map(Vec2 p) => new(ox + p.X * scale, oy + (maxZ - p.Z) * scale);
        string N(float v) => SvgRenderer.Format(v);

        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{N(widthPx)}' height='{N(heightPx)}' viewBox='0 0 {N(widthPx)} {N(heightPx)}' font-family='Georgia, serif'>");
        sb.AppendLine("<style>");
        sb.AppendLine(".floor{fill:#e9e4d2}");
        sb.AppendLine(".wall{stroke:#3a332b;fill:none}");
        sb.AppendLine(".thin{stroke:#3a332b;stroke-width:1.3;fill:none}");
        sb.AppendLine(".dash{stroke:#8a7f70;stroke-width:1.3;fill:none;stroke-dasharray:7 5}");
        sb.AppendLine(".txt{fill:#3a332b;font-size:13px;text-anchor:middle}");
        sb.AppendLine(".small{fill:#57503f;font-size:11px}");
        sb.AppendLine(".title{fill:#3a332b;font-size:19px;font-weight:bold}");
        sb.AppendLine(".roadlabel{fill:#57503f;font-size:13px;letter-spacing:3px;text-anchor:middle}");
        sb.AppendLine("</style>");

        // Background: farmland / grass parchment.
        sb.AppendLine($"<rect x='0' y='0' width='{N(widthPx)}' height='{N(heightPx)}' fill='#f0efe0'/>");

        // Field fills (countryside view), under everything else.
        for (int i = 0; i < map.Fields.Count; i++)
        {
            var fr = map.Fields[i];
            string fill = i < map.FieldFills.Count ? map.FieldFills[i] : "#ded9bd";
            var a = Map(new Vec2(fr.X, fr.Z + fr.H));
            sb.AppendLine($"<rect x='{N(a.X)}' y='{N(a.Y)}' width='{N(fr.W * scale)}' height='{N(fr.H * scale)}' fill='{fill}'/>");
        }

        // Creeks / drainage ribbons: low-contrast blue-gray, with a darker edge. Roads are
        // drawn over them (culverts/bridges read as crossing pavement).
        foreach (var w in map.Waters)
        {
            if (w.Points.Count < 2) continue;
            var pts = string.Join(" ", w.Points.Select(p =>
            {
                var m = Map(p);
                return $"{N(m.X)},{N(m.Y)}";
            }));
            sb.AppendLine($"<polyline points='{pts}' fill='none' stroke='#7f989b' stroke-width='{N(MathF.Max(0.8f, w.WidthFeet * scale))}' stroke-linecap='round' stroke-linejoin='round' stroke-opacity='0.9'/>");
            sb.AppendLine($"<polyline points='{pts}' fill='none' stroke='#a8bfc0' stroke-width='{N(MathF.Max(0.4f, w.WidthFeet * scale * 0.45f))}' stroke-linecap='round' stroke-linejoin='round' stroke-opacity='0.6'/>");
        }

        // Shelterbelt / trees under everything else.
        foreach (var t in map.Trees)
        {
            var c = Map(t);
            // Trunk-location dots only — no canopy fill.
            float r = map.LodSimple ? 3.2f : MathF.Max(0.9f, 2.2f * scale);
            sb.AppendLine($"<circle cx='{N(c.X)}' cy='{N(c.Y)}' r='{N(r)}' fill='#3f3b34' fill-opacity='{(map.LodSimple ? "0.75" : "0.9")}'/>");
        }

        // Lot boundaries (hidden at countryside zoom).
        foreach (var r in map.LotLines)
        {
            if (map.LodSimple) continue;
            var a = Map(new Vec2(r.X, r.Z + r.H));
            var b = Map(new Vec2(r.X + r.W, r.Z));
            sb.AppendLine($"<rect class='dash' x='{N(a.X)}' y='{N(a.Y)}' width='{N(r.W * scale)}' height='{N(r.H * scale)}'/>");
        }

        // Roads and alleys.
        for (int ri = 0; ri < map.Roads.Count; ri++)
        {
            var r = map.Roads[ri];
            var a = Map(new Vec2(r.X, r.Z + r.H));
            bool vertical = r.H > r.W;
            bool dashed = map.RoadDashed.Count == map.Roads.Count ? map.RoadDashed[ri] : map.RoadCenterDashes;
            bool asphalt = map.RoadSurface == "asphalt" || (map.RoadSurface == "" && dashed);
            string fill = asphalt ? "#8f8b83" : "#c8c3b5";
            sb.AppendLine($"<rect x='{N(a.X)}' y='{N(a.Y)}' width='{N(r.W * scale)}' height='{N(r.H * scale)}' fill='{fill}'/>");
            if (dashed)
            {
                if (vertical)
                {
                    float cx = a.X + r.W * scale / 2f;
                    sb.AppendLine($"<line x1='{N(cx)}' y1='{N(a.Y + 6)}' x2='{N(cx)}' y2='{N(a.Y + r.H * scale - 6)}' stroke='#e8d44d' stroke-width='2' stroke-dasharray='14 12'/>");
                }
                else
                {
                    float cy = a.Y + r.H * scale / 2f;
                    sb.AppendLine($"<line x1='{N(a.X + 6)}' y1='{N(cy)}' x2='{N(a.X + r.W * scale - 6)}' y2='{N(cy)}' stroke='#e8d44d' stroke-width='2' stroke-dasharray='14 12'/>");
                }
            }
            else if (!asphalt && vertical)
            {
                float x0 = a.X, x1 = a.X + r.W * scale;
                sb.AppendLine($"<line x1='{N(x0 + 2)}' y1='{N(a.Y)}' x2='{N(x0 + 2)}' y2='{N(a.Y + r.H * scale)}' stroke='#a9a495' stroke-width='1.5'/>");
                sb.AppendLine($"<line x1='{N(x1 - 2)}' y1='{N(a.Y)}' x2='{N(x1 - 2)}' y2='{N(a.Y + r.H * scale)}' stroke='#a9a495' stroke-width='1.5'/>");
            }
            else if (!asphalt)
            {
                float y0 = a.Y, y1 = a.Y + r.H * scale;
                sb.AppendLine($"<line x1='{N(a.X)}' y1='{N(y0 + 2)}' x2='{N(a.X + r.W * scale)}' y2='{N(y0 + 2)}' stroke='#a9a495' stroke-width='1.5'/>");
                sb.AppendLine($"<line x1='{N(a.X)}' y1='{N(y1 - 2)}' x2='{N(a.X + r.W * scale)}' y2='{N(y1 - 2)}' stroke='#a9a495' stroke-width='1.5'/>");
            }
        }
        foreach (var r in map.LodSimple ? Enumerable.Empty<MapRect>() : map.Alleys)
        {
            var a = Map(new Vec2(r.X, r.Z + r.H));
            sb.AppendLine($"<rect x='{N(a.X)}' y='{N(a.Y)}' width='{N(r.W * scale)}' height='{N(r.H * scale)}' fill='#c9c3b2'/>");
        }

        // Cul-de-sac bulbs (asphalt turnarounds) on top of road ends.
        foreach (var b in map.Bulbs)
        {
            var c = Map(b.Center);
            string fill = b.Fill ?? (map.RoadSurface == "asphalt" ? "#8f8b83" : "#c8c3b5");
            sb.AppendLine($"<circle cx='{N(c.X)}' cy='{N(c.Y)}' r='{N(b.RadiusFeet * scale)}' fill='{fill}'/>");
        }

        // Driveways (thick polylines), then walks. Hidden at countryside zoom.
        foreach (var d in map.LodSimple ? Enumerable.Empty<Driveway>() : map.Driveways)
        {
            string color = d.Surface == "concrete" ? "#dcd9d2" : "#d6c9a3";
            sb.AppendLine(Polyline(Map, d.Points, color, MathF.Max(0.6f, d.WidthFeet * scale)));
        }
        foreach (var d in map.LodSimple ? Enumerable.Empty<Driveway>() : map.Walks)
        {
            sb.AppendLine(Polyline(Map, d.Points, "#e3e0d8", MathF.Max(0.5f, d.WidthFeet * scale)));
        }

        // Houses.
        foreach (var h in map.Houses)
        {
            if (map.LodSimple)
            {
                // Footprint silhouette only — walls/openings/captions vanish at this zoom.
                var sil = new StringBuilder();
                sil.Append("M");
                foreach (var p in h.Plan.Footprint)
                {
                    var m = Map(p);
                    sil.Append(N(m.X)).Append(' ').Append(N(m.Y)).Append('L');
                }
                sil.Length--;
                sil.Append("Z");
                sb.AppendLine($"<path d='{sil}' fill='#4c483f' fill-opacity='0.92'/>");
                continue;
            }
            SvgPx HouseMap(Vec2 p)
            {
                var q = Map(p);
                return q;
            }
            SvgRenderer.AppendHouse(sb, h.Plan, HouseMap, scale, "#f0efe0", h.MirroredZ, quietAnnotations: map.QuietAnnotations);
        }

        // Outbuildings.
        foreach (var o in map.Outbuildings)
        {
            var c = Map(o.Center);
            if (map.LodSimple)
            {
                if (o.Kind == OutbuildingKind.GrainBin)
                {
                    float r2 = MathF.Max(1.6f, o.DiameterFeet * scale / 2f);
                    sb.AppendLine($"<circle cx='{N(c.X)}' cy='{N(c.Y)}' r='{N(r2)}' fill='#3f3b34'/>");
                    continue;
                }
                if (o.Kind == OutbuildingKind.CityPark)
                {
                    var pa = Map(new Vec2(o.Center.X - o.WidthFeet / 2f, o.Center.Z + o.DepthFeet / 2f));
                    sb.AppendLine($"<rect x='{N(pa.X)}' y='{N(pa.Y)}' width='{N(o.WidthFeet * scale)}' height='{N(o.DepthFeet * scale)}' fill='#7d8b5f' fill-opacity='0.8'/>");
                    continue;
                }
                var sa = Map(new Vec2(o.Center.X - o.WidthFeet / 2f, o.Center.Z + o.DepthFeet / 2f));
                sb.AppendLine($"<rect x='{N(sa.X)}' y='{N(sa.Y)}' width='{N(o.WidthFeet * scale)}' height='{N(o.DepthFeet * scale)}' fill='#3f3b34' fill-opacity='0.95'/>");
                continue;
            }
            if (o.Kind == OutbuildingKind.GrainBin)
            {
                float r = o.DiameterFeet * scale / 2f;
                sb.AppendLine($"<circle cx='{N(c.X)}' cy='{N(c.Y)}' r='{N(r)}' fill='#c5c0b4' stroke='#57503f' stroke-width='2'/>");
                sb.AppendLine($"<circle cx='{N(c.X)}' cy='{N(c.Y)}' r='{N(r * 0.18f)}' fill='#57503f'/>");
                sb.AppendLine($"<line class='thin' x1='{N(c.X)}' y1='{N(c.Y)}' x2='{N(c.X)}' y2='{N(c.Y - r)}'/>");
                continue;
            }
            if (o.Kind == OutbuildingKind.PropaneTank)
            {
                var a = Map(new Vec2(o.Center.X - o.WidthFeet / 2f, o.Center.Z + o.DepthFeet / 2f));
                sb.AppendLine($"<rect x='{N(a.X)}' y='{N(a.Y)}' width='{N(o.WidthFeet * scale)}' height='{N(o.DepthFeet * scale)}' rx='{N(o.WidthFeet * scale / 2f)}' fill='#d8d8d0' stroke='#57503f' stroke-width='1.5'/>");
                if (!map.SuppressOutbuildingLabels)
                    sb.AppendLine($"<text class='small' x='{N(c.X)}' y='{N(c.Y + 3)}' text-anchor='middle'>{SvgRenderer.Esc(o.Label)}</text>");
                continue;
            }

            string fill = o.Kind switch
            {
                OutbuildingKind.MachineShed => "#b9b09e",
                OutbuildingKind.PoleBarn => "#c4bcab",
                OutbuildingKind.DetachedGarage => "#b9b09e",
                OutbuildingKind.GambrelBarnExisting => "#a89f8f",
                OutbuildingKind.Storefront => "#b8b0a0",
                OutbuildingKind.GrainElevator => "#8f887a",
                OutbuildingKind.Church => "#d9d4c6",
                OutbuildingKind.School => "#b0806f",
                OutbuildingKind.CityPark => "#8a9a6b",
                _ => "#c4bcab",
            };
            var tl = Map(new Vec2(o.Center.X - o.WidthFeet / 2f, o.Center.Z + o.DepthFeet / 2f));
            float wPx = o.WidthFeet * scale, hPx = o.DepthFeet * scale;
            float fillOpacity = o.Kind == OutbuildingKind.CityPark ? 0.8f : 1f;
            sb.AppendLine($"<rect x='{N(tl.X)}' y='{N(tl.Y)}' width='{N(wPx)}' height='{N(hPx)}' fill='{fill}' fill-opacity='{fillOpacity:0.##}' stroke='#3a332b' stroke-width='2.5'/>");
            if (o.Kind == OutbuildingKind.Church)
            {
                // Top-down cross.
                float cx = tl.X + wPx / 2f, cy = tl.Y + hPx / 2f;
                sb.AppendLine($"<line x1='{N(cx)}' y1='{N(cy - hPx * 0.3f)}' x2='{N(cx)}' y2='{N(cy + hPx * 0.3f)}' stroke='#3a332b' stroke-width='2'/>");
                sb.AppendLine($"<line x1='{N(cx - hPx * 0.18f)}' y1='{N(cy - hPx * 0.1f)}' x2='{N(cx + hPx * 0.18f)}' y2='{N(cy - hPx * 0.1f)}' stroke='#3a332b' stroke-width='2'/>");
            }
            else if (o.Kind != OutbuildingKind.CityPark)
            {
                // Ridge line along the long axis.
                if (wPx >= hPx)
                    sb.AppendLine($"<line class='thin' x1='{N(tl.X + 2)}' y1='{N(tl.Y + hPx / 2f)}' x2='{N(tl.X + wPx - 2)}' y2='{N(tl.Y + hPx / 2f)}' stroke-dasharray='5 4'/>");
                else
                    sb.AppendLine($"<line class='thin' x1='{N(tl.X + wPx / 2f)}' y1='{N(tl.Y + 2)}' x2='{N(tl.X + wPx / 2f)}' y2='{N(tl.Y + hPx - 2)}' stroke-dasharray='5 4'/>");
            }
            bool suppress = map.SuppressOutbuildingLabels
                || (map.QuietAnnotations && (o.Kind == OutbuildingKind.DetachedGarage || o.Kind == OutbuildingKind.Storefront));
            if (!string.IsNullOrEmpty(o.Label) && !suppress)
                sb.AppendLine($"<text class='small' x='{N(c.X)}' y='{N(tl.Y + hPx + 12)}' text-anchor='middle'>{SvgRenderer.Esc(o.Label)}</text>");
        }

        // Labels (hidden at countryside zoom; minor labels suppressed at smalltown zoom).
        bool quiet = map.QuietAnnotations;
        foreach (var l in map.LodSimple ? Enumerable.Empty<MapLabel>() : map.Labels)
        {
            if (quiet && !l.Large) continue;
            var p = Map(l.Position);
            string cls = l.Large ? "roadlabel" : "small";
            string rotate = MathF.Abs(l.RotateDeg) > 0.01f ? $" transform='rotate({N(l.RotateDeg)} {N(p.X)} {N(p.Y)})'" : "";
            sb.AppendLine($"<text class='{cls}' x='{N(p.X)}' y='{N(p.Y)}' text-anchor='{l.Anchor}'{rotate}>{SvgRenderer.Esc(l.Text)}</text>");
        }

        // North arrow.
        if (!bare)
        {
            float naX = widthPx - 34f, naY = 40f;
            sb.AppendLine($"<circle cx='{N(naX)}' cy='{N(naY)}' r='13' fill='none' stroke='#57503f' stroke-width='1.5'/>");
            sb.AppendLine($"<line x1='{N(naX)}' y1='{N(naY + 9)}' x2='{N(naX)}' y2='{N(naY - 9)}' stroke='#57503f' stroke-width='1.5'/>");
            sb.AppendLine($"<path d='M {N(naX)} {N(naY - 10)} L {N(naX - 4)} {N(naY - 2)} L {N(naX + 4)} {N(naY - 2)} Z' fill='#57503f'/>");
            sb.AppendLine($"<text class='small' x='{N(naX)}' y='{N(naY + 24)}' text-anchor='middle'>N</text>");
        }

        // Title block.
        if (!bare)
        {
            int buildings = map.Outbuildings.Count;
            int houses = map.Houses.Count;
            sb.AppendLine($"<text class='title' x='{N(Margin)}' y='26'>{SvgRenderer.Esc(map.Title)}</text>");
            sb.AppendLine($"<text class='small' x='{N(Margin)}' y='45'>seed {map.Seed} · {houses} {(houses == 1 ? "house" : "houses")} · {buildings} outbuilding{(buildings == 1 ? "" : "s")} · drive aisles and walks shown — placement guide, not construction</text>");
            sb.AppendLine($"<text class='small' x='{N(Margin)}' y='59'>grid: section lines 1 mi · front doors open to walks · garage aprons face the lane (gravel rural / concrete town)</text>");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    static string Polyline(Func<Vec2, SvgPx> map, List<Vec2> points, string color, float width)
    {
        var pts = string.Join(" ", points.Select(p =>
        {
            var m = map(p);
            return $"{m.X:0.##},{m.Y:0.##}";
        }));
        return $"<polyline points='{pts}' fill='none' stroke='{color}' stroke-width='{width:0.##}' stroke-linecap='round' stroke-linejoin='round' stroke-opacity='0.95'/>";
    }
}
}
