using System;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>Surface material type for driveways, walks, roads, etc.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SurfaceType
    {
        Gravel,
        Concrete,
        Asphalt,
        Dirt,
    }

    /// <summary>Garage attachment mode for houses.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GarageMode
    {
        /// <summary>Integral/attached garage (part of main house footprint).</summary>
        Integral,
        /// <summary>Detached garage (separate building).</summary>
        Detached,
        /// <summary>No garage.</summary>
        None,
    }

    /// <summary>Road network topology pattern.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RoadTopology
    {
        /// <summary>Cardinal grid (section lines).</summary>
        Grid,
        /// <summary>Organic/curvilinear following terrain.</summary>
        Organic,
        /// <summary>Mix of grid and organic.</summary>
        Hybrid,
        /// <summary>Radial from a center point.</summary>
        Radial,
        /// <summary>Following river/stream corridors.</summary>
        RiverFollowing,
    }

    /// <summary>House siding material and color (1990s Iowa market).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SidingType
    {
        WhiteVinyl,
        IvoryVinyl,
        BeigeVinyl,
        LightGrayVinyl,
        BrickFrontVinylSides,
        WoodLap,
        Aluminum,
    }

    /// <summary>Road surface material.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RoadSurface
    {
        Asphalt,
        Gravel,
        Dirt,
    }

    /// <summary>How a town sits against its highway.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TownLayout
    {
        /// <summary>North of the highway only.</summary>
        North,
        /// <summary>South of the highway only.</summary>
        South,
        /// <summary>Straddling the highway (both sides).</summary>
        Both,
    }

    /// <summary>Town size/class tier (Central Place Theory: k=3 marketing hierarchy).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TownClass
    {
        /// <summary>Crossroads: minimal commercial, no civic anchors.</summary>
        Crossroads = 0,
        /// <summary>Hamlet: small commercial strip, 1-2 civic anchors.</summary>
        Hamlet = 1,
        /// <summary>Village: full commercial, school, multiple churches, park.</summary>
        Village = 2,
        /// <summary>Town: large commercial, consolidated school, water tower, fairgrounds.</summary>
        Town = 3,
    }

    /// <summary>House archetype/style.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HouseArchetype
    {
        Ranch,
        Farmhouse,
        SplitLevel,
    }

    /// <summary>Outbuilding/kind type.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OutbuildingKind
    {
        None,
        MachineShed,
        GrainBin,
        GrainElevator,
        PoleBarn,
        GambrelBarn,
        Shop,
        LeanTo,
        LpTank,
        Shelterbelt,
        DetachedGarage,
        Shed,
        Storefront,
        Church,
        School,
        CityPark,
        Depot,
        WaterTower,
        Library,
        FireHall,
        FireStation,
        Fairground,
        Fairgrounds,
        BallField,
        Cemetery,
        GolfCourse,
        Airstrip,
        PostOffice,
        CoOpCorner,
        PropaneTank,
    }

    /// <summary>Town extra feature type (feature grammar).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TownExtraType
    {
        CulDeSacCourt,
        PlatEdgeStreet,
        OutlyingRoad,
        BypassedHighway,
        IndustrialSideStreet,
        TownSquare,
        Boulevard,
        RailCorridorStreet,
        CurvedStreet,
    }

    /// <summary>Commercial business category.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BusinessCategory
    {
        GasStation,
        FeedStore,
        CoOp,
        Hardware,
        CafeDiner,
        ImplementDealer,
        Other,
        CabinetShop,
        MetalShop,
        FeedMill,
        GrainElevator,
    }

    /// <summary>Validation severity level.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ValidationSeverity
    {
        Error,      // Hard failure - config invalid
        Warning,    // Soft - deprecated, performance concern, unusual but valid
        Repair,     // Auto-fixable (min>max swap, empty array defaults)
    }
}