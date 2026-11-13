using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// Generative town tier system based on Central Place Theory (k=3 marketing hierarchy).
    /// Replaces the fixed SizePresets dictionary with computed parameters.
    /// Tiers: 0=Crossroads, 1=Hamlet, 2=Village, 3=Town
    /// </summary>
    public sealed class TownTierSystem
    {
        /// <summary>Central Place Theory k-value: 3=marketing, 4=transport, 7=admin.</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Historical)]
        [Rationale("k=3 marketing hierarchy: each higher tier serves 3x the market area of the tier below.")]
        public int KValue { get; set; } = 3;

        /// <summary>Maximum tier to generate (0-3).</summary>
        [Category(CategoryAttribute.Concern.Density), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("Cap at 3 (Town) - matches smallest Iowa cities per scope constraints.")]
        public int MaxTier { get; set; } = 3;

        // --- Grid geometry scaling per tier ---
        /// <summary>Base number of columns (block columns) for tier 0 (Crossroads).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("LogNormal(1.5, 0.4) - crossroads typically 1-3 columns.")]
        public FloatDistribution BaseColumns { get; set; } = FloatDistribution.LogNormal(0.4f, 0.4f, 1f, 5f); // log(1.5)≈0.4

        /// <summary>Multiplicative growth factor for columns per tier.</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("1.8x columns per tier - geometric growth matching central place theory.")]
        public float ColumnsGrowthPerTier { get; set; } = 1.8f;

        /// <summary>Base E-W streets for tier 0.</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("LogNormal(1.0, 0.3) - crossroads have 1-2 E-W streets.")]
        public FloatDistribution BaseEWStreets { get; set; } = FloatDistribution.LogNormal(0f, 0.3f, 1f, 4f); // log(1.0)=0

        /// <summary>Multiplicative growth factor for E-W streets per tier.</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("1.5x E-W streets per tier.")]
        public float EWStreetsGrowthPerTier { get; set; } = 1.5f;

        /// <summary>Commercial pads per 1000 population (scales with tier).</summary>
        [Category(CategoryAttribute.Concern.Commercial), Category(CategoryAttribute.Concern.Density)]
        [Rationale("2.5 pads per 1000 pop - calibrated to Iowa main street density.")]
        public float CommercialPadsPer1000Pop { get; set; } = 2.5f;

        // --- Civic thresholds (population-based) ---
        /// <summary>Minimum population for a church.</summary>
        [Category(CategoryAttribute.Concern.Civic), Category(CategoryAttribute.Concern.Density)]
        [Rationale("50 people per church - one-room church viable at crossroads.")]
        public int CivicChurchMinPop { get; set; } = 50;

        /// <summary>Minimum population for a school.</summary>
        [Category(CategoryAttribute.Concern.Civic), Category(CategoryAttribute.Concern.Density)]
        [Rationale("200 people - consolidated 1990s school district threshold.")]
        public int CivicSchoolMinPop { get; set; } = 200;

        /// <summary>Minimum population for a park.</summary>
        [Category(CategoryAttribute.Concern.Civic), Category(CategoryAttribute.Concern.Density)]
        [Rationale("150 people - park appears at hamlet/village transition.")]
        public int CivicParkMinPop { get; set; } = 150;

        // --- Landmark chances per tier ---
        /// <summary>Water tower chance per tier (cumulative).</summary>
        [Category(CategoryAttribute.Concern.Visual), Category(CategoryAttribute.Concern.Civic)]
        [Rationale("30% per tier - villages (tier 2) ~60%, towns (tier 3) ~90%.")]
        public float LandmarkWaterTowerChancePerTier { get; set; } = 0.3f;

        /// <summary>Golf course chance per tier.</summary>
        [Category(CategoryAttribute.Concern.Visual), Category(CategoryAttribute.Concern.Civic)]
        [Rationale("10% per tier - only largest towns get golf courses.")]
        public float LandmarkGolfCourseChancePerTier { get; set; } = 0.1f;

        // --- Population estimation ---
        /// <summary>Estimated population per residential lot.</summary>
        [Category(CategoryAttribute.Concern.Density), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("2.5 people per household (1990s Iowa average).")]
        public float PopulationPerLot { get; set; } = 2.5f;

        /// <summary>Compute grid dimensions for a given tier.</summary>
        public (int columns, int ewStreets) ComputeGridSize(int tier, Rng rng)
        {
            tier = Math.Clamp(tier, 0, MaxTier);
            float colMult = 1f;
            for (int t = 0; t < tier; t++) colMult *= ColumnsGrowthPerTier;
            float ewMult = 1f;
            for (int t = 0; t < tier; t++) ewMult *= EWStreetsGrowthPerTier;

            int columns = Math.Max(1, (int)Math.Round(BaseColumns.Sample(rng) * colMult));
            int ewStreets = Math.Max(2, (int)Math.Round(BaseEWStreets.Sample(rng) * ewMult));

            // Cap at reasonable maximums
            columns = Math.Min(columns, 15);
            ewStreets = Math.Min(ewStreets, 8);

            return (columns, ewStreets);
        }

        /// <summary>Compute commercial pads for a tier.</summary>
        public int ComputeCommercialPads(int tier, int estimatedPopulation, Rng rng)
        {
            float basePads = estimatedPopulation / 1000f * CommercialPadsPer1000Pop;
            // Add tier-based base
            float tierBase = tier switch { 0 => 1, 1 => 3, 2 => 6, 3 => 10, _ => 0 };
            return Math.Max(1, (int)Math.Round(basePads + tierBase));
        }

        /// <summary>Compute civic anchors for a tier.</summary>
        public int ComputeCivicAnchors(int tier, int estimatedPopulation, Rng rng)
        {
            int count = 0;
            if (estimatedPopulation >= CivicChurchMinPop) count += rng.RangeInt(1, 2); // 1-2 churches
            if (estimatedPopulation >= CivicSchoolMinPop) count += 1; // school
            if (estimatedPopulation >= CivicParkMinPop) count += 1; // park
            // Tier bonuses
            if (tier >= 2) count += 1; // extra civic at village+
            if (tier >= 3) count += 1; // extra at town
            return count;
        }

        /// <summary>Compute dead-end streets for a tier.</summary>
        public int ComputeDeadEnds(int tier, Rng rng)
        {
            return tier switch { 0 => 1, 1 => 1, 2 => 1, 3 => rng.Chance(0.5f) ? 2 : 1, _ => 1 };
        }

        /// <summary>Get the tier label.</summary>
        public string GetLabel(int tier) => tier switch
        {
            0 => "CROSSROADS",
            1 => "HAMLET",
            2 => "VILLAGE",
            3 => "TOWN",
            _ => $"TIER{tier}",
        };


        /// <summary>Get the class enum for a tier.</summary>
        public TownClass GetClass(int tier) => tier switch
        {
            0 => TownClass.Crossroads,
            1 => TownClass.Hamlet,
            2 => TownClass.Village,
            3 => TownClass.Town,
            _ => TownClass.Crossroads,
        };

        public void Validate() { }
    }
}