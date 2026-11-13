using System;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// Iowa standard street and alley configuration.
    /// Based on: 66ft ROW, 32ft paved, 16ft gravel alleys (Beta clustered);
    /// perimeter loop with north jog; 70% 4-side blocks.
    /// </summary>
    public sealed class StreetConfig
    {
        // =========================================================================
        // STREET DIMENSIONS
        // =========================================================================
        /// <summary>Street right-of-way width (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("66 ft = standard Iowa section-line road ROW (1 chain).")]
        public float StreetRowFt { get; set; } = 66f;

        /// <summary>Street paved width (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("32 ft = two 11ft lanes + 2x5ft shoulders/parking.")]
        public float StreetPavedWidthFt { get; set; } = 32f;

        /// <summary>Centerline offset from ROW center (feet). Positive = north/east.</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("0 ft - pavement centered in ROW by default.")]
        public float CenterlineOffsetFt { get; set; } = 0f;

        /// <summary>Street surface material.</summary>
        [Category(CategoryAttribute.Concern.Visual), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("Asphalt - standard for paved town streets in 1990s.")]
        public RoadSurface StreetSurface { get; set; } = RoadSurface.Asphalt;

        // =========================================================================
        // ALLEYS
        // =========================================================================
        /// <summary>Alley right-of-way width (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("16 ft = standard Iowa alley ROW.")]
        public float AlleyRowFt { get; set; } = 16f;

        /// <summary>Alley surface material.</summary>
        [Category(CategoryAttribute.Concern.Visual), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("Gravel - alleys rarely paved in 1990s small towns.")]
        public SurfaceType AlleySurface { get; set; } = SurfaceType.Gravel;

        /// <summary>Alley chance per block row (Beta distribution for clustering).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("Beta(2,2) - clustered: alleys tend to appear in groups, not randomly scattered.")]
        public FloatDistribution AlleyChancePerBlock { get; set; } = FloatDistribution.Beta(2f, 2f, 0f, 1f);

        // =========================================================================
        // PERIMETER LOOP
        // =========================================================================
        /// <summary>Perimeter gap on north side (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Placement), Units("feet")]
        [Rationale("50 ft - perimeter loop breaks on north for highway access.")]
        public float PerimeterGapNFt { get; set; } = 50f;

        /// <summary>Perimeter jog height on north side (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Placement), Units("feet")]
        [Rationale("80 ft - north jog around commercial band.")]
        public float PerimeterJogHeightFt { get; set; } = 80f;

        // =========================================================================
        // DEAD ENDS
        // =========================================================================
        /// <summary>Dead-end stub length (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Placement), Units("feet")]
        [Rationale("250 ft - standard cul-de-sac stub before bulb.")]
        public float DeadEndStubLengthFt { get; set; } = 250f;

        /// <summary>Dead-end bulb radius (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Placement), Units("feet")]
        [Rationale("44 ft - standard turnaround radius for fire trucks.")]
        public float DeadEndBulbRadiusFt { get; set; } = 44f;

        // =========================================================================
        // BLOCK PATTERNS
        // =========================================================================
        /// <summary>Chance a block is 4-sided (lots on N-S streets too).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Geometry)]
        [Rationale("70% - most blocks fully developed on all four sides.")]
        public float FourSideBlockChance { get; set; } = 0.7f;

        /// <summary>Chance of rail corridor street (free street).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("30% - old rail line becomes a street in 1/3 of towns.")]
        public float RailCorridorStreetChance { get; set; } = 0.3f;

        /// <summary>Chance of curved street (free street).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("30% - organic street in 1/3 of towns.")]
        public float CurvedStreetChance { get; set; } = 0.3f;

        /// <summary>Erase half-width for free streets (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("20 ft - free streets erase grid within this half-width.")]
        public float FreeStreetEraseHalfFt { get; set; } = 20f;
        public void Validate() { }

    }
}