using System;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// Farmstead evolution model: homestead (1880s) → 1990s snapshot.
    /// Includes PSFP shelterbelts (mature by 1990s, 3-7 rows N/W),
    /// machine sheds for 1990s equipment, bin rows.
    /// </summary>
    public sealed class FarmsteadConfig
    {
        // =========================================================================
        // LOT GEOMETRY
        // =========================================================================
        /// <summary>Farmstead lot width (feet, LogNormal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("LogNormal(5.5, 0.1) ≈ 245ft - quarter-quarter section frontage.")]
        public FloatDistribution LotWidthFt { get; set; } = FloatDistribution.LogNormal(5.5f, 0.1f, 180f, 350f);

        /// <summary>Farmstead lot depth (feet, LogNormal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("LogNormal(5.8, 0.1) ≈ 330ft - deep lot for outbuildings.")]
        public FloatDistribution LotDepthFt { get; set; } = FloatDistribution.LogNormal(5.8f, 0.1f, 250f, 500f);

        /// <summary>House archetype weights: Ranch, Farmhouse, SplitLevel (Dirichlet).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Dirichlet(0.6, 0.3, 0.1) - farmsteads favor farmhouse & ranch.")]
        public FloatDistribution HouseArchetypeWeights { get; set; } = FloatDistribution.Dirichlet(0.6f, 0.3f, 0.1f);

        /// <summary>House setback from road (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential), Units("feet")]
        [Rationale("Normal(50, 10) - farmsteads set back further than town lots.")]
        public FloatDistribution HouseSetbackFt { get; set; } = FloatDistribution.Normal(50f, 10f, 30f, 80f);

        // =========================================================================
        // MACHINE SHED (1990s equipment scale)
        // =========================================================================
        /// <summary>Machine shed width (feet, LogNormal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Industrial), Units("feet")]
        [Rationale("LogNormal(3.7, 0.15) ≈ 40ft - fits 1990s 4WD tractor + implement.")]
        public FloatDistribution MachineShedWidthFt { get; set; } = FloatDistribution.LogNormal(3.7f, 0.15f, 30f, 60f);

        /// <summary>Machine shed depth (feet, LogNormal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Industrial), Units("feet")]
        [Rationale("LogNormal(3.5, 0.15) ≈ 33ft - depth for equipment parking.")]
        public FloatDistribution MachineShedDepthFt { get; set; } = FloatDistribution.LogNormal(3.5f, 0.15f, 24f, 50f);

        /// <summary>Machine shed distance behind house (feet).</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Industrial), Units("feet")]
        [Rationale("26 ft - standard farmstead layout: house, yard, machine shed.")]
        public float MachineShedBehindHouseFt { get; set; } = 26f;

        // =========================================================================
        // GRAIN BINS
        // =========================================================================
        /// <summary>Bin count (Poisson + base).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Commercial)]
        [Rationale("Poisson(3)+2 = 2-8 bins typical for 1990s farmstead.")]
        public IntDistribution BinCount { get; set; } = new IntDistribution(FloatDistribution.Poisson(3f, 2f, 10f));

        /// <summary>Bin diameter (feet, LogNormal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        [Rationale("LogNormal(3.0, 0.1) ≈ 20ft - standard bin size.")]
        public FloatDistribution BinDiameterFt { get; set; } = FloatDistribution.LogNormal(3.0f, 0.1f, 15f, 30f);

        /// <summary>Bin row distance behind machine shed (feet).</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Commercial), Units("feet")]
        [Rationale("8 ft - bins just behind shed.")]
        public float BinRowBehindShedFt { get; set; } = 8f;

        // =========================================================================
        // POLE BARN (optional, deeper storage)
        // =========================================================================
        /// <summary>Pole barn chance.</summary>
        [Category(CategoryAttribute.Concern.Density), Category(CategoryAttribute.Concern.Industrial)]
        [Rationale("40% - common for hay/equipment overflow.")]
        public float PoleBarnChance { get; set; } = 0.4f;

        /// <summary>Pole barn width (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Industrial), Units("feet")]
        [Rationale("Normal(30, 4) - 30ft clear span typical.")]
        public FloatDistribution PoleBarnWidthFt { get; set; } = FloatDistribution.Normal(30f, 4f, 20f, 44f);

        /// <summary>Pole barn depth (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Industrial), Units("feet")]
        [Rationale("Normal(48, 6) - deep for hay storage.")]
        public FloatDistribution PoleBarnDepthFt { get; set; } = FloatDistribution.Normal(48f, 6f, 32f, 72f);

        // =========================================================================
        // GAMBREL BARN (legacy, pre-1990s)
        // =========================================================================
        /// <summary>Gambrel barn chance.</summary>
        [Category(CategoryAttribute.Concern.Density), Category(CategoryAttribute.Concern.Historical)]
        [Rationale("15% - surviving pre-1950s dairy barns.")]
        public float GambrelBarnChance { get; set; } = 0.15f;

        /// <summary>Gambrel barn width (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Historical), Units("feet")]
        public FloatDistribution GambrelBarnWidthFt { get; set; } = FloatDistribution.Normal(34f, 4f, 26f, 44f);

        /// <summary>Gambrel barn depth (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Historical), Units("feet")]
        public FloatDistribution GambrelBarnDepthFt { get; set; } = FloatDistribution.Normal(56f, 6f, 40f, 76f);

        // =========================================================================
        // SHELTERBELT (PSFP - mature by 1990s)
        // =========================================================================
        /// <summary>Shelterbelt chance.</summary>
        [Category(CategoryAttribute.Concern.Density), Category(CategoryAttribute.Concern.Vegetation)]
        [Rationale("60% - PSFP plantings mature by 1990s, 3-7 rows N/W.")]
        public float ShelterbeltChance { get; set; } = 0.6f;

        /// <summary>Tree spacing within rows (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Vegetation), Units("feet")]
        [Rationale("Normal(16, 2) - mature conifer spacing.")]
        public FloatDistribution ShelterbeltTreeSpacingFt { get; set; } = FloatDistribution.Normal(16f, 2f, 12f, 22f);

        /// <summary>Number of rows (Poisson + 2 = 3-7 rows).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Vegetation)]
        [Rationale("Poisson(3)+2 = 3-7 rows mature PSFP standard.")]
        public IntDistribution ShelterbeltRows { get; set; } = new IntDistribution(FloatDistribution.Poisson(3f, 2f, 10f));

        /// <summary>Shelterbelt offset from building cluster (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Vegetation), Units("feet")]
        [Rationale("Normal(30, 10) - shelterbelt at field edge, not tight to buildings.")]
        public FloatDistribution ShelterbeltOffsetFt { get; set; } = FloatDistribution.Normal(30f, 10f, 15f, 60f);

        // =========================================================================
        // DRIVEWAYS & WALKS
        // =========================================================================
        /// <summary>Driveway width (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("Normal(14, 2) - single lane gravel drive to road.")]
        public FloatDistribution DrivewayWidthFt { get; set; } = FloatDistribution.Normal(14f, 2f, 10f, 20f);

        /// <summary>Driveway surface.</summary>
        [Category(CategoryAttribute.Concern.Visual), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("Gravel - farmstead drives rarely paved.")]
        public SurfaceType DrivewaySurface { get; set; } = SurfaceType.Gravel;

        /// <summary>Walk width (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("Normal(3.5, 0.5) - narrower than town walks.")]
        public FloatDistribution WalkWidthFt { get; set; } = FloatDistribution.Normal(3.5f, 0.5f, 2.5f, 5f);

        /// <summary>Walk surface.</summary>
        [Category(CategoryAttribute.Concern.Visual), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("Concrete - house-to-outbuilding walks.")]
        public SurfaceType WalkSurface { get; set; } = SurfaceType.Concrete;

        // =========================================================================
        // LP TANK
        // =========================================================================
        /// <summary>LP tank chance.</summary>
        [Category(CategoryAttribute.Concern.Density), Category(CategoryAttribute.Concern.Industrial)]
        public float LpTankChance { get; set; } = 0.3f;

        /// <summary>LP tank offset from house (feet).</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Industrial), Units("feet")]
        public float LpTankOffsetFromHouseFt { get; set; } = 8f;
        /// <summary>Density multiplier for farmstead generation.</summary>
        public float DensityMultiplier { get; set; } = 1.0f;
        public void Validate() { }

    }
}