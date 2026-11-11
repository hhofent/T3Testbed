using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpawnGuide
{

/// <summary>Squished 1-char-per-foot top-down map for the console.</summary>
public static class AsciiRenderer
{
    public static string Render(HousePlan plan)
    {
        const int Margin = 2;
        int w = (int)MathF.Ceiling(plan.WidthFeet) + 2 * Margin + 1;
        int h = (int)MathF.Ceiling(plan.DepthFeet) + 2 * Margin + 1;
        var grid = new char[h, w];
        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
                grid[r, c] = ' ';

        int Col(float x) => Margin + (int)MathF.Floor(x + 0.5f);
        int Row(float z) => Margin + (int)MathF.Floor(z + 0.5f); // front (z=0) at top of the printout

        // Floor fill via even-odd ray cast per cell center.
        float maxX = plan.Footprint.Select(p => p.X).Max();
        float maxZ = plan.Footprint.Select(p => p.Z).Max();
        for (float z = 0.5f; z < maxZ; z += 1f)
        for (float x = 0.5f; x < maxX; x += 1f)
        {
            if (Inside(plan.Footprint, x, z)) grid[Row(z), Col(x)] = '.';
        }

        // Walls.
        foreach (var wall in plan.Walls)
        {
            float len = wall.LengthFeet;
            var dir = (wall.End - wall.Start).Normalized();
            for (float t = 0; t <= len; t += 0.25f)
            {
                var p = wall.Start + dir * t;
                int r = Row(p.Z), c = Col(p.X);
                if (r >= 0 && r < h && c >= 0 && c < w) grid[r, c] = '#';
            }
            // Openings: carve gaps, tag with letters.
            foreach (var op in wall.Openings)
            {
                var center = wall.Start + dir * op.OffsetFeet;
                var half = dir * (op.WidthFeet / 2f);
                var o0 = center - half;
                var o1 = center + half;
                var od = (o1 - o0);
                float olen = od.Length();
                for (float t = 0; t <= olen; t += 0.2f)
                {
                    var p = o0 + od.Normalized() * t;
                    int r = Row(p.Z), c = Col(p.X);
                    if (r >= 0 && r < h && c >= 0 && c < w) grid[r, c] = ' ';
                }
                int cr = Row(center.Z), cc = Col(center.X);
                if (cr >= 0 && cr < h && cc >= 0 && cc < w)
                    grid[cr, cc] = op.Kind switch { OpeningKind.Door => 'D', OpeningKind.Window => 'W', OpeningKind.GarageDoor => 'G', _ => '?' };
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{plan.Archetype.ToUpperInvariant()}  seed {plan.Seed}  {plan.WidthFeet:0.#}' x {plan.DepthFeet:0.#}'  (front at top)");
        for (int r = 0; r < h; r++)
        {
            for (int c = 0; c < w; c++) sb.Append(grid[r, c]);
            sb.AppendLine();
        }
        sb.AppendLine("# wall   . floor   D door   W window   G garage door");
        return sb.ToString();
    }

    static bool Inside(IReadOnlyList<Vec2> poly, float x, float z)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var pi = poly[i]; var pj = poly[j];
            if (pi.Z > z != pj.Z > z && x < (pj.X - pi.X) * (z - pi.Z) / (pj.Z - pi.Z) + pi.X)
                inside = !inside;
        }
        return inside;
    }
}
}
