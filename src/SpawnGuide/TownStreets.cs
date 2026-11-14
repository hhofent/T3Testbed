using System;
using System.Collections.Generic;

namespace SpawnGuide
{

/// <summary>
/// A street that is not on the town's grid: the diagonal rail-corridor street and
/// the curved residential street. Iowa towns have them — one town's main drag runs
/// along the old rail line, another's Railroad St curves through the south third —
/// and a rectangle cannot draw them.
///
/// The street is laid out before the blocks are filled, so the blocks leave room
/// for it and nothing is built on top of it.
/// </summary>
public sealed class FreeStreet
{
    /// <summary>The centre line of the travelled way.</summary>
    public RoadPath Path = new();
    /// <summary>Half the paved width, feet.</summary>
    public float StreetHalf = 15f;
    /// <summary>
    /// Half the reserved width: the street plus the frontage that faces it. No
    /// grid lot may be placed inside this distance of the centre line.
    /// </summary>
    public float HalfWidth = 60f;
    /// <summary>True when houses front the street; false for the industrial corridor.</summary>
    public bool Residential = true;
    /// <summary>The name the street carries on the map.</summary>
    public string Name = "";
}

/// <summary>
/// The geometry of the streets that do not follow the grid: how far a point is
/// from a polyline, and how to lay out an arc.
/// </summary>
public static class StreetGeom
{
    /// <summary>Distance from a point to a line segment.</summary>
    public static float SegDist(Vec2 p, Vec2 a, Vec2 b)
    {
        float dx = b.X - a.X, dz = b.Z - a.Z;
        float len2 = dx * dx + dz * dz;
        if (len2 < 1e-6f) return (p - a).Length();
        float t = ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / len2;
        t = Math.Clamp(t, 0f, 1f);
        float cx = a.X + dx * t, cz = a.Z + dz * t;
        return MathF.Sqrt((p.X - cx) * (p.X - cx) + (p.Z - cz) * (p.Z - cz));
    }

    /// <summary>Distance from a point to the nearest point of a polyline.</summary>
    public static float PathDist(IReadOnlyList<Vec2> pts, float x, float z)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < pts.Count; i++)
            best = MathF.Min(best, SegDist(new Vec2(x, z), pts[i], pts[i + 1]));
        if (pts.Count == 1) best = (new Vec2(x, z) - pts[0]).Length();
        return best;
    }

    /// <summary>
    /// True when an axis-aligned rectangle comes within <paramref name="pad"/> feet of
    /// the street's centre line — that is, the street would run through it.
    /// </summary>
    public static bool PathHitsRect(FreeStreet fs, float x0, float z0, float x1, float z1, float pad)
    {
        // The street's own vertices, if they fall inside the rectangle.
        foreach (var p in fs.Path.Points)
            if (p.X >= x0 - pad && p.X <= x1 + pad && p.Z >= z0 - pad && p.Z <= z1 + pad)
                return true;
        // The rectangle's corners against the street's centre line.
        var corners = new[]
        {
            new Vec2(x0, z0), new Vec2(x1, z0), new Vec2(x1, z1), new Vec2(x0, z1),
        };
        foreach (var c in corners)
            if (PathDist(fs.Path.Points, c.X, c.Z) < pad) return true;
        // Segments that pass clean through the rectangle cross its four edges.
        var pts = fs.Path.Points;
        float rx0 = x0 - pad, rx1 = x1 + pad, rz0 = z0 - pad, rz1 = z1 + pad;
        var rectCorners = new[] { new Vec2(rx0, rz0), new Vec2(rx1, rz0), new Vec2(rx1, rz1), new Vec2(rx0, rz1) };
        for (int i = 0; i + 1 < pts.Count; i++)
            for (int c = 0; c < 4; c++)
                if (SegsCross(pts[i], pts[i + 1], rectCorners[c], rectCorners[(c + 1) % 4])) return true;
        return false;
    }

    /// <summary>True when the two segments cross (proper intersection or touch).</summary>
    static bool SegsCross(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        float Cross(Vec2 o, Vec2 p, Vec2 q) => (p.X - o.X) * (q.Z - o.Z) - (p.Z - o.Z) * (q.X - o.X);
        float d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
        if (((d1 > 0f) != (d2 > 0f)) && ((d3 > 0f) != (d4 > 0f))) return true;
        return false;
    }

    /// <summary>Points along a circular arc, from one bearing to another.</summary>
    public static List<Vec2> Arc(Vec2 centre, float radius, float fromDeg, float toDeg, int steps)
    {
        var pts = new List<Vec2>();
        for (int i = 0; i <= steps; i++)
        {
            float a = (fromDeg + (toDeg - fromDeg) * i / steps) * MathF.PI / 180f;
            pts.Add(new Vec2(centre.X + MathF.Cos(a) * radius, centre.Z + MathF.Sin(a) * radius));
        }
        return pts;
    }

    /// <summary>
    /// The point on a street's centre line at a given distance along it, with the
    /// direction of travel there.
    /// </summary>
    public static (Vec2 Point, Vec2 Tangent) AlongPath(IReadOnlyList<Vec2> pts, float distance)
    {
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            Vec2 a = pts[i], b = pts[i + 1];
            float len = (b - a).Length();
            if (distance <= len || i + 2 == pts.Count)
            {
                Vec2 dir = (b - a).Normalized();
                return (a + dir * MathF.Min(distance, len), dir);
            }
            distance -= len;
        }
        var last = pts[^1];
        return (last, new Vec2(1f, 0f));
    }

    /// <summary>The length of a polyline.</summary>
    public static float PathLength(IReadOnlyList<Vec2> pts)
    {
        float total = 0f;
        for (int i = 0; i + 1 < pts.Count; i++) total += (pts[i + 1] - pts[i]).Length();
        return total;
    }
}

}
