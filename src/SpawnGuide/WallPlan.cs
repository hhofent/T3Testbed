using System;
using System.Collections.Generic;
using System.Linq;
namespace SpawnGuide
{

public enum OpeningKind { Door, Window, GarageDoor }

public enum EdgeTag { MainFront, GarageFront, GarageSide, GarageBack, Left, Right, Back }

/// <summary>
/// An opening in an already-fastened wall. The guide shows where to place it;
/// it is built per Builder-Guy framing (header, jacks, sill) before spawn.
/// </summary>
public sealed class WallOpening
{
    /// <summary>Distance from the wall's start point to the opening's center, feet.</summary>
    public float OffsetFeet { get; set; }
    public float WidthFeet { get; set; }
    public OpeningKind Kind { get; set; }
}

public sealed class WallSegment
{
    public Vec2 Start { get; set; }
    public Vec2 End { get; set; }
    public EdgeTag Tag { get; set; }
    public bool IsExterior { get; set; } = true;
    public float HeightFeet { get; set; }
    public float ThicknessFeet { get; set; }
    public List<WallOpening> Openings { get; } = new();

    public float LengthFeet => (End - Start).Length();
}

/// <summary>A label drawn on the plan (porch zones, material notes, etc.).</summary>
public sealed class PlanAnnotation
{
    public Vec2 Position { get; set; }
    public string Text { get; set; } = "";
    /// <summary>Optional dashed rectangle (porch, garage apron). Half-extents grow from Position.</summary>
    public Vec2? DashedHalfExtents { get; set; }
    /// <summary>
    /// How far the dashed rectangle is turned, in degrees. Zero for an upright plan;
    /// a plan that has been rotated to face a diagonal street carries its angle here.
    /// </summary>
    public float RotateDeg { get; set; }
}

public sealed class HousePlan
{
    public string Archetype { get; set; } = "";
    public uint Seed { get; set; }
    public int Storeys { get; set; } = 1;
    public string RoofNote { get; set; } = "";
    public string SidingNote { get; set; } = "";
    public List<Vec2> Footprint { get; } = new();
    public List<WallSegment> Walls { get; } = new();
    public List<PlanAnnotation> Annotations { get; } = new();

    public float WidthFeet => Footprint.Select(p => p.X).Max() - Footprint.Select(p => p.X).Min();
    public float DepthFeet => Footprint.Select(p => p.Z).Max() - Footprint.Select(p => p.Z).Min();
    public float MinX => Footprint.Select(p => p.X).Min();
    public float MinZ => Footprint.Select(p => p.Z).Min();

    public int OpeningCount => Walls.Sum(w => w.Openings.Count);
}

public static class HousePlanExtensions
{
    /// <summary>Mirror the plan across z = 0 (north-facing copy for the far side of a street).</summary>
    public static HousePlan FlipZ(this HousePlan plan)
    {
        Vec2 M(Vec2 v) => new Vec2(v.X, -v.Z);
        var copy = new HousePlan
        {
            Archetype = plan.Archetype,
            Seed = plan.Seed,
            Storeys = plan.Storeys,
            RoofNote = plan.RoofNote,
            SidingNote = plan.SidingNote,
        };
        foreach (var wall in plan.Walls)
        {
            var w = new WallSegment
            {
                Start = M(wall.Start),
                End = M(wall.End),
                Tag = wall.Tag,
                IsExterior = wall.IsExterior,
                HeightFeet = wall.HeightFeet,
                ThicknessFeet = wall.ThicknessFeet,
            };
            w.Openings.AddRange(wall.Openings);
            copy.Walls.Add(w);
        }
        foreach (var p in plan.Footprint) copy.Footprint.Add(M(p));
        foreach (var ann in plan.Annotations)
            copy.Annotations.Add(new PlanAnnotation { Position = M(ann.Position), Text = ann.Text, DashedHalfExtents = ann.DashedHalfExtents, RotateDeg = -ann.RotateDeg });
        return copy;
    }

    /// <summary>Rotate 90° clockwise in the map plane: (x, z) → (z, −x).
    /// A south-facing plan (front at −z) becomes a west-facing plan (front at −x).</summary>
    public static HousePlan Rotate90CW(this HousePlan plan) => Rotate90(plan, cw: true);

    /// <summary>Rotate 90° counter-clockwise in the map plane: (x, z) → (−z, x).
    /// A south-facing plan (front at −z) becomes an east-facing plan (front at +x).</summary>
    public static HousePlan Rotate90CCW(this HousePlan plan) => Rotate90(plan, cw: false);

