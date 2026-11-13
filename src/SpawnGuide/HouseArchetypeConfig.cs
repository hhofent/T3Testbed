using System;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// House archetype configurations with distribution-based parameters.
    /// Replaces simple FloatRange/IntRange with FloatDistribution/IntDistribution.
    /// </summary>
    public sealed class HouseArchetypeConfig
    {
        // =========================================================================
        // RANCH (1950s-90s, most common)
        // =========================================================================
        public sealed class RanchConfig
        {
            /// <summary>Ranch width (feet, LogNormal).</summary>
            [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
            [Rationale("LogNormal(3.7, 0.1) ≈ 40ft - typical ranch width.")]
            public FloatDistribution WidthFt { get; set; } = FloatDistribution.LogNormal(3.7f, 0.1f, 30f, 55f);

            /// <summary>Ranch depth (feet, LogNormal).</summary>
            [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
            [Rationale("LogNormal(3.2, 0.1) ≈ 25ft - shallow ranch depth.")]
            public FloatDistribution DepthFt { get; set; } = FloatDistribution.LogNormal(3.2f, 0.1f, 20f, 35f);

            /// <summary>Attached garage width (feet, Normal).</summary>
            [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
            [Rationale("Normal(22, 3) - standard 2-car garage.")]
            public FloatDistribution GarageWidthFt { get; set; } = FloatDistribution.Normal(22f, 3f, 16f, 30f);

            /// <summary>Attached garage depth (feet, Normal).</summary>
            [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
            [Rationale("Normal(22, 2) - standard garage depth.")]
            public FloatDistribution GarageDepthFt { get; set; } = FloatDistribution.Normal(22f, 2f, 18f, 26f);

            /// <summary>Chance garage is on left side of front facade.</summary>
            [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
            [Rationale("50/50 - no side preference.")]
            public float GarageLeftChance { get; set; } = 0.5f;

            /// <summary>Front door offset from center (fraction of width, Beta).</summary>
            [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
            [Rationale("Beta(2,5, 0.15, 0.35) - door biased toward garage side.")]
            public FloatDistribution DoorOffsetFraction { get; set; } = FloatDistribution.Beta(2f, 5f, 0.15f, 0.35f);

            /// <summary>Front window jitter fraction (Beta).</summary>
            [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Visual)]
            [Rationale("Beta(2,5, 0, 0.08) - windows nearly aligned with slight variance.")]
            public FloatDistribution WindowJitterFraction { get; set; } = FloatDistribution.Beta(2f, 5f, 0f, 0.08f);
        }

        // =========================================================================
        // FARMHOUSE (2-story, traditional)
        // =========================================================================
        public sealed class FarmhouseConfig
        {
            /// <summary>Farmhouse width (feet, LogNormal).</summary>
            [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
            [Rationale("LogNormal(3.4, 0.1) ≈ 30ft - narrow traditional form.")]
            public FloatDistribution WidthFt { get; set; } = FloatDistribution.LogNormal(3.4f, 0.1f, 24f, 40f);

            /// <summary>Farmhouse depth (feet, LogNormal).</summary>
            [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
            [Rationale("LogNormal(3.5, 0.1) ≈ 33ft - deeper than wide.")]
            public FloatDistribution DepthFt { get; set; } = FloatDistribution.LogNormal(3.5f, 0.1f, 26f, 44f);

            /// <summary>Porch width as fraction of house width (Beta).</summary>
            [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
            [Rationale("Beta(5,3, 0.5, 0.8) - porch spans 50-80% of front.")]
            public FloatDistribution PorchWidthFraction { get; set; } = FloatDistribution.Beta(5f, 3f, 0.5f, 0.8f);

            /// <summary>Porch depth (feet, Normal).</summary>
            [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
            [Rationale("Normal(7, 1) - usable porch depth.")]
            public FloatDistribution PorchDepthFt { get; set; } = FloatDistribution.Normal(7f, 1f, 5f, 10f);

            /// <summary>Chance front door is centered.</summary>
            [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
            [Rationale("90% - traditional centered door.")]
            public float DoorCenteredChance { get; set; } = 0.9f;
        }

        // =========================================================================
        // SPLIT-LEVEL (1960s-80s)
        // =========================================================================
        public sealed class SplitLevelConfig
        {
            /// <summary>Split-level width (feet, LogNormal).</summary>
            [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
            [Rationale("LogNormal(3.4, 0.1) ≈ 30ft - compact footprint.")]
            public FloatDistribution WidthFt { get; set; } = FloatDistribution.LogNormal(3.4f, 0.1f, 24f, 40f);

            /// <summary>Split-level depth (feet, LogNormal).</summary>
            [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
            [Rationale("LogNormal(3.4, 0.1) ≈ 30ft - roughly square.")]
            public FloatDistribution DepthFt { get; set; } = FloatDistribution.LogNormal(3.4f, 0.1f, 24f, 40f);

            /// <summary>Integral garage width (feet, Normal).</summary>
            [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
            [Rationale("Normal(16, 2) - single-car integral garage typical.")]
            public FloatDistribution GarageWidthFt { get; set; } = FloatDistribution.Normal(16f, 2f, 12f, 22f);

            /// <summary>Integral garage depth (feet, Normal).</summary>
            [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
            [Rationale("Normal(20, 2) - garage under living space.")]
            public FloatDistribution GarageDepthFt { get; set; } = FloatDistribution.Normal(20f, 2f, 16f, 26f);

            /// <summary>Chance garage is on left.</summary>
            [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
            public float GarageLeftChance { get; set; } = 0.5f;

            /// <summary>Chance garage door is on side (not front) facade.</summary>
            [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
            [Rationale("90% - split-levels usually have side-entry garage.")]
            public float GarageDoorOnSideChance { get; set; } = 0.9f;
        }

        // =========================================================================
        // SHARED PARAMETERS
        // =========================================================================
        public RanchConfig Ranch { get; set; } = new();
        public FarmhouseConfig Farmhouse { get; set; } = new();
        public SplitLevelConfig SplitLevel { get; set; } = new();

        /// <summary>Siding type options (1990s Iowa market share order).</summary>
        [Category(CategoryAttribute.Concern.Visual), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Market order: White/Ivory/Beige vinyl dominant, brick front legacy, wood/aluminum rare.")]
        public SidingType[] SidingOptions { get; set; } = new[]
        {
            SidingType.WhiteVinyl,
            SidingType.IvoryVinyl,
            SidingType.BeigeVinyl,
            SidingType.LightGrayVinyl,
            SidingType.BrickFrontVinylSides,
            SidingType.WoodLap,
            SidingType.Aluminum,
        };

        /// <summary>Garage door width: bimodal 9ft (single) / 16ft (double).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("Bimodal(9ft:65%, 16ft:35%) - single vs double door.")]
        public FloatDistribution GarageDoorWidthFt { get; set; } = FloatDistribution.Bimodal(9f, 16f, 0.65f);

        /// <summary>Minimum wall length for 2 windows (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("Normal(22, 2) - need ~22ft for two 3ft windows + trim.")]
        public FloatDistribution TwoWindowMinWallFt { get; set; } = FloatDistribution.Normal(22f, 2f, 18f, 28f);

        /// <summary>Minimum wall length for 1 window (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("Normal(9, 1) - need ~9ft for one 3ft window + trim.")]
        public FloatDistribution OneWindowMinWallFt { get; set; } = FloatDistribution.Normal(9f, 1f, 7f, 12f);

        /// <summary>Sample a house archetype from the Dirichlet weights.</summary>
        public HouseArchetype SampleArchetype(Rng rng, FloatDistribution weights)
        {
            var w = weights.SampleDirichlet(rng);
            float roll = rng.NextFloat();
            float cum = 0;
            for (int i = 0; i < w.Length; i++)
            {
                cum += w[i];
                if (roll <= cum) return (HouseArchetype)i;
            }
            return HouseArchetype.Ranch;
        }

        /// <summary>Sample a siding type (uniform from options).</summary>
        public SidingType SampleSiding(Rng rng)
        {
            return rng.Pick(SidingOptions);
        }
        public void Validate() { }

    }
}