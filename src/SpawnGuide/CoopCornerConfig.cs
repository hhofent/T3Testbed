using System;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// Coop Corner archetype: grain elevator complex at section-line corner with rail access.
    /// Section-line corner, rail-required, with elevator, office, scale house, bins.
    /// </summary>
    public sealed class CoopCornerConfig
    {
        /// <summary>Chance per section corner that has rail access.</summary>
        [Category(CategoryAttribute.Concern.Density), Category(CategoryAttribute.Concern.Commercial)]
        [Rationale("2% - rare but present at rail-section intersections.")]
        public float ChancePerSectionCorner { get; set; } = 0.02f;

        /// <summary>Requires rail line within spur distance.</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("True - coop corners exist only where rail meets section grid.")]
        public bool RequiresRail { get; set; } = true;

        /// <summary>Requires highway access (optional).</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("False - rail is the key; highway is secondary.")]
        public bool RequiresHighway { get; set; } = false;

        // =========================================================================
        // ELEVATOR COMPLEX
        // =========================================================================
        /// <summary>Elevator capacity (bushels, LogNormal in thousands).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Commercial)]
        [Rationale("LogNormal(5.5, 0.3) ≈ 250k bu - large country elevator.")]
        public FloatDistribution ElevatorCapacityBu { get; set; } = FloatDistribution.LogNormal(5.5f, 0.3f, 50000f, 1000000f);

        /// <summary>Number of bins (Poisson + base).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Commercial)]
        [Rationale("Poisson(6)+2 = 8-15 bins - major facility.")]
        public IntDistribution BinCount { get; set; } = new IntDistribution(FloatDistribution.Poisson(6f, 2f, 20f));

        /// <summary>Bin diameter (feet, LogNormal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        [Rationale("LogNormal(3.1, 0.1) ≈ 22ft - standard bin.")]
        public FloatDistribution BinDiameterFt { get; set; } = FloatDistribution.LogNormal(3.1f, 0.1f, 18f, 30f);

        /// <summary>Bin spacing (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        public float BinSpacingFt { get; set; } = 30f;

        /// <summary>Elevator headhouse width (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        public float ElevatorWidthFt { get; set; } = 54f;

        /// <summary>Elevator headhouse depth (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        public float ElevatorDepthFt { get; set; } = 100f;

        // =========================================================================
        // OFFICE & SCALE HOUSE
        // =========================================================================
        /// <summary>Office building size (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        [Rationale("Normal(40, 10) - modest office.")]
        public FloatDistribution OfficeSizeFt { get; set; } = FloatDistribution.Normal(40f, 10f, 24f, 60f);

        /// <summary>Scale house size (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        [Rationale("Normal(20, 5) - small scale house.")]
        public FloatDistribution ScaleHouseSizeFt { get; set; } = FloatDistribution.Normal(20f, 5f, 12f, 30f);

        /// <summary>Office offset from elevator (feet).</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        public float OfficeOffsetFt { get; set; } = 30f;

        /// <summary>Scale house offset from elevator (feet).</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        public float ScaleHouseOffsetFt { get; set; } = 50f;

        // =========================================================================
        // SITE LAYOUT
        // =========================================================================
        /// <summary>Lot width at section corner (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Placement), Units("feet")]
        public float LotWidthFt { get; set; } = 300f;

        /// <summary>Lot depth (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Placement), Units("feet")]
        public float LotDepthFt { get; set; } = 400f;

        /// <summary>Rail siding length (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        public float SidingLengthFt { get; set; } = 600f;

        /// <summary>Driveway/access road width (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        public float AccessRoadWidthFt { get; set; } = 24f;

        /// <summary>Access road surface.</summary>
        [Category(CategoryAttribute.Concern.Visual), Category(CategoryAttribute.Concern.Infrastructure)]
        public SurfaceType AccessRoadSurface { get; set; } = SurfaceType.Gravel;

        // =========================================================================
        // ADDITIONAL STRUCTURES
        // =========================================================================
        /// <summary>Feed mill chance.</summary>
        [Category(CategoryAttribute.Concern.Density), Category(CategoryAttribute.Concern.Commercial)]
        public float FeedMillChance { get; set; } = 0.3f;

        /// <summary>Feed mill size (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        public FloatDistribution FeedMillSizeFt { get; set; } = FloatDistribution.Normal(40f, 8f, 24f, 60f);

        /// <summary>Fertilizer storage chance.</summary>
        [Category(CategoryAttribute.Concern.Density), Category(CategoryAttribute.Concern.Commercial)]
        public float FertilizerStorageChance { get; set; } = 0.4f;

        /// <summary>Fertilizer tank count.</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Commercial)]
        public IntDistribution FertilizerTankCount { get; set; } = new IntDistribution(FloatDistribution.Poisson(2f, 1f, 10f));

        /// <summary>Fertilizer tank diameter (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        public FloatDistribution FertilizerTankDiameterFt { get; set; } = FloatDistribution.LogNormal(2.7f, 0.2f, 12f, 24f);
        public void Validate() { }

    }
}