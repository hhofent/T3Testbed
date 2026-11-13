using System;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// 1990s residential lot and house placement standards (Iowa).
    /// Based on: 25ft front / 7.5ft side / 25ft rear setbacks; garage modes Dirichlet(55/35/10);
    /// concrete drives/walks; Poisson yard trees.
    /// </summary>
    public sealed class ResidentialConfig
    {
        // =========================================================================
        // LOT GEOMETRY
        // =========================================================================
        /// <summary>Lot frontage (feet, LogNormal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("LogNormal(4.2, 0.2) ≈ 67ft median - standard 1990s subdivision lot.")]
        public FloatDistribution LotFrontageFt { get; set; } = FloatDistribution.LogNormal(4.2f, 0.2f, 45f, 100f);

        /// <summary>Lot depth (feet, LogNormal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("LogNormal(4.8, 0.15) ≈ 121ft median - deep lots for yards.")]
        public FloatDistribution LotDepthFt { get; set; } = FloatDistribution.LogNormal(4.8f, 0.15f, 80f, 180f);

        // =========================================================================
        // SETBACKS (1990s Iowa standards)
        // =========================================================================
        /// <summary>Front setback from street ROW (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("Normal(25, 5) - 25ft front yard standard, ±5ft variance.")]
        public FloatDistribution FrontSetbackFt { get; set; } = FloatDistribution.Normal(25f, 5f, 15f, 40f);

        /// <summary>Side setback (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("Normal(7.5, 2) - 7.5ft side yard standard per Iowa zoning.")]
        public FloatDistribution SideSetbackFt { get; set; } = FloatDistribution.Normal(7.5f, 2f, 5f, 15f);

        /// <summary>Rear setback (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("Normal(25, 5) - 25ft rear yard standard.")]
        public FloatDistribution RearSetbackFt { get; set; } = FloatDistribution.Normal(25f, 5f, 15f, 40f);

        // =========================================================================
        // GARAGE MODES (Dirichlet weights)
        // =========================================================================
        /// <summary>Garage mode weights: Integral, Detached, None (Dirichlet concentrations).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Dirichlet(0.55, 0.35, 0.10) - 55% integral, 35% detached, 10% none (1990s).")]
        public FloatDistribution GarageModeWeights { get; set; } = FloatDistribution.Dirichlet(0.55f, 0.35f, 0.10f);

        /// <summary>Attached garage width (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("Normal(22, 2) - standard 2-car attached garage width.")]
        public FloatDistribution AttachedGarageWidthFt { get; set; } = FloatDistribution.Normal(22f, 2f, 18f, 28f);

        /// <summary>Attached garage depth (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("Normal(22, 2) - standard 2-car garage depth.")]
        public FloatDistribution AttachedGarageDepthFt { get; set; } = FloatDistribution.Normal(22f, 2f, 18f, 28f);

        /// <summary>Detached garage width (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("Normal(24, 3) - detached garages slightly larger.")]
        public FloatDistribution DetachedGarageWidthFt { get; set; } = FloatDistribution.Normal(24f, 3f, 18f, 32f);

        /// <summary>Detached garage depth (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("Normal(24, 3) - detached garage depth.")]
        public FloatDistribution DetachedGarageDepthFt { get; set; } = FloatDistribution.Normal(24f, 3f, 18f, 32f);

        // =========================================================================
        // DRIVEWAYS & WALKS
        // =========================================================================
        /// <summary>Driveway width (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("Normal(16, 2) - standard 2-car driveway width.")]
        public FloatDistribution DrivewayWidthFt { get; set; } = FloatDistribution.Normal(16f, 2f, 12f, 24f);

        /// <summary>Driveway surface material.</summary>
        [Category(CategoryAttribute.Concern.Visual), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("Concrete - 1990s standard for town driveways.")]
        public SurfaceType DrivewaySurface { get; set; } = SurfaceType.Concrete;

        /// <summary>Walk width (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("Normal(4, 0.5) - standard 4ft sidewalk.")]
        public FloatDistribution WalkWidthFt { get; set; } = FloatDistribution.Normal(4f, 0.5f, 3f, 5f);

        /// <summary>Walk surface material.</summary>
        [Category(CategoryAttribute.Concern.Visual), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("Concrete - universal for town walks.")]
        public SurfaceType WalkSurface { get; set; } = SurfaceType.Concrete;

        // =========================================================================
        // SHEDS & ACCESSORIES
        // =========================================================================
        /// <summary>Chance of a backyard shed.</summary>
        [Category(CategoryAttribute.Concern.Density), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("15% - modest shed presence in 1990s.")]
        public float ShedChance { get; set; } = 0.15f;

        /// <summary>Shed width (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        public FloatDistribution ShedWidthFt { get; set; } = FloatDistribution.Normal(10f, 1.5f, 8f, 14f);

        /// <summary>Shed depth (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        public FloatDistribution ShedDepthFt { get; set; } = FloatDistribution.Normal(12f, 2f, 8f, 18f);

        // =========================================================================
        // STREET & YARD TREES
        // =========================================================================
        /// <summary>Street tree spacing (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Vegetation), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("Normal(40, 8) - mature street tree spacing.")]
        public FloatDistribution StreetTreeSpacingFt { get; set; } = FloatDistribution.Normal(40f, 8f, 25f, 60f);

        /// <summary>Yard tree count (Poisson).</summary>
        [Category(CategoryAttribute.Concern.Vegetation), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Poisson(2.5) - average 2-3 yard trees per lot.")]
        public IntDistribution YardTreeCount { get; set; } = new IntDistribution(FloatDistribution.Poisson(2.5f));

        /// <summary>Sample a garage mode from the Dirichlet distribution.</summary>
        public GarageMode SampleGarageMode(Rng rng)
        {
            var weights = GarageModeWeights.SampleDirichlet(rng);
            float roll = rng.NextFloat();
            float cum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                cum += weights[i];
                if (roll <= cum) return (GarageMode)i;
            }
            return GarageMode.None;
        }

        /// <summary>Density multiplier for residential generation.</summary>
        public float DensityMultiplier { get; set; } = 1.0f;
        public void Validate() { }

    }
}