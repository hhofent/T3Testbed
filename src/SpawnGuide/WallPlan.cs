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
            copy.Annotations.Add(new PlanAnnotation { Position = M(ann.Position), Text = ann.Text, DashedHalfExtents = ann.DashedHalfExtents });
        return copy;
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
