using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;

namespace SpawnGuide
{

/// <summary>Screen-space point in an SVG. Library-public so map renderers can reuse it.</summary>
public readonly struct SvgPx
{
    public readonly float X, Y;
    public SvgPx(float x, float y) { X = x; Y = y; }
    public SvgPx Offset(float dx, float dy) => new(X + dx, Y + dy);
}

/// <summary>Top-down 2D plan SVG. Street/front is drawn at the bottom.</summary>
public static class SvgRenderer
{
    public static string Format(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Draws the floor fill, dashed annotations, walls with opening gaps and annotation
    /// labels for a house plan, using the given coordinate map and scale. Everything the
    /// map renderer needs to embed a house in a larger scene.
    /// </summary>
    public static void AppendHouse(StringBuilder sb, HousePlan plan, Func<Vec2, SvgPx> map, float scale, string gapColor, bool mirroredZ = false, bool quietAnnotations = false)
    {
        string N(float v) => Format(v);

        // Floor fill (even-odd handles the concave L).
        var floor = new StringBuilder();
        floor.Append("M");
        foreach (var p in plan.Footprint)
        {
            var m = map(p);
            floor.Append(N(m.X)).Append(' ').Append(N(m.Y)).Append('L');
        }
        floor.Length--;
        floor.Append("Z");
        sb.AppendLine($"<path class='floor' fill-rule='evenodd' d='{floor}'/>");

        // Dashed annotations (porch) under walls.
        foreach (var ann in plan.Annotations.Where(a => a.DashedHalfExtents.HasValue))
        {
            var c = map(ann.Position);
            var h = ann.DashedHalfExtents!.Value;
            sb.AppendLine($"<rect class='dash' x='{N(c.X - h.X * scale)}' y='{N(c.Y - h.Z * scale)}' width='{N(2 * h.X * scale)}' height='{N(2 * h.Z * scale)}'/>");
            if (!quietAnnotations)
                sb.AppendLine($"<text class='small' x='{N(c.X)}' y='{N(c.Y + 3)}' text-anchor='middle'>{Esc(ann.Text)}</text>");
        }

        // Walls with opening gaps.
        foreach (var wall in plan.Walls)
        {
            var a = map(wall.Start);
            var b = map(wall.End);
            float thick = MathF.Max(wall.ThicknessFeet * scale, scale < 0.15f ? 0.7f : 2.2f);
            sb.AppendLine($"<line class='wall' x1='{N(a.X)}' y1='{N(a.Y)}' x2='{N(b.X)}' y2='{N(b.Y)}' stroke-width='{N(thick)}'/>");

            var dir = (wall.End - wall.Start).Normalized();
            foreach (var op in wall.Openings)
            {
                var center = wall.Start + dir * op.OffsetFeet;
                var half = dir * (op.WidthFeet / 2f);
                var p0 = map(center - half);
                var p1 = map(center + half);
                sb.AppendLine($"<line x1='{N(p0.X)}' y1='{N(p0.Y)}' x2='{N(p1.X)}' y2='{N(p1.Y)}' stroke='{gapColor}' stroke-width='{N(thick + 3.5f)}'/>");
                var c = map(center);

                if (op.Kind == OpeningKind.Window)
                {
                    // Classic plan symbol: three parallel lines across the wall.
                    // Screen-space perpendicular = (dir.Z, dir.X), scaled to the wall thickness.
                    var nqx = dir.Z * thick * 0.32f;
                    var nqy = dir.X * thick * 0.32f;
                    foreach (var f in new[] { -1f, 0f, 1f })
                    {
                        sb.AppendLine($"<line class='thin' x1='{N(p0.X + nqx * f)}' y1='{N(p0.Y + nqy * f)}' x2='{N(p1.X + nqx * f)}' y2='{N(p1.Y + nqy * f)}'/>");
                    }
                }
                else if (op.Kind == OpeningKind.Door)
                {
                    // Leaf + quarter-arc swing, leaf opening into the room (up-screen).
                    float leaf = op.WidthFeet * scale;
                    float swing = mirroredZ ? leaf : -leaf;
                    var leafEnd = p0.Offset(0, swing);
                    sb.AppendLine($"<line class='thin' x1='{N(p0.X)}' y1='{N(p0.Y)}' x2='{N(leafEnd.X)}' y2='{N(leafEnd.Y)}'/>");
                    sb.AppendLine($"<path class='dash' d='M {N(p1.X)} {N(p1.Y)} A {N(leaf)} {N(leaf)} 0 0 {(mirroredZ ? "0" : "1")} {N(leafEnd.X)} {N(leafEnd.Y)}'/>");
                }
                else if (op.Kind == OpeningKind.GarageDoor)
                {
                    if (quietAnnotations) continue;
                    bool verticalWall = MathF.Abs(dir.Z) > MathF.Abs(dir.X);
                    if (verticalWall)
                        sb.AppendLine($"<text class='small' x='{N(c.X)}' y='{N(c.Y)}' text-anchor='middle' transform='rotate(-90 {N(c.X)} {N(c.Y)})'>GARAGE DOOR {op.WidthFeet:0.#} FT</text>");
                    else
                        sb.AppendLine($"<text class='small' x='{N(c.X)}' y='{N(c.Y - 6)}' text-anchor='middle'>GARAGE DOOR {op.WidthFeet:0.#} FT</text>");
                }
            }
        }

        // Annotation labels (garage text etc.)
        foreach (var ann in plan.Annotations.Where(a => !a.DashedHalfExtents.HasValue))
        {
            if (quietAnnotations) break;
            var c = map(ann.Position);
            sb.AppendLine($"<text class='txt' x='{N(c.X)}' y='{N(c.Y)}'>{Esc(ann.Text)}</text>");
        }
    }

    public static string Render(HousePlan plan)
    {
        const float Margin = 26f;
        const float TitleH = 64f, StreetH = 40f;
        float planW = plan.WidthFeet, planD = plan.DepthFeet;
        float minX = plan.MinX;

        // Reserve bottom space for a porch hanging off the front (street) side.
        float porchDepth = plan.Annotations
            .Where(a => a.DashedHalfExtents.HasValue)
            .Select(a => 2f * a.DashedHalfExtents!.Value.Z)
            .DefaultIfEmpty(0f).Max();
        float planH = planD + porchDepth + 1.5f;

        float widthPx = MathF.Max(560, planW * 13f + 2 * Margin + 40f);
        float heightPx = MathF.Max(480, planH * 13f + TitleH + StreetH);
        float laneH = heightPx - TitleH - StreetH;
        float scale = MathF.Min((widthPx - 2 * Margin) / planW, laneH / planH);

        float ox = (widthPx - planW * scale) / 2f - minX * scale;
        float oy = TitleH + laneH - (porchDepth + 1.5f) * scale;

        SvgPx Map(Vec2 p) => new(ox + p.X * scale, oy - p.Z * scale);
        string N(float v) => Format(v);

        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{N(widthPx)}' height='{N(heightPx)}' viewBox='0 0 {N(widthPx)} {N(heightPx)}' font-family='Georgia, serif'>");
        sb.AppendLine("<style>");
        sb.AppendLine(".wall{stroke:#3a332b;fill:none}");
        sb.AppendLine(".floor{fill:#e7dfd0}");
        sb.AppendLine(".thin{stroke:#3a332b;stroke-width:1.4;fill:none}");
        sb.AppendLine(".dash{stroke:#8a7f70;stroke-width:1.4;fill:none;stroke-dasharray:6 5}");
        sb.AppendLine(".txt{fill:#3a332b;font-size:12px;text-anchor:middle}");
        sb.AppendLine(".small{fill:#6b6154;font-size:10px}");
        sb.AppendLine(".title{fill:#3a332b;font-size:17px;font-weight:bold}");
        sb.AppendLine("</style>");

        sb.AppendLine($"<rect x='0' y='0' width='{N(widthPx)}' height='{N(heightPx)}' fill='#faf7ef'/>");

        AppendHouse(sb, plan, Map, scale, "#faf7ef");

        // Overall width dimension, street side (true footprint bounds).
        float maxX = plan.Footprint.Select(p => p.X).Max();
        var sw = Map(new Vec2(plan.MinX, 0));
        var ne = Map(new Vec2(maxX, 0));
        float dimY = oy + (porchDepth + 1.3f) * scale + 2f;
        sb.AppendLine($"<line class='thin' x1='{N(sw.X)}' y1='{N(dimY)}' x2='{N(ne.X)}' y2='{N(dimY)}'/>");
        sb.AppendLine($"<text class='small' x='{N((sw.X + ne.X) / 2)}' y='{N(dimY + 12)}' text-anchor='middle'>{planW:0.#} FT</text>");

        // Depth dimension, left side.
        float dimX = ox - plan.MinX * scale - 14f;
        float topY = oy - planD * scale;
        sb.AppendLine($"<line class='thin' x1='{N(dimX)}' y1='{N(topY)}' x2='{N(dimX)}' y2='{N(oy)}'/>");
        float midY = (topY + oy) / 2f;
        sb.AppendLine($"<text class='small' x='{N(dimX - 4)}' y='{N(midY)}' text-anchor='middle' transform='rotate(-90 {N(dimX - 4)} {N(midY)})'>{planD:0.#} FT</text>");

        // Title block.
        sb.AppendLine($"<text class='title' x='{N(Margin)}' y='26'>SPAWNER GUIDE — {Esc(plan.Archetype.ToUpperInvariant())}</text>");
        sb.AppendLine($"<text class='small' x='{N(Margin)}' y='43'>seed {plan.Seed} · {plan.Storeys} {(plan.Storeys == 1 ? "story" : "stories")} · {plan.Walls.Count} walls · {plan.OpeningCount} openings — walls spawn already fastened; placement only</text>");
        sb.AppendLine($"<text class='small' x='{N(Margin)}' y='57'>framed per Builder-Guy (2×6 plates + precut studs, {97.125f:0.###} in = 8′-1⅛″ per story) · roof: {Esc(plan.RoofNote)} · siding: {Esc(plan.SidingNote)}</text>");

        // Street marker along the bottom.
        sb.AppendLine($"<text class='small' x='{N(widthPx / 2f)}' y='{N(heightPx - 10)}' text-anchor='middle'>— STREET (front faces south) —</text>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    public static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
}
