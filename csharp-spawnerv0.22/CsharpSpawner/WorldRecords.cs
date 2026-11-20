using System.Collections.Generic;
using System.Linq;

namespace CsharpSpawner;

/// <summary>World-space coordinates in feet: X east, Z north, Y elevation.</summary>
public readonly record struct WorldPosition(double X, double Y, double Z);

public sealed record WorldRoad(string Id, IReadOnlyList<WorldPosition> Centerline, double WidthFeet, string Surface);

public sealed record WorldBuilding(string Id, IReadOnlyList<WorldPosition> Footprint, double FacingRadians, string Archetype);

public sealed record WorldPerson(string SettlerId, string Name, string LotId, WorldPosition Position);

/// <summary>Typed world-space feature used by library clients; all linear units are feet.</summary>
public sealed record WorldFeature(string Id, string Kind, string Name, string Surface, string Carrier,
    IReadOnlyList<WorldPosition> Geometry, double WidthFeet, double HeightFeet, double ElevationFeet,
    int Population, int StreamOrder, bool Closed, bool IsMajor, string RailPassThrough = "", int YearBuilt = 0)
{
    /// <summary>Ground elevation in feet; legacy ElevationFeet remains available during migration.</summary>
    public double GroundElevationFeet { get; init; }
    /// <summary>Vertical structure height in feet, independent of footprint depth.</summary>
    public double VerticalHeightFeet { get; init; }
    public double FootprintWidthFeet { get; init; }
    public double FootprintDepthFeet { get; init; }
    public double FacingRadians { get; init; }
    public string Archetype { get; init; } = "";
    public bool? IsOccupied { get; init; }
    public string ParentId { get; init; } = "";
}

public sealed record WorldScene(int ChunkX, int ChunkNorth, IReadOnlyList<WorldFeature> Features);
public enum WorldDetail { Far, Mid, Near }
public sealed record WorldQueryResult(IReadOnlyList<WorldFeature> Features, IReadOnlyList<WorldPerson> People,
    ElevationGridData TerrainGrid = null, IReadOnlyList<WorldWallPlan> WallPlans = null);
public sealed record WorldWallOpening(double OffsetFeet,double WidthFeet,string Kind);
public sealed record WorldWall(string Id,string HouseId,WorldPosition Start,WorldPosition End,double HeightFeet,double ThicknessFeet,
    string EdgeTag,bool IsExterior,IReadOnlyList<WorldWallOpening> Openings);
public sealed record WorldWallPlan(string HouseId,string Archetype,uint Seed,int Storeys,string RoofNote,string SidingNote,
    IReadOnlyList<WorldPosition> Footprint,IReadOnlyList<WorldWall> Walls);