    static HousePlan Rotate90(HousePlan plan, bool cw)
    {
        Vec2 M(Vec2 v) => cw ? new Vec2(v.Z, -v.X) : new Vec2(-v.Z, v.X);
        var copy = new HousePlan
        {
            Archetype = plan.Archetype,
            Seed = plan.Seed,
            Storeys = plan.Storeys,
            RoofNote = plan.RoofNote,
            SidingNote = plan.SidingNote,
        };
        foreach (var wall in plan.Walls)
        {
            var w = new WallSegment
            {
                Start = M(wall.Start),
                End = M(wall.End),
                Tag = wall.Tag,
                IsExterior = wall.IsExterior,
                HeightFeet = wall.HeightFeet,
                ThicknessFeet = wall.ThicknessFeet,
            };
            w.Openings.AddRange(wall.Openings);
            copy.Walls.Add(w);
        }
        foreach (var p in plan.Footprint) copy.Footprint.Add(M(p));
        foreach (var ann in plan.Annotations)
            copy.Annotations.Add(new PlanAnnotation
            {
                Position = M(ann.Position),
                Text = ann.Text,
                DashedHalfExtents = ann.DashedHalfExtents.HasValue
                    ? new Vec2(ann.DashedHalfExtents.Value.Z, ann.DashedHalfExtents.Value.X)
                    : null,
            });
        return copy;
    }

    /// <summary>
    /// Rotate the plan about a pivot, by an angle in degrees. A house that fronts a
    /// diagonal or curved street is turned to face it; the walls keep their tags and
    /// their openings keep their offsets, so the plan is still the same house.
    /// </summary>
    public static HousePlan Rotate(this HousePlan plan, float degrees, Vec2 pivot)
    {
        float r = degrees * MathF.PI / 180f;
        float c = MathF.Cos(r), s = MathF.Sin(r);
        Vec2 M(Vec2 v)
        {
            float x = v.X - pivot.X, z = v.Z - pivot.Z;
            return new Vec2(pivot.X + x * c - z * s, pivot.Z + x * s + z * c);
        }
        var copy = new HousePlan
        {
            Archetype = plan.Archetype,
            Seed = plan.Seed,
            Storeys = plan.Storeys,
            RoofNote = plan.RoofNote,
            SidingNote = plan.SidingNote,
        };
        foreach (var wall in plan.Walls)
        {
            var w = new WallSegment
            {
                Start = M(wall.Start),
                End = M(wall.End),
                Tag = wall.Tag,
                IsExterior = wall.IsExterior,
                HeightFeet = wall.HeightFeet,
                ThicknessFeet = wall.ThicknessFeet,
            };
            w.Openings.AddRange(wall.Openings);
            copy.Walls.Add(w);
        }
        foreach (var p in plan.Footprint) copy.Footprint.Add(M(p));
        foreach (var ann in plan.Annotations)
            copy.Annotations.Add(new PlanAnnotation
            {
                Position = M(ann.Position),
                Text = ann.Text,
                DashedHalfExtents = ann.DashedHalfExtents,
                RotateDeg = ann.RotateDeg + degrees,
            });
        return copy;
    }

    /// <summary>
    /// The point on the front wall that the front door opens from — the middle of
    /// the main front edge. Used to sit a turned house against a street.
    /// </summary>
    public static Vec2 FrontMidpoint(this HousePlan plan)
    {
        var front = plan.Walls.FirstOrDefault(w => w.Tag == EdgeTag.MainFront);
        if (front == null) return new Vec2(plan.MinX + plan.WidthFeet / 2f, plan.MinZ);
        return new Vec2((front.Start.X + front.End.X) / 2f, (front.Start.Z + front.End.Z) / 2f);
    }

    /// <summary>Deep-enough copy of a plan shifted by (dx, dz) feet for placement on a map.</summary>
    public static HousePlan OffsetCopy(this HousePlan plan, float dx, float dz)
    {
        var shift = new Vec2(dx, dz);
        var copy = new HousePlan
        {
            Archetype = plan.Archetype,
            Seed = plan.Seed,
            Storeys = plan.Storeys,
            RoofNote = plan.RoofNote,
            SidingNote = plan.SidingNote,
        };
        foreach (var wall in plan.Walls)
        {
            var w = new WallSegment
            {
                Start = wall.Start + shift,
                End = wall.End + shift,
                Tag = wall.Tag,
                IsExterior = wall.IsExterior,
                HeightFeet = wall.HeightFeet,
                ThicknessFeet = wall.ThicknessFeet,
            };
            w.Openings.AddRange(wall.Openings); // openings never mutated after generation
            copy.Walls.Add(w);
        }
        foreach (var p in plan.Footprint) copy.Footprint.Add(p + shift);
        foreach (var ann in plan.Annotations)
            copy.Annotations.Add(new PlanAnnotation { Position = ann.Position + shift, Text = ann.Text, DashedHalfExtents = ann.DashedHalfExtents });
        return copy;
    }
}

/// <summary>
/// True dimensions carried from Builder-Guy (see Builder-Guy CONTEXT.md / Stock.cs).
/// Walls spawn already fastened; nothing here models the raise or anchoring steps.
/// </summary>
public static class BuilderGuyDims
{
    public const float WallHeightFeet = 97.125f / 12f;   // one 8-ft frame: plate + 92.625 precut + double top plate
    public const float ExteriorThicknessFeet = 6.5f / 12f; // 2x6 (5.5 in) + 2x 1/2 in drywall
    public const float InteriorThicknessFeet = 4.5f / 12f; // 2x4 (3.5 in) + 2x 1/2 in drywall
    public const string PlateStock = "TenFootPlateTwoBySix";   // one bottom, double top
    public const string StudStock = "PrecutStudTwoBySix";      // 92.625 in
}
}
