using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// Highway commercial business type definition.
    /// </summary>
    public sealed class HighwayBusinessType
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string Descriptor { get; set; } = "";
        public float Weight { get; set; } = 1f;
        public int MinTier { get; set; } = 0;
        public bool EdgeBusiness { get; set; } = false; // Prefers ends of strip
        public FloatDistribution WidthFt { get; set; } = FloatDistribution.LogNormal(3.5f, 0.3f, 20f, 80f);
        public FloatDistribution DepthFt { get; set; } = FloatDistribution.LogNormal(3.2f, 0.3f, 20f, 80f);
        public string Surface { get; set; } = "asphalt";
        public void Validate() { }

    }

    /// <summary>
    /// Rail industrial business type definition.
    /// </summary>
    public sealed class RailIndustrialType
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public int MinTier { get; set; } = 0;
        public FloatDistribution LotWidthFt { get; set; } = FloatDistribution.LogNormal(4.5f, 0.5f, 60f, 200f);
        public FloatDistribution LotDepthFt { get; set; } = FloatDistribution.LogNormal(3.2f, 0.3f, 20f, 80f);
        public void Validate() { }

    }

    /// <summary>
    /// Configuration for highway commercial strip and rail industrial.
    /// Separates highway-facing commercial (gas, food, retail) from rail industrial (elevators, ag services).
    /// </summary>
    public sealed class CommercialConfig
    {
        // =========================================================================
        // HIGHWAY COMMERCIAL STRIP
        // =========================================================================
        /// <summary>Spacing between commercial buildings along highway (miles).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Commercial), Units("miles")]
        [Rationale("1.5 miles between commercial clusters - highway strip spacing.")]
        public float HighwayCommercialSpacingMi { get; set; } = 1.5f;

        /// <summary>Depth of commercial lots from highway (feet, LogNormal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        [Rationale("LogNormal(3.5, 0.3) ≈ 33ft median - shallow highway frontage lots.")]
        public FloatDistribution HighwayCommercialDepthFt { get; set; } = FloatDistribution.LogNormal(3.5f, 0.3f, 20f, 100f);

        /// <summary>Width of commercial lots along highway (feet, LogNormal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        [Rationale("LogNormal(3.8, 0.4) ≈ 45ft median - typical storefront width.")]
        public FloatDistribution HighwayCommercialWidthFt { get; set; } = FloatDistribution.LogNormal(3.8f, 0.4f, 20f, 120f);

        /// <summary>Business type weights for highway commercial (must sum to ~1).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Commercial)]
        [Rationale("1990s Iowa highway mix: gas/food dominant, implement dealer at ends.")]
        public Dictionary<string, float> BusinessTypeWeights { get; set; } = new()
        {
            ["gas-station"] = 0.25f,
            ["feed-store"] = 0.15f,
            ["co-op"] = 0.10f,
            ["hardware"] = 0.10f,
            ["cafe-diner"] = 0.15f,
            ["implement-dealer"] = 0.10f,
            ["other"] = 0.15f,
        };

        /// <summary>Detailed business type definitions (overrides weights if provided).</summary>
        public List<HighwayBusinessType> BusinessTypes { get; set; } = new();

        // =========================================================================
        // GRAIN ELEVATOR (RAIL)
        // =========================================================================
        /// <summary>Elevator offset from highway (feet) when no rail spur.</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        [Rationale("200 ft - elevator near highway but not on it when no rail.")]
        public float ElevatorOffsetFromHighwayFt { get; set; } = 200f;

        /// <summary>Number of bins on elevator (Poisson + base).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Commercial)]
        [Rationale("5 bins base - typical 1990s country elevator.")]
        public IntDistribution ElevatorBinCount { get; set; } = new IntDistribution(FloatDistribution.Poisson(3, 2, 15));

        /// <summary>Bin diameter (feet, LogNormal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        [Rationale("22 ft diameter - standard grain bin size.")]
        public FloatDistribution BinDiameterFt { get; set; } = FloatDistribution.LogNormal(3.1f, 0.1f, 18f, 30f);

        /// <summary>Spacing between bin centers (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        [Rationale("30 ft = 22ft bin + 8ft gap.")]
        public float BinSpacingFt { get; set; } = 30f;

        /// <summary>Elevator building width (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        public float ElevatorWidthFt { get; set; } = 48f;

        /// <summary>Elevator building depth (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        public float ElevatorDepthFt { get; set; } = 88f;

        /// <summary>Chance elevator is on right (south) side of main street.</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Commercial)]
        public float ElevatorRightChance { get; set; } = 0.62f;

        // =========================================================================
        // RAIL INDUSTRIAL STRIP
        // =========================================================================
        /// <summary>Depth of rail industrial lots (feet, LogNormal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Industrial), Units("feet")]
        [Rationale("LogNormal(4.5, 0.5) ≈ 90ft median - deep lots for rail sidings.")]
        public FloatDistribution RailIndustrialLotDepthFt { get; set; } = FloatDistribution.LogNormal(4.5f, 0.5f, 60f, 200f);

        /// <summary>Width of rail industrial lots (feet, LogNormal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Industrial), Units("feet")]
        [Rationale("LogNormal(3.8, 0.4) ≈ 45ft median - siding width.")]
        public FloatDistribution RailIndustrialLotWidthFt { get; set; } = FloatDistribution.LogNormal(3.8f, 0.4f, 30f, 150f);

        /// <summary>Row spacing along rail (feet, FloatRange).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Industrial), Units("feet")]
        public FloatDistribution RailIndustrialRowSpacingFt { get; set; } = FloatDistribution.Uniform(56f, 84f);

        /// <summary>Z-jitter for rail industrial placement (feet).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Industrial), Units("feet")]
        public FloatDistribution RailIndustrialZJitterFt { get; set; } = FloatDistribution.Normal(0f, 16f, -50f, 50f);

        /// <summary>Rail industrial business types (grain only per G-004).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Industrial)]
        [Rationale("Rail industrial = grain elevators & ag services only. No manufacturing.")]
        public List<RailIndustrialType> RailIndustrialTypes { get; set; } = new()
        {
            new RailIndustrialType { Id = "grain-elevator", Label = "ELEVATOR", MinTier = 0, LotWidthFt = FloatDistribution.LogNormal(3.7f, 0.2f, 40f, 80f), LotDepthFt = FloatDistribution.LogNormal(4.5f, 0.3f, 80f, 150f) },
            new RailIndustrialType { Id = "feed-mill", Label = "FEED MILL", MinTier = 1, LotWidthFt = FloatDistribution.LogNormal(3.5f, 0.2f, 30f, 60f), LotDepthFt = FloatDistribution.LogNormal(4.0f, 0.3f, 50f, 100f) },
            new RailIndustrialType { Id = "ag-service", Label = "AG SERVICE", MinTier = 1, LotWidthFt = FloatDistribution.LogNormal(3.4f, 0.2f, 25f, 50f), LotDepthFt = FloatDistribution.LogNormal(3.7f, 0.3f, 40f, 80f) },
            new RailIndustrialType { Id = "coal-yard", Label = "COAL YARD", MinTier = 0, LotWidthFt = FloatDistribution.LogNormal(3.7f, 0.3f, 40f, 100f), LotDepthFt = FloatDistribution.LogNormal(4.2f, 0.3f, 60f, 120f) },
        };

        // =========================================================================
        // CROSS STREET COMMERCIAL (shops on the street perpendicular to highway)
        // =========================================================================
        /// <summary>Max shops on cross street.</summary>
        [Category(CategoryAttribute.Concern.Density), Category(CategoryAttribute.Concern.Commercial)]
        public int CrossStreetShopMax { get; set; } = 3;

        /// <summary>Cross street shop depth along street (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        public FloatDistribution CrossStreetShopDepthFt { get; set; } = FloatDistribution.Uniform(24f, 100f);

        /// <summary>Cross street shop width across street (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        public FloatDistribution CrossStreetShopWidthFt { get; set; } = FloatDistribution.Uniform(24f, 100f);
        public void Validate() { }

    }
}