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
    /// <summary>
    /// How far the building is turned, in degrees. Zero for the usual upright
    /// building; a shop fronting a diagonal street is turned to face it.
    /// </summary>
    public float RotateDeg { get; set; }
}

public sealed class Driveway
{
    public List<Vec2> Points { get; } = new();
    public float WidthFeet { get; set; }
    /// <summary>gravel (rural) | concrete (town)</summary>
    public string Surface { get; set; } = "gravel";
}

/// <summary>A creek / drainage ribbon. Drawn as a wide water-colored polyline under roads and trees.</summary>
public sealed class WaterCourse
{
    public List<Vec2> Points { get; } = new();
    public float WidthFeet { get; set; } = 50f;
}

/// <summary>A railroad line: straight across the landscape, drawn with ballast, rails and ties.</summary>
public sealed class RailLine
{
    public List<Vec2> Points { get; } = new();
}

/// <summary>One marching-squares contour segment. Level is the sampled elevation in feet.</summary>
public readonly struct ContourSegment
{
    public readonly Vec2 A, B;
    public readonly int Level;
    public ContourSegment(Vec2 a, Vec2 b, int level) { A = a; B = b; Level = level; }
}

/// <summary>Cul-de-sac bulb: asphalt turnaround at the clipped end of a street.</summary>
public sealed class RoadBulb
{
    public Vec2 Center { get; set; }
    public float RadiusFeet { get; set; } = 44f;
    /// <summary>Explicit fill; null → asphalt gray.</summary>
    public string? Fill { get; set; }
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
/// <summary>
/// A street that is not axis-aligned: a polyline with a width. Iowa towns have
/// them — a rail-corridor street that follows the old track, a curved residential
/// street — and a rectangle cannot draw them.
/// </summary>
public sealed class RoadPath
{
    public List<Vec2> Points { get; } = new();
    /// <summary>Width of the travelled way, feet.</summary>
    public float WidthFeet { get; set; } = 36f;
    public bool Paved { get; set; } = true;
}

public sealed class MapPlan
{
    public string Title { get; set; } = "";
    public uint Seed { get; set; }
    /// <summary>Optional place name (town / crossroads), used by the world stamper.</summary>
    public string PlaceName { get; set; } = "";
    public float WidthFeet { get; set; }
    public float DepthFeet { get; set; }
    /// <summary>
    /// World-window mode: the plan is exactly 0..Width × 0..Depth; content outside is
    /// clipped by the SVG viewBox and never expands the drawing bounds. This is what
    /// makes adjacent lens windows line up pixel-for-pixel.
    /// </summary>
    public bool ExactBounds { get; set; }

    /// <summary>Road/street bands. Negative Z.</summary>
    public List<MapRect> Roads { get; } = new();
    /// <summary>Streets that are not axis-aligned: a polyline with a width.</summary>
    public List<RoadPath> RoadPaths { get; } = new();
    /// <summary>Gravel alleys behind town lots.</summary>
    public List<MapRect> Alleys { get; } = new();
    /// <summary>Large terrain fills (fields), drawn under everything else. Parallel with FieldFills.</summary>
    public List<MapRect> Fields { get; } = new();
    public List<string> FieldFills { get; } = new();
    /// <summary>Creek / drainage ribbons, drawn above fields and under roads, trees and buildings.</summary>
    public List<WaterCourse> Waters { get; } = new();
    /// <summary>Railroad lines, drawn above water and under roads/trees.</summary>
    public List<RailLine> Rails { get; } = new();
    /// <summary>Low-contrast elevation contours, drawn above fields (Great Plains: 10-ft interval).</summary>
    public List<ContourSegment> Contours { get; } = new();
    /// <summary>Cul-de-sac bulbs drawn on top of the road surface.</summary>
    public List<RoadBulb> Bulbs { get; } = new();
    public bool RoadCenterDashes { get; set; }
    /// <summary>Per-road yellow-centerline flag, parallel to Roads. Falls back to RoadCenterDashes when empty.</summary>
    public List<bool> RoadDashed { get; } = new();
    /// <summary>Per-road paved flag, parallel to Roads. Empty → derive from RoadSurface.</summary>
    public List<bool> RoadPaved { get; } = new();
    /// <summary>"asphalt" | "gravel" | "" (derive from dash flags, back-compat).</summary>
    public string RoadSurface { get; set; } = "";
    /// <summary>Level-of-detail: houses/outbuildings silhouette-only, no captions (countryside zoom).</summary>
    public bool LodSimple { get; set; }
    /// <summary>Suppress in-plan captions (GARAGE DOOR/PORCH/etc.). Generators set this; NOT derived from scale anymore.</summary>
    public bool QuietAnnotations { get; set; }
    /// <summary>Hide per-building labels (MACHINE SHED, GAMBREL BARN, ...) while keeping geometry.
    /// World windows need this: at nine miles a farm label is a dozen pixels of noise.</summary>
    public bool SuppressOutbuildingLabels { get; set; }
    /// <summary>
    /// Roads that are allowed to end at the edge of the plat. A street that runs out
    /// of town into the fields is a real thing (Iowa plats have them), and it is not
    /// a dangling end — it leaves town.
    /// </summary>
    public List<MapRect> RoadMayEndFree { get; } = new();
    /// <summary>Planted medians: the green strip down the middle of a boulevard street.</summary>
    public List<MapRect> Medians { get; } = new();
    /// <summary>
    /// The branch spur's right-of-way, in local coordinates. Zero when the town has
    /// no spur. The world reads this to draw the track where the town actually put
    /// it, rather than guessing.
    /// </summary>
    public float RailTrackLocalX { get; set; }
    /// <summary>Local z of the spur's near end (the elevator) and its far end.</summary>
    public float RailTrackLocalZ0 { get; set; }
    public float RailTrackLocalZ1 { get; set; }

    /// <summary>
    /// Bounds of the town's perimeter street loop, in local coordinates. County roads
    /// stop at this rectangle so they meet a town street instead of dangling.
    /// </summary>
    public float LoopMinX { get; set; }
    public float LoopMinZ { get; set; }
    public float LoopMaxX { get; set; }
    public float LoopMaxZ { get; set; }
    /// <summary>Bounds of what actually got built (blocks and houses), local coords.</summary>
    public float BuiltMinX { get; set; }
    public float BuiltMinZ { get; set; }
    public float BuiltMaxX { get; set; }
    public float BuiltMaxZ { get; set; }
    /// <summary>Dashed lot boundary rectangles (town lots). Rural: the outer parcel line.</summary>
    public List<MapRect> LotLines { get; } = new();    public List<PositionedHouse> Houses { get; } = new();
    public List<Outbuilding> Outbuildings { get; } = new();
    public List<Driveway> Driveways { get; } = new();
    public List<Driveway> Walks { get; } = new();
    /// <summary>Shelterbelt / windbreak tree positions (filled circles at render time).</summary>
    public List<Vec2> Trees { get; } = new();
    public List<MapLabel> Labels { get; } = new();
}
}