/// <summary>Generation configuration passed through queries. Defaults mirror the 0.2 baseline.</summary>
public sealed class WorldGenerationSettings
{
    public int Seed { get; set; } = 33;
    public double SettlementSpacingMiles { get; set; } = 12;
    public double MinimumSettlementSeparationMiles { get; set; } = 9;
    public double WaterSearchMiles { get; set; } = 1.5;
    public int MajorTownChanceDenominator { get; set; } = 3;
    public int PeopleAtNewSiteBase { get; set; } = 24;
    public int PeopleAtNewSiteRollMax { get; set; } = 36;
    public int FarmCountZeroBelowPercent { get; set; } = 60;
    public int FarmCountOneBelowPercent { get; set; } = 95;
    public int AcreageChancePercent { get; set; } = 12;
    public double AcreageAreaMinAcres { get; set; } = 1;
    public double AcreageAreaMaxAcres { get; set; } = 5;
    public double FarmsteadAreaMinAcres { get; set; } = 3;
    public double FarmsteadAreaMaxAcres { get; set; } = 9;
    public int HomeOccupancyPercent { get; set; } = 95;
    public int GarageChancePercent { get; set; } = 42;
    public double SeaLevelFeet { get; set; } = -50;
    public double SouthDropFeetPerMile { get; set; } = 4;
    public int GambrelBarnChancePercent { get; set; } = 12;
    public int ShelterbeltMinRows { get; set; } = 3;
    public int ShelterbeltMaxRows { get; set; } = 7;
    public int FieldEntrancesMinPerQuarter { get; set; } = 1;
    public int FieldEntrancesMaxPerQuarter { get; set; } = 2;
    public double FieldEntranceWidthFeet { get; set; } = 26;
    public double FieldEntranceLengthMinFeet { get; set; } = 95;
    public double FieldEntranceLengthMaxFeet { get; set; } = 170;
    public double FieldEntranceSetbackFeet { get; set; } = 110;
    public int CattleYardChancePercent { get; set; } = 25;
    public double CattleYardMinAcres { get; set; } = 20;
    public double CattleYardMaxAcres { get; set; } = 40;
    public int CattleYardSectionsPerSite { get; set; } = 4;
    public double CoopCornerRailDistanceFeet { get; set; } = 300;
    public double CoopCornerTownExclusionMiles { get; set; } = 2;
    public int TownNorthPlacementPercent { get; set; } = 46;
    public int TownSouthPlacementPercent { get; set; } = 24;
    public int TownAlleyChancePercent { get; set; } = 55;
    public int TownDowntownBusinessPercent { get; set; } = 70;
    public int TownNoExtraStreetPercent { get; set; } = 35;
    public int TownMaxExtraStreets { get; set; } = 2;
    public double TownSidewalkWidthFeet { get; set; } = 4.5;
    public double TownSidewalkInsetFeet { get; set; } = 2.25;
    public double TownPavedStripSearchMiles { get; set; } = 1.5;
    public double TownDeadEndBulbWidthFeet { get; set; } = 44;
    public int TownWaterTowerChancePercent { get; set; } = 90;
    public int TownBallFieldChancePercent { get; set; } = 50;
    public int TownCemeteryChancePercent { get; set; } = 60;
    public int TownLibraryChancePercent { get; set; } = 70;
    public int TownEdgeParkChancePercent { get; set; } = 40;
    public int TownCentralSquareChancePercent { get; set; } = 15;
    public int TownGolfCourseChancePercent { get; set; } = 15;
    public int TownFairgroundsChancePercent { get; set; } = 10;
    public int TownAirstripChancePercent { get; set; } = 8;
    public int TownMotelSmallChancePercent { get; set; } = 12;
    public int TownMotelMediumChancePercent { get; set; } = 34;
    public int TownMotelLargeChancePercent { get; set; } = 58;
    public int TreeDensityPercent { get; set; } = 20;
    public int TownStreetTreeChancePercent { get; set; } = 15;
    public int TreeGroveSectionsPerGrove { get; set; } = 4;
}

/// <summary>Compatibility conversion at the viewer boundary. Legacy map points use miles, north-positive Y.</summary>
public static class LegacyMapFeatureConverter
{
    private const double FeetPerMile = 5280.0;

    public static WorldPosition ToWorldPosition(MapPoint point, double elevationFeet = 0) =>
        new(point.X * FeetPerMile, elevationFeet, point.Y * FeetPerMile);

    public static MapPoint ToLegacyMapPoint(WorldPosition point) =>
        new() { X = point.X / FeetPerMile, Y = point.Z / FeetPerMile };

    public static WorldRoad ToRoad(MapFeature feature) => new(
        feature.RouteId ?? string.Empty,
        (feature.Points ?? new List<MapPoint>()).Select(point => ToWorldPosition(point)).ToList(),
        feature.Width * FeetPerMile,
        feature.Surface ?? string.Empty);

