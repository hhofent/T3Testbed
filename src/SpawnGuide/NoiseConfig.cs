using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// All noise and natural system parameters extracted from WorldConfig.
    /// Controls: hills (fBm), contours, rivers, rail, roads, towns, farms.
    /// </summary>
    public sealed class NoiseConfig
    {
        // =========================================================================
        // HILLS (fBm - fractal Brownian motion)
        // =========================================================================
        /// <summary>Number of octaves for fBm hill generation.</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Geological)]
        [Rationale("6 octaves gives detailed ridges without looking noisy. Match Great Plains swell character.")]
        public int HillsOctaves { get; set; } = 6;

        /// <summary>Persistence (amplitude decay per octave) for hills fBm.</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Geological)]
        [Rationale("0.45 gives moderate roughness - lower than terrain (0.5) for softer Iowa swells.")]
        public float HillsPersistence { get; set; } = 0.45f;

        /// <summary>Lacunarity (frequency multiplier per octave) for hills fBm.</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Geological)]
        [Rationale("2.1 slightly stretches octaves vs standard 2.0, avoiding visible grid artifacts.")]
        public float HillsLacunarity { get; set; } = 2.1f;

        /// <summary>Base wavelength (feet) for the first octave of hills.</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Geological), Units("feet")]
        [Rationale("12000 ft (~2.3 mi) - long wavelength for broad glacial swells, not hills.")]
        public float HillsBaseWavelengthFt { get; set; } = 12000f;

        /// <summary>Total amplitude (feet) of the combined fBm hill field.</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Geological), Units("feet")]
        [Rationale("35 ft total relief - matches NW Iowa's subtle glacial topography.")]
        public float HillsAmplitudeFt { get; set; } = 35f;

        /// <summary>Domain warp amplitude (feet) for hills - warps the input coordinates before sampling.</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Geological), Units("feet")]
        [Rationale("800 ft warp creates natural-looking distortion of the swell pattern.")]
        public float HillsDomainWarpAmp { get; set; } = 800f;

        /// <summary>Number of octaves for domain warp noise.</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Geological)]
        [Rationale("3 octaves of warp - enough to break up regularity without chaos.")]
        public int HillsDomainWarpOctaves { get; set; } = 3;

        // =========================================================================
        // CONTOURS
        // =========================================================================
        /// <summary>Contour line interval in feet.</summary>
        [Category(CategoryAttribute.Concern.Visual), Category(CategoryAttribute.Concern.Geological), Units("feet")]
        [Rationale("20 ft intervals standard for USGS topo in this terrain.")]
        public float ContourIntervalFt { get; set; } = 20f;

        /// <summary>Lattice cell size (feet) for marching squares contour extraction.</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Geological), Units("feet")]
        [Rationale("1320 ft = 1/4 mile section grid alignment.")]
        public float ContourLatticeCellFt { get; set; } = 1320f;

        /// <summary>Draw index contour every N lines (5 = every 100 ft with 20 ft interval).</summary>
        [Category(CategoryAttribute.Concern.Visual)]
        [Rationale("Index contours every 5th line (100 ft) for readability.")]
        public int ContourIndexEvery { get; set; } = 5;

        // =========================================================================
        // RIVERS
        // =========================================================================
        /// <summary>Trunk river spacing distribution (LogNormal: μ=28mi, σ=6mi).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Geological), Units("miles")]
        [Rationale("LogNormal trunk spacing: major rivers every ~28 miles on average, skewed.")]
        public FloatDistribution TrunkRiverSpacingMi { get; set; } = FloatDistribution.LogNormal(28f, 6f, 10f, 80f);

        /// <summary>Perlin meander octaves for trunk rivers.</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Geological)]
        [Rationale("4 octaves of meander detail - major bends to small wiggles.")]
        public int RiverMeanderOctaves { get; set; } = 4;

        /// <summary>Base meander amplitude (feet) for trunk rivers.</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Geological), Units("feet")]
        [Rationale("1200 ft base meander - major river course deviations.")]
        public float RiverMeanderBaseAmpFt { get; set; } = 1200f;

        /// <summary>Base meander wavelength (feet) for trunk rivers.</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Geological), Units("feet")]
        [Rationale("8000 ft wavelength - meander bends every ~1.5 miles.")]
        public float RiverMeanderBaseWavelengthFt { get; set; } = 8000f;

        /// <summary>Persistence for river meander fBm.</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Geological)]
        [Rationale("0.5 standard persistence for natural meander self-similarity.")]
        public float RiverMeanderPersistence { get; set; } = 0.5f;

        /// <summary>Lacunarity for river meander fBm.</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Geological)]
        [Rationale("2.0 standard lacunarity.")]
        public float RiverMeanderLacunarity { get; set; } = 2.0f;

        /// <summary>Meander amplitude per channel width (feet).</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Geological), Units("feet")]
        [Rationale("15 ft per width unit - wider rivers meander more.")]
        public float RiverMeanderAmpPerWidth { get; set; } = 15f;

        // Tributary parameters
        /// <summary>Tributary spacing (miles) along trunk river.</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Geological), Units("miles")]
        [Rationale("8 miles between tributary heads - Strahler order 1-2 streams.")]
        public FloatDistribution TributarySpacingMi { get; set; } = FloatDistribution.LogNormal(8f, 2f, 3f, 20f);

        /// <summary>Tributary length base (miles).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Geological), Units("miles")]
        [Rationale("2 miles base length for tributaries.")]
        public FloatDistribution TributaryLengthBaseMi { get; set; } = FloatDistribution.LogNormal(2f, 0.5f, 0.5f, 10f);

        /// <summary>Tributary length jitter (miles).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Geological), Units("miles")]
        [Rationale("2.4 miles jitter - highly variable tributary lengths.")]
        public FloatDistribution TributaryLengthJitterMi { get; set; } = FloatDistribution.LogNormal(2.4f, 0.8f, 0.1f, 8f);

        /// <summary>Tributary head jitter from regular spacing (miles).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Geological), Units("miles")]
        public FloatDistribution TributaryHeadJitterMi { get; set; } = FloatDistribution.LogNormal(2.4f, 0.8f, 0.1f, 8f);

        /// <summary>Gap from trunk river to tributary head (miles).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Geological), Units("miles")]
        public FloatDistribution TributaryGapFromTrunkMi { get; set; } = FloatDistribution.LogNormal(2.6f, 0.5f, 0.5f, 10f);

        /// <summary>Gap jitter (miles).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Geological), Units("miles")]
        public FloatDistribution TributaryGapJitterMi { get; set; } = FloatDistribution.LogNormal(5.4f, 1.5f, 1f, 20f);

        /// <summary>Tributary bow amplitude (feet) - lateral excursion.</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Geological), Units("feet")]
        [Rationale("800 ft bow - tributaries curve back toward trunk.")]
        public float TributaryBowAmpFt { get; set; } = 800f;

        /// <summary>Tributary channel width (feet).</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Geological), Units("feet")]
        public float TributaryWidthFt { get; set; } = 16f;

        /// <summary>Trunk river bank tree chance (0-1).</summary>
        [Category(CategoryAttribute.Concern.Vegetation), Category(CategoryAttribute.Concern.Geological)]
        [Rationale("46% chance per sample for mature trees on major river banks.")]
        public float RiverBankTreeChance { get; set; } = 0.46f;

        /// <summary>Trunk river bank tree offset from channel (feet).</summary>
        [Category(CategoryAttribute.Concern.Vegetation), Category(CategoryAttribute.Concern.Geological), Units("feet")]
        public FloatDistribution RiverBankTreeOffsetFt { get; set; } = FloatDistribution.Uniform(14f, 104f);

        /// <summary>Tributary bank tree chance (0-1).</summary>
        [Category(CategoryAttribute.Concern.Vegetation), Category(CategoryAttribute.Concern.Geological)]
        [Rationale("34% chance - smaller streams have less riparian forest.")]
        public float TributaryBankTreeChance { get; set; } = 0.34f;

        /// <summary>Tributary bank tree offset from channel (feet).</summary>
        [Category(CategoryAttribute.Concern.Vegetation), Category(CategoryAttribute.Concern.Geological), Units("feet")]
        public FloatDistribution TributaryBankTreeOffsetFt { get; set; } = FloatDistribution.Uniform(12f, 72f);

        /// <summary>Distance (feet) from river to veto town placement.</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Geological), Units("feet")]
        [Rationale("1200 ft veto - towns don't sit in floodways.")]
        public float RiverVetoDistanceFt { get; set; } = 1200f;

        // =========================================================================
        // RAIL
        // =========================================================================
        /// <summary>Rail line spacing (miles) - major lines every ~75 miles.</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Infrastructure), Units("miles")]
        [Rationale("75 mile spacing matches historical Iowa railroad grid.")]
        public FloatDistribution RailSpacingMi { get; set; } = FloatDistribution.Normal(75f, 5f, 50f, 100f);

        /// <summary>Phase offset (sections) to avoid rail/paved corridor alignment.</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Infrastructure), Units("sections")]
        [Rationale("3 sections (3 miles) phase prevents 75-mile parallel runs with highways.")]
        public float RailPhaseSections { get; set; } = 3f;

        /// <summary>Rail offset from its section line base (feet).</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("40 ft base offset + jitter puts rail beside, not on, the section road.")]
        public FloatDistribution RailOffsetBaseFt { get; set; } = FloatDistribution.Uniform(40f, 210f);

        /// <summary>Rail offset jitter (feet) - already included in RailOffsetBaseFt range.</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("Handled by RailOffsetBaseFt range.")]
        [Deprecated("Use RailOffsetBaseFt range", "RailOffsetJitterFt")]
        public float RailOffsetJitterFt { get; set; } = 170f;

        // Rail swell (fBm) - one-sided vertical displacement
        /// <summary>Base swell amplitude (feet).</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("50 ft base swell - gentle grade undulations.")]
        public float RailSwellBaseAmpFt { get; set; } = 50f;

        /// <summary>Swell amplitude jitter (feet).</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("150 ft jitter - some lines much hillier than others.")]
        public float RailSwellAmpJitterFt { get; set; } = 150f;

        /// <summary>Base swell wavelength (feet).</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("8000 ft wavelength - long gradual grade changes.")]
        public float RailSwellBaseWavelengthFt { get; set; } = 8000f;

        /// <summary>Swell wavelength jitter (feet).</summary>
        [Category(CategoryAttribute.Concern.Noise), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("10000 ft jitter - variable swell scales.")]
        public float RailSwellWavelengthJitterFt { get; set; } = 10000f;

        // Rail spurs (branch lines to towns)
        /// <summary>Maximum distance (miles) from rail for a town to get a spur.</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Infrastructure), Units("miles")]
        [Rationale("5.5 miles - branch lines only served nearby towns.")]
        public float RailSpurMaxDistanceMi { get; set; } = 5.5f;

        /// <summary>Spur offset from town center (feet).</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        public FloatDistribution RailSpurOffsetBaseFt { get; set; } = FloatDistribution.Uniform(100f, 280f);

        /// <summary>Spur offset jitter (feet).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("Handled by RailSpurOffsetBaseFt range.")]
        [Deprecated("Use RailSpurOffsetBaseFt range", "RailSpurOffsetJitterFt")]
        public float RailSpurOffsetJitterFt { get; set; } = 180f;

        /// <summary>Chance spur goes on north vs south side (0.5 = equal).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("50/50 side chance - no directional bias.")]
        public float RailSpurSideChance { get; set; } = 0.5f;

        /// <summary>Whether spurs are always perpendicular to main line.</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("True - branch lines left main at right angles for grade crossings.")]
        public bool RailSpurAlwaysPerpendicular { get; set; } = true;

        /// <summary>Whether every spur town gets a grain elevator.</summary>
        [Category(CategoryAttribute.Concern.Infrastructure), Category(CategoryAttribute.Concern.Commercial)]
        [Rationale("True - elevator is the reason the spur exists.")]
        public bool RailSpurAlwaysHasElevator { get; set; } = true;

        // =========================================================================
        // ROADS
        // =========================================================================
        /// <summary>Paved road corridor spacing (miles).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Infrastructure), Units("miles")]
        [Rationale("10 miles - county highway grid spacing.")]
        public float PavedSpacingMi { get; set; } = 10f;

        /// <summary>Chance a mile road line is absent (1.8%).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("1.8% - rare missing section lines (water, rough terrain).")]
        public float RoadLineAbsentChance { get; set; } = 0.018f;

        /// <summary>Chance a gravel mile road is paved (2.2%).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("2.2% - occasional paved shortcuts.")]
        public float RoadLinePavedChance { get; set; } = 0.022f;

        /// <summary>Margin (feet) from farmstead to road.</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Infrastructure), Units("feet")]
        [Rationale("300 ft - farmsteads set back from road right-of-way.")]
        public float FarmMarginFt { get; set; } = 300f;

        // =========================================================================
        // TOWNS
        // =========================================================================
        /// <summary>Maximum distance (miles) from rail for town placement.</summary>
        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Infrastructure), Units("miles")]
        [Rationale("9 miles - towns only form near rail in this era.")]
        public float TownRailProximityMi { get; set; } = 9f;

        /// <summary>Town layout weights: North, South, Both (Dirichlet concentrations).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("Dirichlet(0.46, 0.24, 0.30) - north-side favored (historical), both common.")]
        public FloatDistribution TownLayoutWeights { get; set; } = FloatDistribution.Dirichlet(0.46f, 0.24f, 0.30f);

        /// <summary>Town class weights: Crossroads, Hamlet, Village, Town (Dirichlet).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("Dirichlet(0.315, 0.185, 0.09, 0.045) - k=3 central place hierarchy.")]
        public FloatDistribution TownClassWeights { get; set; } = FloatDistribution.Dirichlet(0.315f, 0.185f, 0.09f, 0.045f);

        /// <summary>Town slot jitter as fraction of slot spacing (3.5%).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("3.5% jitter - towns don't sit perfectly on the 10-mile lattice.")]
        public float TownSlotJitterFraction { get; set; } = 0.035f;

        /// <summary>Minimum center-to-center spacing between inhabited town sites, in miles.</summary>
        [Category(CategoryAttribute.Concern.Density), Category(CategoryAttribute.Concern.Placement), Units("miles")]
        [Rationale("A 20-mile floor preserves isolated Iowa communities while allowing much longer spacing between most towns.")]
        public float TownMinSpacingMi { get; set; } = 20f;

        // =========================================================================
        // FARMS
        // =========================================================================
        /// <summary>Maximum farmsteads per section (640 acres).</summary>
        [Category(CategoryAttribute.Concern.Density), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("Max 3 per section - physical limit on quarter-quarter sites.")]
        public int FarmsPerSectionMax { get; set; } = 3;

        /// <summary>Farm perpendicular jitter from section line (feet, Normal).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Placement), Units("feet")]
        [Rationale("Normal(0, 250) - farmsteads cluster near the road but vary.")]
        public FloatDistribution FarmSectionJitterFt { get; set; } = FloatDistribution.Normal(0f, 250f, -500f, 500f);

        /// <summary>Farm along-road jitter (feet, LogNormal).</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Placement), Units("feet")]
        [Rationale("LogNormal(200, 300) - skewed along-road spacing.")]
        public FloatDistribution FarmAlongJitterFt { get; set; } = FloatDistribution.LogNormal(5.3f, 0.6f, 50f, 2000f); // log(200)≈5.3

        /// <summary>Mean farmsteads per 640-acre section; Poisson-sampled (NW Iowa baseline).</summary>
        [Category(CategoryAttribute.Concern.Density), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("A 0.15 mean per section yields sparse, deterministic farmstead placement in the 1990s snapshot.")]
        public float FarmDensity { get; set; } = 0.15f;

        public void Validate() { }
    }
}
