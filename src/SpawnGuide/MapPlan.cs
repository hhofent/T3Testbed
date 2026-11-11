using System;
using System.Collections.Generic;
using System.Linq;

namespace SpawnGuide
{

public readonly struct MapRect
{
    public readonly float X, Z, W, H;
    public MapRect(float x, float z, float w, float h) { X = x; Z = z; W = w; H = h; }
}

public enum OutbuildingKind { MachineShed, PoleBarn, GrainBin, GambrelBarnExisting, DetachedGarage, PropaneTank, Storefront, GrainElevator, Church, School, CityPark }

public sealed class Outbuilding
{
    public OutbuildingKind Kind { get; set; }
    /// <summary>Center, feet (map coordinates).</summary>
    public Vec2 Center { get; set; }
    /// <summary>For rectangular buildings: footprint size. For bins: ignored (use DiameterFeet).</summary>
    public float WidthFeet { get; set; }
    public float DepthFeet { get; set; }
    /// <summary>Grain bins only: diameter, feet.</summary>
    public float DiameterFeet { get; set; }
    public string Label { get; set; } = "";
}

public sealed class Driveway
{
    public List<Vec2> Points { get; } = new();
    public float WidthFeet { get; set; }
    /// <summary>gravel (rural) | concrete (town)</summary>
    public string Surface { get; set; } = "gravel";
}

/// <summary>Cul-de-sac bulb: asphalt turnaround at the clipped end of a street.</summary>
public sealed class RoadBulb
{
    public Vec2 Center { get; set; }
    public float RadiusFeet { get; set; } = 44f;
}

public sealed class PositionedHouse
{
    public HousePlan Plan { get; set; } = null!;
    /// <summary>True for houses flipped to face a street from the far side (door swing etc.).</summary>
    public bool MirroredZ { get; set; }
}

public sealed class MapLabel
{
    public Vec2 Position { get; set; }
    public string Text { get; set; } = "";
    /// <summary>true → .txt-sized; false → .small-sized.</summary>
    public bool Large { get; set; }
    public float RotateDeg { get; set; }
    public string Anchor { get; set; } = "middle";
}

/// <summary>
/// A placed site map: county road or town street, lot boundaries, driveways, walks,
/// one or more positioned houses, outbuildings and shelterbelt trees.
/// Coordinates: X east, Z north-into-the-lot; the street edge is at z = 0.
/// </summary>
public sealed class MapPlan
{
    public string Title { get; set; } = "";
    public uint Seed { get; set; }
    public float WidthFeet { get; set; }
    public float DepthFeet { get; set; }

    /// <summary>Road/street bands. Negative Z.</summary>
    public List<MapRect> Roads { get; } = new();
    /// <summary>Gravel alleys behind town lots.</summary>
    public List<MapRect> Alleys { get; } = new();
    /// <summary>Large terrain fills (fields), drawn under everything else. Parallel with FieldFills.</summary>
    public List<MapRect> Fields { get; } = new();
    public List<string> FieldFills { get; } = new();
    /// <summary>Cul-de-sac bulbs drawn on top of the road surface.</summary>
    public List<RoadBulb> Bulbs { get; } = new();
    public bool RoadCenterDashes { get; set; }
    /// <summary>Per-road yellow-centerline flag, parallel to Roads. Falls back to RoadCenterDashes when empty.</summary>
    public List<bool> RoadDashed { get; } = new();
    /// <summary>"asphalt" | "gravel" | "" (derive from dash flags, back-compat).</summary>
    public string RoadSurface { get; set; } = "";
    /// <summary>Level-of-detail: houses/outbuildings silhouette-only, no captions (countryside zoom).</summary>
    public bool LodSimple { get; set; }
    /// <summary>Suppress in-plan captions (GARAGE DOOR/PORCH/etc.). Generators set this; NOT derived from scale anymore.</summary>
    public bool QuietAnnotations { get; set; }
    /// <summary>Dashed lot boundary rectangles (town lots). Rural: the outer parcel line.</summary>
    public List<MapRect> LotLines { get; } = new();
    public List<PositionedHouse> Houses { get; } = new();
    public List<Outbuilding> Outbuildings { get; } = new();
    public List<Driveway> Driveways { get; } = new();
    public List<Driveway> Walks { get; } = new();
    /// <summary>Shelterbelt / windbreak tree positions (filled circles at render time).</summary>
    public List<Vec2> Trees { get; } = new();
    public List<MapLabel> Labels { get; } = new();
}
}