    public static WorldFeature ToWorldFeature(MapFeature feature) => new WorldFeature(
        feature.RouteId ?? string.Empty, feature.Kind ?? string.Empty, feature.Name ?? string.Empty,
        feature.Surface ?? string.Empty, feature.Carrier ?? string.Empty,
        (feature.Points ?? new List<MapPoint>()).Select(point => ToWorldPosition(point, GroundElevation(feature))).ToList(),
        feature.Width * FeetPerMile, LegacyHeightFeet(feature), GroundElevation(feature),
        feature.Population, feature.StreamOrder, IsClosedGeometry(feature), feature.IsMajorTown, feature.RailPassThrough ?? string.Empty,feature.YearBuilt)
    {
        GroundElevationFeet = GroundElevation(feature),
        VerticalHeightFeet = IsBuildingKind(feature.Kind) ? feature.Height * FeetPerMile : 0,
        FootprintWidthFeet = IsBuildingKind(feature.Kind) ? feature.Width * FeetPerMile : 0,
        FootprintDepthFeet = IsBuildingKind(feature.Kind) ? (feature.DepthFt ?? feature.Height * FeetPerMile) : 0,
        Archetype = IsBuildingKind(feature.Kind) ? feature.Name ?? string.Empty : string.Empty,
        ParentId = feature.Carrier ?? string.Empty
    };

    private static bool IsBuildingKind(string kind) => kind is "rural_building" or "town_building" or "town_garage" or
        "town_business" or "town_business_aux" or "cattle_office_shed" or "rural_coop_elevator" or "rural_coop_bin" or "town_rail_elevator" or "town_rail_bin";
    private static double GroundElevation(MapFeature feature) => feature.Kind == "contours" ? feature.Height : 0;
    private static double LegacyHeightFeet(MapFeature feature) => IsBuildingKind(feature.Kind)
        ? (feature.DepthFt ?? feature.Height * FeetPerMile) : feature.Kind == "contours" ? 0 : feature.Height * FeetPerMile;

    private static bool IsClosedGeometry(MapFeature feature) => feature.Closed || feature.Kind is
        "lakes" or "town" or "town_lot" or "town_business_lot" or "rural_lot" or "rural_building" or
        "rural_yard" or "town_building" or "town_garage" or "town_business" or "town_business_aux" or
        "town_rail_ground" or "town_rail_elevator" or "town_rail_bin";

    public static MapFeature ToLegacyMapFeature(WorldFeature feature) => new()
    {
        RouteId=feature.Id, Kind=feature.Kind, Name=feature.Name, Surface=feature.Surface, Carrier=feature.Carrier,
        Points=(feature.Geometry ?? new List<WorldPosition>()).Select(ToLegacyMapPoint).ToList(),
        Width=feature.WidthFeet/FeetPerMile, Height=feature.HeightFeet/FeetPerMile,
        Population=feature.Population, StreamOrder=feature.StreamOrder, Closed=feature.Closed, IsMajorTown=feature.IsMajor,
        RailPassThrough=feature.RailPassThrough, YearBuilt=feature.YearBuilt
    };

    public static WorldScene ToWorldScene(ViewerScene scene) => new(scene.ChunkX, scene.ChunkY,
        (scene.Features ?? new List<MapFeature>()).Select(ToWorldFeature).ToList());

    public static IReadOnlyList<WorldFeature> ToWorldFeatures(IEnumerable<MapFeature> features) =>
        (features ?? Enumerable.Empty<MapFeature>()).Select(ToWorldFeature).ToList();

    public static WorldBuilding ToBuilding(MapFeature feature, string archetype = "") => new(
        feature.RouteId ?? string.Empty,
        (feature.Points ?? new List<MapPoint>()).Select(point => ToWorldPosition(point)).ToList(),
        0,
        archetype.Length > 0 ? archetype : feature.Name ?? string.Empty);

    public static WorldPerson ToPerson(Settler settler, string lotId = "") => new(
        settler.Id ?? string.Empty,
        settler.Name ?? string.Empty,
        lotId,
        new WorldPosition(settler.X * FeetPerMile, 0, settler.Y * FeetPerMile));
}
