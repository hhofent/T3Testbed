using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// Root configuration for all procedural generation parameters.
    /// Load from JSON with <see cref="Load(string)"/> or create defaults with <see cref="Default"/>.
    /// Every field has a detailed description of what it controls.
    /// Schema version 2: adds Distributions, Curves, Expressions, Enums, ConditionalRules, Profiles.
    /// </summary>
    public sealed class SpawnConfig
    {
        // =========================================================================
        // CORE CONFIG SECTIONS
        // =========================================================================
        public HouseConfig Houses { get; set; } = new();
        public RuralConfig Rural { get; set; } = new();
        public TownBlockConfig TownBlock { get; set; } = new();
        public SmallTownConfig SmallTown { get; set; } = new();
        public WorldConfig World { get; set; } = new();
        public CountrysideConfig Countryside { get; set; } = new();
        public RenderConfig Render { get; set; } = new();

        // =========================================================================
        // NEW PHASE 1 CONFIG SECTIONS
        // =========================================================================
        /// <summary>Noise and natural system parameters (hills, rivers, rail, roads, towns, farms).</summary>
        public NoiseConfig Noise { get; set; } = new();

        /// <summary>Generative town tier system (Central Place Theory k=3).</summary>
        public TownTierSystem TownTiers { get; set; } = new();

        /// <summary>Town extra feature definitions (feature grammar).</summary>
        public List<TownExtraDef> TownExtras { get; set; } = TownExtraGrammar.GetDefaultPool();

        /// <summary>Highway commercial and rail industrial configuration.</summary>
        public CommercialConfig Commercial { get; set; } = new();

        /// <summary>1990s residential lot and house standards.</summary>
        public ResidentialConfig Residential { get; set; } = new();

        /// <summary>Iowa standard street and alley configuration.</summary>
        public StreetConfig Streets { get; set; } = new();

        /// <summary>Farmstead evolution model (homestead → 1990s).</summary>
        public FarmsteadConfig Farmsteads { get; set; } = new();

        /// <summary>House archetype distributions.</summary>
        public HouseArchetypeConfig HouseArchetypes { get; set; } = new();

        /// <summary>Coop corner archetype (rail + section corner).</summary>
        public CoopCornerConfig CoopCorners { get; set; } = new();

        /// <summary>Conditional correlation rules (If X Then Y).</summary>
        public List<ConditionalRule> ConditionalRules { get; set; } = new(DefaultConditionalRules);

        private static readonly ConditionalRule[] DefaultConditionalRules = new[]
        {
            // Higher tier towns get more civic density
            new ConditionalRule { IfParameter = "SmallTown.Tier", Operator = RuleOperator.GreaterThanOrEqual, Value = "2",
                ThenParameter = "SmallTown.CivicDensity", ThenOperation = RuleOperation.Multiply, ThenValue = 1.5f },

            // Straddling towns (Both layout) get more residential
            new ConditionalRule { IfParameter = "World.TownLayout", Operator = RuleOperator.Equal, Value = "Both",
                ThenParameter = "SmallTown.ResidentialDensity", ThenOperation = RuleOperation.Multiply, ThenValue = 1.2f },

            // Large rural lots -> fewer outbuildings
            new ConditionalRule { IfParameter = "Rural.LotWidth", Operator = RuleOperator.GreaterThan, Value = "250",
                ThenParameter = "Rural.OutbuildingDensity", ThenOperation = RuleOperation.Multiply, ThenValue = 0.8f },

            // High farm density -> smaller farmsteads
            new ConditionalRule { IfParameter = "World.FarmDensity", Operator = RuleOperator.GreaterThan, Value = "0.2",
                ThenParameter = "Rural.LotWidth", ThenOperation = RuleOperation.Multiply, ThenValue = 0.9f,
                ClampMin = 150, ClampMax = 350 },

            // Rail towns get industrial boost
            new ConditionalRule { IfParameter = "World.TownRailProximityMiles", Operator = RuleOperator.LessThanOrEqual, Value = "5",
                ThenParameter = "SmallTown.IndustrialDensity", ThenOperation = RuleOperation.Multiply, ThenValue = 1.3f },

            // High commercial density -> wider commercial band
            new ConditionalRule { IfParameter = "SmallTown.CommercialDensity", Operator = RuleOperator.GreaterThan, Value = "1.5",
                ThenParameter = "SmallTown.BandDepth", ThenOperation = RuleOperation.Multiply, ThenValue = 1.1f,
                ClampMax = 300 },
        };

        // =========================================================================
        // SCHEMA & MIGRATION
        // =========================================================================
        /// <summary>Current schema version. Increment when adding/removing fields or changing types.</summary>
        public int SchemaVersion { get; set; } = 2;

        /// <summary>Load configuration from a JSON file. Returns defaults if file doesn't exist.
        /// Automatically migrates from older schema versions and uses binary cache for speed.</summary>
        public static SpawnConfig Load(string path)
        {
            return ConfigSerialization.Load(path);
        }

        /// <summary>Save configuration to a JSON file (creates directory if needed).</summary>
        public void Save(string path)
        {
            ConfigSerialization.Save(this, path);
        }

        /// <summary>Create a default configuration file at the given path if it doesn't exist.</summary>
        public static void CreateDefaultIfMissing(string path)
        {
            if (!File.Exists(path)) Default.Save(path);
        }

        /// <summary>Validate all ranges and throw if any are invalid.</summary>
        public void Validate()
        {
            var results = ConfigValidator.Validate(this, applyRepairs: false);
            var errors = results.FindAll(r => r.Severity == ValidationSeverity.Error);
            if (errors.Count > 0)
            {
                throw new ArgumentException("Config validation failed:\n" + string.Join("\n", errors.ConvertAll(e => $"  {e.ParameterPath}: {e.Message}")));
            }
            // Also validate legacy sections for backward compat
            Houses.Validate();
            Rural.Validate();
            TownBlock.Validate();
            SmallTown.Validate();
            World.Validate();
            Countryside.Validate();
            Render.Validate();
            Noise.Validate();
            TownTiers.Validate();
            Commercial.Validate();
            Residential.Validate();
            Streets.Validate();
            Farmsteads.Validate();
            HouseArchetypes.Validate();
            CoopCorners.Validate();
        }

        /// <summary>Validate and auto-repair (clamps, swaps). Returns true if valid after repairs.</summary>
        public bool ValidateAndRepair()
        {
            return ConfigValidator.ValidateAndRepair(this, out _);
        }

        /// <summary>Migrate this config from an older schema version to current.</summary>
        public SpawnConfig Migrate(int fromVersion)
        {
            return ConfigSerialization.Migrate(this, fromVersion);
        }

        /// <summary>Get the default configuration (baked-in values matching current generator behavior).</summary>
        public static SpawnConfig Default => new();

        /// <summary>Create a configuration with all spawn rates multiplied by a factor (for testing density).</summary>
        public static SpawnConfig WithDensityMultiplier(float multiplier)
        {
            return WithDensityMultiplier(Default, multiplier);
        }

        /// <summary>Apply density multiplier to an existing configuration.</summary>
        public static SpawnConfig WithDensityMultiplier(SpawnConfig cfg, float multiplier)
        {
            cfg.Rural.OutbuildingDensity *= multiplier;
            cfg.TownBlock.HouseDensity *= multiplier;
            cfg.SmallTown.ResidentialDensity *= multiplier;
            cfg.SmallTown.CommercialDensity *= multiplier;
            cfg.SmallTown.IndustrialDensity *= multiplier;
            cfg.SmallTown.CivicDensity *= multiplier;
            cfg.SmallTown.LandmarkDensity *= multiplier;
            cfg.World.FarmDensity *= multiplier;
            cfg.World.TownDensity *= multiplier;
            cfg.Countryside.FarmsteadDensity *= multiplier;
            cfg.Countryside.FieldFeatureDensity *= multiplier;
            // New configs
            cfg.Noise.FarmDensity *= multiplier;
            cfg.Residential.DensityMultiplier = multiplier; // We'll add this
            cfg.Farmsteads.DensityMultiplier = multiplier; // We'll add this
            return cfg;
        }

        /// <summary>Apply a built-in profile by name.</summary>
        public static SpawnConfig WithProfile(string profileName)
        {
            return ConfigProfiles.ApplyProfile(profileName);
        }

        /// <summary>Apply conditional rules to this config.</summary>
        public void ApplyConditionalRules()
        {
            ConfigRuleEngine.ApplyRules(this, ConditionalRules);
        }
    }

    // =========================================================================
    // HOUSE GENERATION (updated with distributions)
    // =========================================================================

    /// <summary>Parameters controlling house archetype generation.</summary>
    public sealed class HouseConfig
    {
        // --- Ranch ---
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(3.7, 0.1) ≈ 40ft - typical ranch width.")]
        [JsonPropertyName("ranchWidthFt")] public FloatDistribution RanchWidth { get; set; } = FloatDistribution.LogNormal(3.7f, 0.1f, 30f, 55f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(3.2, 0.1) ≈ 25ft - shallow ranch depth.")]
        [JsonPropertyName("ranchDepthFt")] public FloatDistribution RanchDepth { get; set; } = FloatDistribution.LogNormal(3.2f, 0.1f, 20f, 35f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(22, 3) - standard 2-car garage width.")]
        [JsonPropertyName("ranchGarageWidthFt")] public FloatDistribution RanchGarageWidth { get; set; } = FloatDistribution.Normal(22f, 3f, 16f, 30f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(22, 2) - standard garage depth.")]
        [JsonPropertyName("ranchGarageDepthFt")] public FloatDistribution RanchGarageDepth { get; set; } = FloatDistribution.Normal(22f, 2f, 18f, 26f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("50/50 - no side preference.")]
        [JsonPropertyName("ranchGarageLeftChance")] public float RanchGarageLeftChance { get; set; } = 0.5f;

        // --- Farmhouse ---
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(3.4, 0.1) ≈ 30ft - narrow traditional form.")]
        [JsonPropertyName("farmhouseWidthFt")] public FloatDistribution FarmhouseWidth { get; set; } = FloatDistribution.LogNormal(3.4f, 0.1f, 24f, 40f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(3.5, 0.1) ≈ 33ft - deeper than wide.")]
        [JsonPropertyName("farmhouseDepthFt")] public FloatDistribution FarmhouseDepth { get; set; } = FloatDistribution.LogNormal(3.5f, 0.1f, 26f, 44f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Beta(5,3, 0.5, 0.8) - porch spans 50-80% of front.")]
        [JsonPropertyName("farmhousePorchWidthFraction")] public FloatDistribution FarmhousePorchWidthFraction { get; set; } = FloatDistribution.Beta(5f, 3f, 0.5f, 0.8f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(7, 1) - usable porch depth.")]
        [JsonPropertyName("farmhousePorchDepthFt")] public FloatDistribution FarmhousePorchDepth { get; set; } = FloatDistribution.Normal(7f, 1f, 5f, 10f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("90% - traditional centered door.")]
        [JsonPropertyName("farmhouseDoorCenteredChance")] public float FarmhouseDoorCenteredChance { get; set; } = 0.9f;

        // --- Split-Level ---
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(3.4, 0.1) ≈ 30ft - compact footprint.")]
        [JsonPropertyName("splitLevelWidthFt")] public FloatDistribution SplitLevelWidth { get; set; } = FloatDistribution.LogNormal(3.4f, 0.1f, 24f, 40f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(3.4, 0.1) ≈ 30ft - roughly square.")]
        [JsonPropertyName("splitLevelDepthFt")] public FloatDistribution SplitLevelDepth { get; set; } = FloatDistribution.LogNormal(3.4f, 0.1f, 24f, 40f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(16, 2) - single-car integral garage typical.")]
        [JsonPropertyName("splitLevelGarageWidthFt")] public FloatDistribution SplitLevelGarageWidth { get; set; } = FloatDistribution.Normal(16f, 2f, 12f, 22f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(20, 2) - garage under living space.")]
        [JsonPropertyName("splitLevelGarageDepthFt")] public FloatDistribution SplitLevelGarageDepth { get; set; } = FloatDistribution.Normal(20f, 2f, 16f, 26f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [JsonPropertyName("splitLevelGarageLeftChance")] public float SplitLevelGarageLeftChance { get; set; } = 0.5f;

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("90% - split-levels usually have side-entry garage.")]
        [JsonPropertyName("splitLevelGarageDoorOnSideChance")] public float SplitLevelGarageDoorOnSideChance { get; set; } = 0.9f;

        // --- Shared ---
        [Category(CategoryAttribute.Concern.Visual), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Market order: White/Ivory/Beige vinyl dominant, brick front legacy, wood/aluminum rare.")]
        [JsonPropertyName("sidingOptions")] public SidingType[] SidingOptions { get; set; } = new[]
        {
            SidingType.WhiteVinyl, SidingType.IvoryVinyl, SidingType.BeigeVinyl,
            SidingType.LightGrayVinyl, SidingType.BrickFrontVinylSides,
            SidingType.WoodLap, SidingType.Aluminum,
        };

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Beta(2,5, 0.15, 0.35) - door biased toward garage side.")]
        [JsonPropertyName("doorOffsetFractionRange")] public FloatDistribution DoorOffsetFraction { get; set; } = FloatDistribution.Beta(2f, 5f, 0.15f, 0.35f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Visual)]
        [Rationale("Beta(2,5, 0, 0.08) - windows nearly aligned with slight variance.")]
        [JsonPropertyName("frontWindowJitterFraction")] public FloatDistribution FrontWindowJitterFraction { get; set; } = FloatDistribution.Beta(2f, 5f, 0f, 0.08f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Visual)]
        [Rationale("Beta(2,5, 0, 0.06) - side windows tighter.")]
        [JsonPropertyName("sideWindowJitterFraction")] public FloatDistribution SideWindowJitterFraction { get; set; } = FloatDistribution.Beta(2f, 5f, 0f, 0.06f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Bimodal(9ft:65%, 16ft:35%) - single vs double door.")]
        [JsonPropertyName("garageDoorWidthFt")] public FloatDistribution GarageDoorWidthFt { get; set; } = FloatDistribution.Bimodal(9f, 16f, 0.65f);

        /// <summary>Minimum wall length (ft) to place 2 windows instead of 1.</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(22, 2) - need ~22ft for two 3ft windows + trim.")]
        [JsonPropertyName("twoWindowMinWallFt")] public FloatDistribution TwoWindowMinWall { get; set; } = FloatDistribution.Normal(22f, 2f, 18f, 28f);

        /// <summary>Minimum wall length (ft) to place 1 window.</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(9, 1) - need ~9ft for one 3ft window + trim.")]
        [JsonPropertyName("oneWindowMinWallFt")] public FloatDistribution OneWindowMinWall { get; set; } = FloatDistribution.Normal(9f, 1f, 7f, 12f);

        // Legacy garage mode chances (used by town generators)
        [JsonPropertyName("garageModeIntegralChance")] public float GarageModeIntegralChance { get; set; } = 0.55f;
        [JsonPropertyName("garageModeDetachedChance")] public float GarageModeDetachedChance { get; set; } = 0.35f;
        [JsonPropertyName("garageModeNoneChance")] public float GarageModeNoneChance { get; set; } = 0.10f;

        public void Validate()
        {
            // Validate distributions
            foreach (var prop in GetType().GetProperties())
            {
                if (prop.PropertyType == typeof(FloatDistribution))
                {
                    var fd = (FloatDistribution)prop.GetValue(this);
                    // Basic validation happens in ConfigValidator
                }
            }
        }
    }

    // =========================================================================
    // RURAL FARMSTEAD LOT (updated with distributions)
    // =========================================================================

    /// <summary>Parameters controlling the rural farmstead lot (section-line county road, house, machine shed, bins, etc.).</summary>
    public sealed class RuralConfig
    {
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(5.5, 0.1) ≈ 245ft - quarter-quarter section frontage.")]
        [JsonPropertyName("lotWidthFt")] public FloatDistribution LotWidth { get; set; } = FloatDistribution.LogNormal(5.5f, 0.1f, 180f, 350f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(5.8, 0.1) ≈ 330ft - deep lot for outbuildings.")]
        [JsonPropertyName("lotDepthFt")] public FloatDistribution LotDepth { get; set; } = FloatDistribution.LogNormal(5.8f, 0.1f, 250f, 500f);

        [Category(CategoryAttribute.Concern.Placement), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Uniform(0.22, 0.38) - house in front 1/3 of lot.")]
        [JsonPropertyName("houseXFraction")] public FloatDistribution HouseXFraction { get; set; } = FloatDistribution.Uniform(0.22f, 0.38f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(50, 10) - farmsteads set back further than town lots.")]
        [JsonPropertyName("houseSetbackFt")] public FloatDistribution HouseSetback { get; set; } = FloatDistribution.Normal(50f, 10f, 30f, 80f);

        // Machine shed
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Industrial)]
        [Rationale("LogNormal(3.7, 0.15) ≈ 40ft - fits 1990s equipment.")]
        [JsonPropertyName("machineShedWidthFt")] public FloatDistribution MachineShedWidth { get; set; } = FloatDistribution.LogNormal(3.7f, 0.15f, 30f, 60f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Industrial)]
        [Rationale("LogNormal(3.5, 0.15) ≈ 33ft - equipment parking depth.")]
        [JsonPropertyName("machineShedDepthFt")] public FloatDistribution MachineShedDepth { get; set; } = FloatDistribution.LogNormal(3.5f, 0.15f, 24f, 50f);

        [JsonPropertyName("machineShedXFractionRight")] public FloatDistribution MachineShedXFractionRight { get; set; } = FloatDistribution.Uniform(0.06f, 0.18f);
        [JsonPropertyName("machineShedXFractionLeft")] public FloatDistribution MachineShedXFractionLeft { get; set; } = FloatDistribution.Uniform(0.08f, 0.20f);
        [JsonPropertyName("machineShedBehindHouseFt")] public float MachineShedBehindHouse { get; set; } = 26f;

        // Driveway
        [JsonPropertyName("drivewayEntrySideMarginFt")] public float DrivewayEntrySideMargin { get; set; } = 15f;
        [JsonPropertyName("drivewayYardDepthFt")] public float DrivewayYardDepth { get; set; } = 20f;
        [JsonPropertyName("drivewayWidthFt")] public FloatDistribution DrivewayWidth { get; set; } = FloatDistribution.Normal(14f, 2f, 10f, 20f);
        [JsonPropertyName("drivewaySurface")] public SurfaceType DrivewaySurface { get; set; } = SurfaceType.Gravel;

        // Walk
        [JsonPropertyName("walkWidthFt")] public FloatDistribution WalkWidth { get; set; } = FloatDistribution.Normal(3.5f, 0.5f, 2.5f, 5f);
        [JsonPropertyName("walkSurface")] public SurfaceType WalkSurface { get; set; } = SurfaceType.Concrete;
        [JsonPropertyName("walkOffsetFromLaneFt")] public float WalkOffsetFromLane { get; set; } = 7f;

        // Shop / lean-to
        [JsonPropertyName("shopChance")] public float ShopChance { get; set; } = 0.4f;
        [JsonPropertyName("shopWidthFt")] public FloatDistribution ShopWidth { get; set; } = FloatDistribution.Normal(22f, 3f, 16f, 30f);
        [JsonPropertyName("shopDepthFt")] public FloatDistribution ShopDepth { get; set; } = FloatDistribution.Normal(28f, 4f, 20f, 40f);
        [JsonPropertyName("shopOffsetFromShedFt")] public float ShopOffsetFromShed { get; set; } = 10f;

        // Steel bins
        [JsonPropertyName("binCountRange")] public IntDistribution BinCount { get; set; } = new IntDistribution(FloatDistribution.Poisson(3f) + 2f);
        [JsonPropertyName("binDiameterOptionsFt")] public FloatDistribution BinDiameter { get; set; } = FloatDistribution.LogNormal(3.0f, 0.1f, 15f, 30f);
        [JsonPropertyName("binSpacingExtraFt")] public float BinSpacingExtra { get; set; } = 8f;
        [JsonPropertyName("binRowBehindShedFt")] public float BinRowBehindShed { get; set; } = 8f;

        // Pole barn
        [JsonPropertyName("poleBarnChance")] public float PoleBarnChance { get; set; } = 0.4f;
        [JsonPropertyName("poleBarnWidthFt")] public FloatDistribution PoleBarnWidth { get; set; } = FloatDistribution.Normal(30f, 4f, 20f, 44f);
        [JsonPropertyName("poleBarnDepthFt")] public FloatDistribution PoleBarnDepth { get; set; } = FloatDistribution.Normal(48f, 6f, 32f, 72f);
        [JsonPropertyName("poleBarnBehindBinsFt")] public float PoleBarnBehindBins { get; set; } = 12f;
        [JsonPropertyName("poleBarnXJitterFt")] public FloatDistribution PoleBarnXJitter { get; set; } = FloatDistribution.Normal(0f, 30f, -60f, 60f);

        // Gambrel barn
        [JsonPropertyName("gambrelBarnChance")] public float GambrelBarnChance { get; set; } = 0.15f;
        [JsonPropertyName("gambrelBarnWidthFt")] public FloatDistribution GambrelBarnWidth { get; set; } = FloatDistribution.Normal(34f, 4f, 26f, 44f);
        [JsonPropertyName("gambrelBarnDepthFt")] public FloatDistribution GambrelBarnDepth { get; set; } = FloatDistribution.Normal(56f, 6f, 40f, 76f);
        [JsonPropertyName("gambrelBarnXFraction")] public FloatDistribution GambrelBarnXFraction { get; set; } = FloatDistribution.Uniform(0.60f, 0.80f);
        [JsonPropertyName("gambrelBarnFromBackFt")] public float GambrelBarnFromBack { get; set; } = 14f;

        // LP tank
        [JsonPropertyName("lpTankChance")] public float LpTankChance { get; set; } = 0.3f;
        [JsonPropertyName("lpTankOffsetFromHouseFt")] public float LpTankOffsetFromHouse { get; set; } = 8f;

        // Shelterbelt
        [JsonPropertyName("shelterbeltChance")] public float ShelterbeltChance { get; set; } = 0.6f;
        [JsonPropertyName("shelterbeltTreeSpacingFt")] public FloatDistribution ShelterbeltTreeSpacing { get; set; } = FloatDistribution.Normal(16f, 2f, 12f, 22f);
        [JsonPropertyName("shelterbeltTreeJitterFt")] public float ShelterbeltTreeJitter { get; set; } = 2f;
        [JsonPropertyName("shelterbeltNorthOffsetFt")] public float ShelterbeltNorthOffset { get; set; } = 8f;
        [JsonPropertyName("shelterbeltWestOffsetFt")] public float ShelterbeltWestOffset { get; set; } = 10f;
        [JsonPropertyName("shelterbeltTreeSpacingZ")] public float ShelterbeltTreeSpacingZ { get; set; } = 17f;
        [JsonPropertyName("shelterbeltRows")] public IntDistribution ShelterbeltRows { get; set; } = new IntDistribution(FloatDistribution.Poisson(3f) + 2f);

        // Detached garage (for farmhouse)
        [JsonPropertyName("detachedGarageWidthFt")] public FloatDistribution DetachedGarageWidth { get; set; } = FloatDistribution.Normal(24f, 3f, 18f, 32f);
        [JsonPropertyName("detachedGarageDepthFt")] public FloatDistribution DetachedGarageDepth { get; set; } = FloatDistribution.Normal(24f, 3f, 18f, 32f);
        [JsonPropertyName("detachedGarageSideOffsetFt")] public float DetachedGarageSideOffset { get; set; } = 16f;
        [JsonPropertyName("detachedGarageBehindHouseFt")] public float DetachedGarageBehindHouse { get; set; } = 4f;

        // Outbuilding density multiplier
        [JsonPropertyName("outbuildingDensity")] public float OutbuildingDensity { get; set; } = 1.0f;

        // Collision padding
        [JsonPropertyName("buildingCollisionPadFt")] public float BuildingCollisionPad { get; set; } = 3f;

        public void Validate() { /* ConfigValidator handles this */ }
    }

    // =========================================================================
    // TOWN BLOCK (updated with distributions)
    // =========================================================================

    /// <summary>Parameters controlling the town block generator (single street, 4 lots per side).</summary>
    public sealed class TownBlockConfig
    {
        [JsonPropertyName("streetRowFt")] public float StreetRow { get; set; } = 36f;
        [JsonPropertyName("alleyRowFt")] public float AlleyRow { get; set; } = 16f;

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [JsonPropertyName("lotDepthFt")] public FloatDistribution LotDepth { get; set; } = FloatDistribution.LogNormal(4.8f, 0.1f, 100f, 160f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Infrastructure)]
        [Rationale("Beta(2,2) - alleys cluster in groups.")]
        [JsonPropertyName("alleyChance")] public FloatDistribution AlleyChance { get; set; } = FloatDistribution.Beta(2f, 2f, 0f, 1f);

        [JsonPropertyName("lotCountPerSide")] public int LotCountPerSide { get; set; } = 4;

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(4.2, 0.15) ≈ 67ft - town lot frontage.")]
        [JsonPropertyName("lotFrontageFt")] public FloatDistribution LotFrontage { get; set; } = FloatDistribution.LogNormal(4.2f, 0.15f, 50f, 100f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(15, 5) - 15ft setback typical.")]
        [JsonPropertyName("setbackFt")] public FloatDistribution Setback { get; set; } = FloatDistribution.Normal(15f, 5f, 8f, 25f);

        [JsonPropertyName("houseXJitterFt")] public float HouseXJitter { get; set; } = 3f;

        // Garage
        [JsonPropertyName("garageViaAlleyChance")] public float GarageViaAlleyChance { get; set; } = 0.6f;
        [JsonPropertyName("detachedGarageWidthFt")] public FloatDistribution DetachedGarageWidth { get; set; } = FloatDistribution.Normal(22f, 3f, 16f, 30f);
        [JsonPropertyName("detachedGarageDepthFt")] public FloatDistribution DetachedGarageDepth { get; set; } = FloatDistribution.Normal(26f, 3f, 20f, 34f);
        [JsonPropertyName("detachedGarageXJitterFt")] public float DetachedGarageXJitter { get; set; } = 6f;
        [JsonPropertyName("detachedGarageFromAlleyFt")] public float DetachedGarageFromAlley { get; set; } = 9f;

        // Farmhouse detached garage
        [JsonPropertyName("farmhouseDetachedGarageWidthFt")] public FloatDistribution FarmhouseDetachedGarageWidth { get; set; } = FloatDistribution.Normal(20f, 3f, 16f, 26f);
        [JsonPropertyName("farmhouseDetachedGarageDepthFt")] public FloatDistribution FarmhouseDetachedGarageDepth { get; set; } = FloatDistribution.Normal(22f, 3f, 18f, 28f);
        [JsonPropertyName("farmhouseDetachedGarageSideMarginFt")] public float FarmhouseDetachedGarageSideMargin { get; set; } = 3f;
        [JsonPropertyName("farmhouseDetachedGarageDepthFraction")] public float FarmhouseDetachedGarageDepthFraction { get; set; } = 0.25f;

        // Driveway
        [JsonPropertyName("drivewayWidthFt")] public FloatDistribution DrivewayWidth { get; set; } = FloatDistribution.Normal(16f, 2f, 12f, 24f);
        [JsonPropertyName("drivewaySurface")] public SurfaceType DrivewaySurface { get; set; } = SurfaceType.Concrete;
        [JsonPropertyName("drivewayStartOffsetFt")] public float DrivewayStartOffset { get; set; } = 31f;

        // Alley driveway
        [JsonPropertyName("alleyDrivewayWidthFt")] public float AlleyDrivewayWidth { get; set; } = 10f;
        [JsonPropertyName("alleyDrivewaySurface")] public SurfaceType AlleyDrivewaySurface { get; set; } = SurfaceType.Gravel;

        // Walks
        [JsonPropertyName("walkWidthFt")] public FloatDistribution WalkWidth { get; set; } = FloatDistribution.Normal(4f, 0.5f, 3f, 5f);
        [JsonPropertyName("walkSurface")] public SurfaceType WalkSurface { get; set; } = SurfaceType.Concrete;
        [JsonPropertyName("walkStartInsetFt")] public float WalkStartInset { get; set; } = 4.5f;

        // Street trees
        [JsonPropertyName("streetTreeSpacingFt")] public FloatDistribution StreetTreeSpacing { get; set; } = FloatDistribution.Normal(40f, 8f, 25f, 60f);
        [JsonPropertyName("streetTreeChance")] public float StreetTreeChance { get; set; } = 0.75f;
        [JsonPropertyName("streetTreeSidewalkOffsetFt")] public float StreetTreeSidewalkOffset { get; set; } = 7f;

        // Yard trees
        [JsonPropertyName("yardTreeCountRange")] public IntDistribution YardTreeCount { get; set; } = new IntDistribution(FloatDistribution.Poisson(2.5f));
        [JsonPropertyName("yardTreePadFt")] public float YardTreePad { get; set; } = 5f;
        [JsonPropertyName("yardTreeSideMarginFt")] public float YardTreeSideMargin { get; set; } = 4f;

        // House density multiplier
        [JsonPropertyName("houseDensity")] public float HouseDensity { get; set; } = 1.0f;

        // Street names
        [JsonPropertyName("streetNamePool")] public string[] StreetNamePool { get; set; } = new[]
        { "OAK", "MAPLE", "CEDAR", "ELM", "WASHINGTON", "MAIN", "COLLEGE", "SECOND" };
        [JsonPropertyName("streetSuffixStreetChance")] public float StreetSuffixStreetChance { get; set; } = 0.7f;

        public void Validate() { }
    }

    // =========================================================================
    // SMALL TOWN (updated with tier system references)
    // =========================================================================

    /// <summary>Parameters controlling the full small-town generator.</summary>
    public sealed class SmallTownConfig
    {
        // --- Core grid geometry ---
        [JsonPropertyName("highwayWidthFt")] public float HighwayWidth { get; set; } = 66f;
        [JsonPropertyName("streetRowFt")] public float StreetRow { get; set; } = 36f;
        [JsonPropertyName("alleyRowFt")] public float AlleyRow { get; set; } = 16f;
        [JsonPropertyName("blockLotDepthFt")] public float BlockLotDepth { get; set; } = 122f;
        [JsonPropertyName("sideLotDepthFt")] public float SideLotDepth { get; set; } = 80f;
        [JsonPropertyName("bandDepthFt")] public float BandDepth { get; set; } = 175f;
        [JsonPropertyName("fourSideBlockChance")] public float FourSideBlockChance { get; set; } = 0.7f;
        [JsonPropertyName("lotDepthJitterFt")] public float LotDepthJitter { get; set; } = 6f;

        // --- Town size presets (legacy - replaced by TownTierSystem) ---
        [JsonPropertyName("sizePresets")] public Dictionary<string, TownSizePreset> SizePresets { get; set; } = new()
        {
            ["crossroads"] = new TownSizePreset { Columns = 2, EWStreets = 2, LotsMin = 2, LotsMax = 3, CommercialPads = 3, CivicAnchors = 1, DeadEnds = 1, Tier = 0, Label = "CROSSROADS" },
            ["hamlet"] = new TownSizePreset { Columns = 4, EWStreets = 3, LotsMin = 3, LotsMax = 4, CommercialPads = 6, CivicAnchors = 2, DeadEnds = 1, Tier = 1, Label = "HAMLET" },
            ["village"] = new TownSizePreset { Columns = 7, EWStreets = 5, LotsMin = 3, LotsMax = 5, CommercialPads = 10, CivicAnchors = 4, DeadEnds = 1, Tier = 2, Label = "VILLAGE" },
            ["town"] = new TownSizePreset { Columns = 10, EWStreets = 6, LotsMin = 3, LotsMax = 5, CommercialPads = 14, CivicAnchors = 5, DeadEnds = 2, Tier = 3, Label = "TOWN" },
            ["boyden"] = new TownSizePreset { Columns = 7, EWStreets = 5, LotsMin = 3, LotsMax = 5, CommercialPads = 10, CivicAnchors = 4, DeadEnds = 1, Tier = 2, Label = "VILLAGE" },
            ["hull"] = new TownSizePreset { Columns = 10, EWStreets = 6, LotsMin = 3, LotsMax = 5, CommercialPads = 14, CivicAnchors = 5, DeadEnds = 2, Tier = 3, Label = "TOWN" },
        };

        // --- Town extras (legacy - replaced by TownExtraDef feature grammar) ---
        [JsonPropertyName("extraCourtChance")] public float ExtraCourtChance { get; set; } = 0.50f;
        [JsonPropertyName("extraPlatEdgeChance")] public float ExtraPlatEdgeChance { get; set; } = 0.45f;
        [JsonPropertyName("extraOutlyingRoadChance")] public float ExtraOutlyingRoadChance { get; set; } = 0.45f;
        [JsonPropertyName("extraBypassedHighwayChance")] public float ExtraBypassedHighwayChance { get; set; } = 0.35f;
        [JsonPropertyName("extraIndustrialSideStreetChance")] public float ExtraIndustrialSideStreetChance { get; set; } = 0.40f;
        [JsonPropertyName("extraTownSquareChance")] public float ExtraTownSquareChance { get; set; } = 0.35f;
        [JsonPropertyName("extraBoulevardChance")] public float ExtraBoulevardChance { get; set; } = 0.16f;
        [JsonPropertyName("extraNoneChance")] public float ExtraNoneChance { get; set; } = 0.35f;
        [JsonPropertyName("extraOneChance")] public float ExtraOneChance { get; set; } = 0.60f;
        [JsonPropertyName("maxExtrasPerTown")] public int MaxExtrasPerTown { get; set; } = 2;

        // --- Per-row alley ---
        [JsonPropertyName("rowAlleyChance")] public FloatDistribution RowAlleyChance { get; set; } = FloatDistribution.Beta(2f, 2f, 0f, 1f);

        // --- Place names ---
        [JsonPropertyName("placeNames")] public string[] PlaceNames { get; set; } = new[]
        {
            "MAPLE CROSSING", "ELM CREEK", "WALNUT GROVE", "CEDAR RIDGE", "PRAIRIE ROSE",
            "STONE BRIDGE", "NORTH BEND", "SILVER LAKE", "OAKDALE", "FAIRVIEW",
            "CLOVER HILL", "GRANDVIEW", "HICKORY GROVE", "PLEASANT VALLEY", "RIVERSIDE",
            "BRISTOL", "LINN GROVE", "COLD SPRING", "UNION CENTER", "HARTFORD",
            "BETHANY", "MORNINGSIDE", "SUNNYSIDE", "GREENFIELD", "BROOKVILLE",
            "PINEHURST", "RIDGEWAY", "LAKEVIEW", "MILLBROOK", "WESTFIELD",
            "GLENDALE", "HOMESTEAD", "PRAIRIE VIEW", "SPRINGDALE", "ASH GROVE",
            "BENTON", "CARLTON", "DOVER", "EASTON", "FRANKLIN"
        };

        // --- Street name pools ---
        [JsonPropertyName("ewStreetNamePool")] public string[] EWStreetNamePool { get; set; } = new[]
        { "IOWA", "MICHIGAN", "MINNESOTA", "DAKOTA", "CALUMET", "OHIO", "NEBRASKA", "KANSAS", "ILLINOIS", "WISCONSIN", "ARKANSAS", "NEBASKA" };
        [JsonPropertyName("nsStreetNamePool")] public string[] NSStreetNamePool { get; set; } = new[]
        { "DIVISION", "LINCOLN", "WASHINGTON", "HARRIS", "WILLIAMS", "CHESTNUT", "BURROUGHS", "ADA", "PARK", "ELEANOR", "VINE", "CHELSEA", "STONE", "HUGH", "LOCUST", "DELTA", "JEFFERSON", "MADISON", "MONROE", "WILSON", "MAPLE", "ELM", "ASH", "OAK", "CEDAR", "POPLAR", "WALNUT", "SPRUCE" };

        // --- Column/lot frontages ---
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [JsonPropertyName("lotFrontageFt")] public FloatDistribution LotFrontage { get; set; } = FloatDistribution.LogNormal(4.2f, 0.15f, 50f, 100f);

        // --- Town silhouette (blob) ---
        [JsonPropertyName("blobHarmonic1Range")] public IntRange BlobHarmonic1 { get; set; } = new(2, 4);
        [JsonPropertyName("blobHarmonic2Range")] public IntRange BlobHarmonic2 { get; set; } = new(5, 8);
        [JsonPropertyName("blobAmplitude1Range")] public FloatRange BlobAmplitude1 { get; set; } = new(0.02f, 0.06f);
        [JsonPropertyName("blobAmplitude2Range")] public FloatRange BlobAmplitude2 { get; set; } = new(0.01f, 0.03f);
        [JsonPropertyName("blobPhaseRange")] public FloatRange BlobPhase { get; set; } = new(0, 6.2831853f);
        [JsonPropertyName("blobRadiusMultiplier")] public float BlobRadiusMultiplier { get; set; } = 1.04f;
        [JsonPropertyName("freeStreetBlobPadding")] public float FreeStreetBlobPadding { get; set; } = 0.32f;

        // --- Vacancy rates ---
        [JsonPropertyName("vacantOuterChance")] public float VacantOuterChance { get; set; } = 0.14f;
        [JsonPropertyName("vacantMidChance")] public float VacantMidChance { get; set; } = 0.05f;
        [JsonPropertyName("vacantCoreChance")] public float VacantCoreChance { get; set; } = 0.015f;
        [JsonPropertyName("vacantOuterRadius")] public float VacantOuterRadius { get; set; } = 0.70f;
        [JsonPropertyName("vacantMidRadius")] public float VacantMidRadius { get; set; } = 0.50f;
        [JsonPropertyName("radialPlatVacantChance")] public float RadialPlatVacantChance { get; set; } = 0.22f;

        // --- Commercial strip ---
        [JsonPropertyName("commercialStartXRangeFt")] public FloatDistribution CommercialStartX { get; set; } = FloatDistribution.Uniform(40f, 260f);
        [JsonPropertyName("commercialGapFt")] public FloatDistribution CommercialGap { get; set; } = FloatDistribution.Uniform(45f, 140f);
        [JsonPropertyName("commercialMainGapMarginFt")] public float CommercialMainGapMargin { get; set; } = 12f;
        [JsonPropertyName("commercialBandOccMarginFt")] public float CommercialBandOccMargin { get; set; } = 5f;
        [JsonPropertyName("industrialBoxCountChance")] public float IndustrialBoxCountChance { get; set; } = 0.55f;
        [JsonPropertyName("crossStreetShopMax")] public int CrossStreetShopMax { get; set; } = 3;
        [JsonPropertyName("crossStreetShopDepthRangeFt")] public FloatDistribution CrossStreetShopDepth { get; set; } = FloatDistribution.Uniform(24f, 100f);
        [JsonPropertyName("crossStreetShopWidthRangeFt")] public FloatDistribution CrossStreetShopWidth { get; set; } = FloatDistribution.Uniform(24f, 100f);

        // --- Grain elevator & bins ---
        [JsonPropertyName("elevatorWidthFt")] public float ElevatorWidth { get; set; } = 48f;
        [JsonPropertyName("elevatorDepthFt")] public float ElevatorDepth { get; set; } = 88f;
        [JsonPropertyName("elevatorRightChance")] public float ElevatorRightChance { get; set; } = 0.62f;
        [JsonPropertyName("elevatorOffsetFromMainFt")] public FloatDistribution ElevatorOffsetFromMain { get; set; } = FloatDistribution.Uniform(45f, 150f);
        [JsonPropertyName("binCountRange")] public IntDistribution ElevatorBinCount { get; set; } = new IntDistribution(FloatDistribution.Poisson(3f) + 2f);
        [JsonPropertyName("binDiameterFt")] public FloatDistribution BinDiameter { get; set; } = FloatDistribution.LogNormal(3.1f, 0.1f, 18f, 30f);
        [JsonPropertyName("binSpacingFt")] public float BinSpacing { get; set; } = 30f;
        [JsonPropertyName("binRowBehindElevatorFt")] public float BinRowBehindElevator { get; set; } = 10f;

        // --- Rail-side industrial strip ---
        [JsonPropertyName("railIndustrialRowSpacingFt")] public FloatDistribution RailIndustrialRowSpacing { get; set; } = FloatDistribution.Uniform(56f, 84f);
        [JsonPropertyName("railIndustrialDepthRangeFt")] public FloatDistribution RailIndustrialDepth { get; set; } = FloatDistribution.LogNormal(4.5f, 0.5f, 60f, 200f);
        [JsonPropertyName("railIndustrialZJitterFt")] public FloatDistribution RailIndustrialZJitter { get; set; } = FloatDistribution.Normal(0f, 16f, -50f, 50f);

        // --- South fringe ---
        [JsonPropertyName("southBandDepthFt")] public FloatDistribution SouthBandDepth { get; set; } = FloatDistribution.Uniform(150f, 210f);
        [JsonPropertyName("southHouseCountRange")] public IntDistribution SouthHouseCount { get; set; } = new IntDistribution(FloatDistribution.Uniform(1, 4));
        [JsonPropertyName("southHouseWidthFt")] public FloatDistribution SouthHouseWidth { get; set; } = FloatDistribution.Uniform(150f, 220f);
        [JsonPropertyName("southHouseSpacingFt")] public FloatDistribution SouthHouseSpacing { get; set; } = FloatDistribution.Uniform(24f, 60f);
        [JsonPropertyName("southIndustrialChance")] public float SouthIndustrialChance { get; set; } = 0.6f;

        // --- Civic anchors (per tier) ---
        [JsonPropertyName("churchCountTier0")] public IntDistribution ChurchCountTier0 { get; set; } = new IntDistribution(FloatDistribution.Uniform(0, 2));
        [JsonPropertyName("churchCountTier1")] public IntDistribution ChurchCountTier1 { get; set; } = new IntDistribution(FloatDistribution.Uniform(1, 3));
        [JsonPropertyName("churchCountTier2Plus")] public IntDistribution ChurchCountTier2Plus { get; set; } = new IntDistribution(FloatDistribution.Uniform(1, 3));
        [JsonPropertyName("schoolChanceTier0")] public float SchoolChanceTier0 { get; set; } = 0.3f;
        [JsonPropertyName("schoolChanceTier1")] public float SchoolChanceTier1 { get; set; } = 0.6f;
        [JsonPropertyName("schoolChanceTier2Plus")] public float SchoolChanceTier2Plus { get; set; } = 0.95f;
        [JsonPropertyName("parkChanceTier0")] public float ParkChanceTier0 { get; set; } = 0.35f;
        [JsonPropertyName("parkChanceTier1")] public float ParkChanceTier1 { get; set; } = 0.6f;
        [JsonPropertyName("parkChanceTier2Plus")] public float ParkChanceTier2Plus { get; set; } = 0.85f;

        // --- Residential lots ---
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [JsonPropertyName("lotSetbackFt")] public FloatDistribution LotSetback { get; set; } = FloatDistribution.Normal(12f, 4f, 6f, 22f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Dirichlet(0.55, 0.35, 0.10) - integral/detached/none.")]
        [JsonPropertyName("garageModeIntegralChance")] public float GarageModeIntegralChance { get; set; } = 0.55f;
        [JsonPropertyName("garageModeDetachedChance")] public float GarageModeDetachedChance { get; set; } = 0.35f;
        [JsonPropertyName("garageModeNoneChance")] public float GarageModeNoneChance { get; set; } = 0.10f;

        [JsonPropertyName("houseFrontageNarrowThresholdFt")] public float HouseFrontageNarrowThreshold { get; set; } = 46f;
        [JsonPropertyName("housePlacementJitterFt")] public float HousePlacementJitter { get; set; } = 2f;
        [JsonPropertyName("houseFitPadFt")] public float HouseFitPad { get; set; } = 8f;
        [JsonPropertyName("houseSetbackMinFt")] public float HouseSetbackMin { get; set; } = 4f;
        [JsonPropertyName("shedChance")] public float ShedChance { get; set; } = 0.12f;
        [JsonPropertyName("shedWidthFt")] public FloatDistribution ShedWidth { get; set; } = FloatDistribution.Normal(10f, 1.5f, 8f, 14f);
        [JsonPropertyName("shedDepthFt")] public FloatDistribution ShedDepth { get; set; } = FloatDistribution.Normal(12f, 2f, 8f, 18f);
        [JsonPropertyName("shedSideMarginFt")] public float ShedSideMargin { get; set; } = 4f;
        [JsonPropertyName("shedBackMarginFt")] public float ShedBackMargin { get; set; } = 5f;
        [JsonPropertyName("detachedGarageViaAlleyChance")] public float DetachedGarageViaAlleyChance { get; set; } = 0.6f;
        [JsonPropertyName("detachedGarageWidthFt")] public FloatDistribution DetachedGarageWidth { get; set; } = FloatDistribution.Normal(22f, 3f, 16f, 30f);
        [JsonPropertyName("detachedGarageDepthFt")] public FloatDistribution DetachedGarageDepth { get; set; } = FloatDistribution.Normal(26f, 3f, 20f, 34f);
        [JsonPropertyName("detachedGarageXJitterFt")] public float DetachedGarageXJitter { get; set; } = 6f;
        [JsonPropertyName("detachedGarageFromAlleyFt")] public float DetachedGarageFromAlley { get; set; } = 9f;
        [JsonPropertyName("farmhouseDetachedGarageWidthFt")] public FloatDistribution FarmhouseDetachedGarageWidth { get; set; } = FloatDistribution.Normal(20f, 3f, 16f, 26f);
        [JsonPropertyName("farmhouseDetachedGarageDepthFt")] public FloatDistribution FarmhouseDetachedGarageDepth { get; set; } = FloatDistribution.Normal(22f, 3f, 18f, 28f);
        [JsonPropertyName("farmhouseDetachedGarageSideMarginFt")] public float FarmhouseDetachedGarageSideMargin { get; set; } = 3f;
        [JsonPropertyName("farmhouseDetachedGarageZRangeFt")] public FloatDistribution FarmhouseDetachedGarageZRange { get; set; } = FloatDistribution.Uniform(22f, 100f);

        // Driveways & walks
        [JsonPropertyName("drivewayWidthFt")] public FloatDistribution DrivewayWidth { get; set; } = FloatDistribution.Normal(16f, 2f, 12f, 24f);
        [JsonPropertyName("drivewaySurface")] public SurfaceType DrivewaySurface { get; set; } = SurfaceType.Concrete;
        [JsonPropertyName("drivewayStartOffsetFt")] public float DrivewayStartOffset { get; set; } = 31f;
        [JsonPropertyName("alleyDrivewayWidthFt")] public float AlleyDrivewayWidth { get; set; } = 10f;
        [JsonPropertyName("alleyDrivewaySurface")] public SurfaceType AlleyDrivewaySurface { get; set; } = SurfaceType.Gravel;
        [JsonPropertyName("walkWidthFt")] public FloatDistribution WalkWidth { get; set; } = FloatDistribution.Normal(4f, 0.5f, 3f, 5f);
        [JsonPropertyName("walkSurface")] public SurfaceType WalkSurface { get; set; } = SurfaceType.Concrete;
        [JsonPropertyName("walkSidewalkInsetFt")] public float WalkSidewalkInset { get; set; } = 2.25f;

        // --- Free streets ---
        [JsonPropertyName("railCorridorStreetChance")] public float RailCorridorStreetChance { get; set; } = 0.30f;
        [JsonPropertyName("curvedStreetChance")] public float CurvedStreetChance { get; set; } = 0.30f;
        [JsonPropertyName("railCorridorEraseHalfFt")] public float RailCorridorEraseHalf { get; set; } = 20f;
        [JsonPropertyName("curvedStreetEraseHalfFt")] public float CurvedStreetEraseHalf { get; set; } = 20f;

        // --- Plat & build pass ---
        [JsonPropertyName("platLotFrontageFt")] public FloatDistribution PlatLotFrontage { get; set; } = FloatDistribution.LogNormal(4.3f, 0.15f, 55f, 95f);
        [JsonPropertyName("platVacantChance")] public float PlatVacantChance { get; set; } = 0.10f;
        [JsonPropertyName("platSetbackFt")] public FloatDistribution PlatSetback { get; set; } = FloatDistribution.Normal(12f, 3f, 7f, 18f);
        [JsonPropertyName("platHouseJitterFt")] public float PlatHouseJitter { get; set; } = 2f;
        [JsonPropertyName("platGarageIntegralChance")] public float PlatGarageIntegralChance { get; set; } = 0.55f;
        [JsonPropertyName("platGarageDetachedChance")] public float PlatGarageDetachedChance { get; set; } = 0.35f;
        [JsonPropertyName("platGarageNoneChance")] public float PlatGarageNoneChance { get; set; } = 0.10f;
        [JsonPropertyName("platDetachedGarageWidthFt")] public FloatDistribution PlatDetachedGarageWidth { get; set; } = FloatDistribution.Normal(22f, 3f, 16f, 30f);
        [JsonPropertyName("platDetachedGarageDepthFt")] public FloatDistribution PlatDetachedGarageDepth { get; set; } = FloatDistribution.Normal(26f, 3f, 20f, 34f);
        [JsonPropertyName("platDetachedGarageXJitterFt")] public float PlatDetachedGarageXJitter { get; set; } = 6f;
        [JsonPropertyName("platDetachedGarageFromAlleyFt")] public float PlatDetachedGarageFromAlley { get; set; } = 9f;

        // --- Cul-de-sac court ---
        [JsonPropertyName("courtXRangeFt")] public FloatDistribution CourtXRange { get; set; } = FloatDistribution.Uniform(120f, 800f);
        [JsonPropertyName("courtLenFt")] public FloatDistribution CourtLen { get; set; } = FloatDistribution.Uniform(240f, 340f);
        [JsonPropertyName("courtHouseCountRange")] public IntDistribution CourtHouseCount { get; set; } = new IntDistribution(FloatDistribution.Uniform(2, 4));
        [JsonPropertyName("courtHouseFrontageFt")] public FloatDistribution CourtHouseFrontage { get; set; } = FloatDistribution.LogNormal(4.1f, 0.15f, 50f, 75f);
        [JsonPropertyName("courtHouseSetbackFt")] public FloatDistribution CourtHouseSetback { get; set; } = FloatDistribution.Normal(12f, 3f, 7f, 18f);
        [JsonPropertyName("courtHouseJitterFt")] public float CourtHouseJitter { get; set; } = 2f;

        // --- Plat edge street ---
        [JsonPropertyName("platEdgeXRangeFt")] public FloatDistribution PlatEdgeXRange { get; set; } = FloatDistribution.Uniform(150f, 800f);
        [JsonPropertyName("platEdgeLenFt")] public FloatDistribution PlatEdgeLen { get; set; } = FloatDistribution.Uniform(260f, 420f);
        [JsonPropertyName("platEdgeLotFrontageFt")] public FloatDistribution PlatEdgeLotFrontage { get; set; } = FloatDistribution.LogNormal(4.2f, 0.15f, 50f, 85f);

        // --- Outlying road ---
        [JsonPropertyName("outlyingRoadZOffsetFt")] public FloatDistribution OutlyingRoadZOffset { get; set; } = FloatDistribution.Uniform(200f, 300f);
        [JsonPropertyName("outlyingRoadHouseCountRange")] public IntDistribution OutlyingRoadHouseCount { get; set; } = new IntDistribution(FloatDistribution.Uniform(3, 6));
        [JsonPropertyName("outlyingRoadHouseFrontageFt")] public FloatDistribution OutlyingRoadHouseFrontage { get; set; } = FloatDistribution.LogNormal(4.2f, 0.15f, 55f, 90f);
        [JsonPropertyName("outlyingRoadHouseDepthFt")] public FloatDistribution OutlyingRoadHouseDepth { get; set; } = FloatDistribution.LogNormal(3.9f, 0.15f, 40f, 60f);
        [JsonPropertyName("outlyingRoadHouseSpacingFt")] public FloatDistribution OutlyingRoadHouseSpacing { get; set; } = FloatDistribution.Uniform(300f, 430f);

        // --- Bypassed highway ---
        [JsonPropertyName("bypassedHighwayXRangeFt")] public FloatDistribution BypassedHighwayXRange { get; set; } = FloatDistribution.Uniform(90f, 800f);
        [JsonPropertyName("bypassedHighwayShopCountRange")] public IntDistribution BypassedHighwayShopCount { get; set; } = new IntDistribution(FloatDistribution.Uniform(3, 6));
        [JsonPropertyName("bypassedHighwayShopFrontageFt")] public FloatDistribution BypassedHighwayShopFrontage { get; set; } = FloatDistribution.LogNormal(4.2f, 0.15f, 55f, 90f);

        // --- Boulevard ---
        [JsonPropertyName("boulevardMedianWidthFt")] public FloatDistribution BoulevardMedianWidth { get; set; } = FloatDistribution.Normal(16f, 4f, 10f, 30f);

        // --- Perimeter loop ---
        [JsonPropertyName("perimeterGapNFt")] public FloatDistribution PerimeterGapN { get; set; } = FloatDistribution.Normal(50f, 15f, 20f, 100f);
        [JsonPropertyName("perimeterJogHeightFt")] public FloatDistribution PerimeterJogHeight { get; set; } = FloatDistribution.Normal(80f, 20f, 30f, 150f);
        [JsonPropertyName("deadEndStubXJitterFt")] public float DeadEndStubXJitter { get; set; } = 40f;
        [JsonPropertyName("deadEndStubLenFt")] public FloatDistribution DeadEndStubLen { get; set; } = FloatDistribution.Uniform(210f, 300f);
        [JsonPropertyName("deadEndBulbRadiusFt")] public float DeadEndBulbRadius { get; set; } = 44f;

        // Density multipliers
        [JsonPropertyName("residentialDensity")] public float ResidentialDensity { get; set; } = 1.0f;
        [JsonPropertyName("commercialDensity")] public float CommercialDensity { get; set; } = 1.0f;
        [JsonPropertyName("industrialDensity")] public float IndustrialDensity { get; set; } = 1.0f;
        [JsonPropertyName("civicDensity")] public float CivicDensity { get; set; } = 1.0f;
        [JsonPropertyName("landmarkDensity")] public float LandmarkDensity { get; set; } = 1.0f;

        [JsonPropertyName("townExtras")] public List<TownExtraDef> TownExtras { get; set; } = TownExtraGrammar.GetDefaultPool();

        public void Validate() { }
    }

    public sealed class TownSizePreset
    {
        public int Columns { get; set; }
        public int EWStreets { get; set; }
        public int LotsMin { get; set; }
        public int LotsMax { get; set; }
        public int CommercialPads { get; set; }
        public int CivicAnchors { get; set; }
        public int DeadEnds { get; set; }
        public int Tier { get; set; }
        public string Label { get; set; } = "";
    }

    // =========================================================================
    // WORLD MAP (simplified - noise params moved to NoiseConfig)
    // =========================================================================

    /// <summary>Parameters controlling the infinite world generator (high-level only; details in NoiseConfig).</summary>
    public sealed class WorldConfig
    {
        // --- Fundamental units ---
        [JsonPropertyName("mileFt")] public float MileFeet { get; set; } = 5280f;
        [JsonPropertyName("sectionFeet")] public float SectionFeet { get; set; } = 5280f;
        [JsonPropertyName("cellFeet")] public float CellFeet { get; set; } = 15840f;
        [JsonPropertyName("minSpanFt")] public float MinSpan { get; set; } = 150f;

        // --- High-level density multipliers ---
        [JsonPropertyName("townDensity")] public float TownDensity { get; set; } = 1.0f;
        [JsonPropertyName("farmDensity")] public float FarmDensity { get; set; } = 1.0f;

        // --- Highway names ---
        [JsonPropertyName("highwayNames")] public string[] HighwayNames { get; set; } = new[]
        { "HWY 12", "HWY 7", "HWY 31", "HWY 46", "HWY 22", "HWY 38", "HWY 5", "HWY 17" };

        // --- Field entrances ---
        [JsonPropertyName("fieldEntrancePerQuarterChance")] public float FieldEntrancePerQuarterChance { get; set; } = 1.0f;
        [JsonPropertyName("fieldEntranceMaxPerQuarter")] public int FieldEntranceMaxPerQuarter { get; set; } = 2;
        [JsonPropertyName("fieldEntranceWidthFt")] public float FieldEntranceWidth { get; set; } = 26f;
        [JsonPropertyName("fieldEntranceLengthFt")] public FloatDistribution FieldEntranceLength { get; set; } = FloatDistribution.Uniform(95f, 170f);
        [JsonPropertyName("fieldEntranceSetbackFromIntersectionFt")] public float FieldEntranceSetbackFromIntersection { get; set; } = 110f;
        [JsonPropertyName("fieldEntranceSurface")] public SurfaceType FieldEntranceSurface { get; set; } = SurfaceType.Dirt;

        public void Validate() { }
    }

    // =========================================================================
    // COUNTRYSIDE (3-mile cell views)
    // =========================================================================

    /// <summary>Parameters controlling the 3-mile countryside cell generator.</summary>
    public sealed class CountrysideConfig
    {
        [JsonPropertyName("cellFeet")] public float CellFeet { get; set; } = 15840f;
        [JsonPropertyName("sectionRoadWidthFt")] public float SectionRoadWidth { get; set; } = 66f;
        [JsonPropertyName("highwayWidthFt")] public float HighwayWidth { get; set; } = 66f;
        [JsonPropertyName("sectionMidFt")] public float SectionMid { get; set; } = 7920f;

        // Town placement within cell
        [JsonPropertyName("townXJitterFt")] public FloatDistribution TownXJitter { get; set; } = FloatDistribution.Normal(0f, 220f, -440f, 440f);
        [JsonPropertyName("townZJitterFt")] public FloatDistribution TownZJitter { get; set; } = FloatDistribution.Normal(0f, 220f, -440f, 440f);

        // Farmsteads
        [JsonPropertyName("farmsteadCountRange")] public IntDistribution FarmsteadCount { get; set; } = new IntDistribution(FloatDistribution.Uniform(6, 13));
        [JsonPropertyName("farmsteadXRangeFt")] public FloatDistribution FarmsteadXRange { get; set; } = FloatDistribution.Uniform(600f, 15240f);
        [JsonPropertyName("farmsteadZRangeFt")] public FloatDistribution FarmsteadZRange { get; set; } = FloatDistribution.Uniform(600f, 15240f);
        [JsonPropertyName("farmsteadHouseRingCount")] public int FarmsteadHouseRingCount { get; set; } = 10;
        [JsonPropertyName("farmsteadHouseRingRadiusFt")] public FloatDistribution FarmsteadHouseRingRadius { get; set; } = FloatDistribution.Uniform(55f, 95f);
        [JsonPropertyName("farmsteadHouseRingAngleJitter")] public float FarmsteadHouseRingAngleJitter { get; set; } = 0.2f;
        [JsonPropertyName("farmsteadGroveCountRange")] public IntDistribution FarmsteadGroveCount { get; set; } = new IntDistribution(FloatDistribution.Uniform(6, 13));
        [JsonPropertyName("farmsteadGroveRadiusFt")] public FloatDistribution FarmsteadGroveRadius { get; set; } = FloatDistribution.Uniform(35f, 95f);
        [JsonPropertyName("farmsteadGroveAngleJitter")] public float FarmsteadGroveAngleJitter { get; set; } = 0.3f;

        // Fields
        [JsonPropertyName("fieldPalette")] public string[] FieldPalette { get; set; } = new[]
        { "#ded9bd", "#d6d0b4", "#cbc4a2", "#e4e0cb", "#d2cbb0", "#dcd6b8", "#d8d2b6" };

        // Density
        [JsonPropertyName("farmsteadDensity")] public float FarmsteadDensity { get; set; } = 1.0f;
        [JsonPropertyName("fieldFeatureDensity")] public float FieldFeatureDensity { get; set; } = 1.0f;

        public void Validate() { }
    }

    // =========================================================================
    // RENDERING
    // =========================================================================

    /// <summary>Parameters controlling SVG rendering (colors, line weights, LOD thresholds).</summary>
    public sealed class RenderConfig
    {
        // House SVG
        [JsonPropertyName("houseWallScreenPx")] public float HouseWallScreenPx { get; set; } = 0.95f;
        [JsonPropertyName("houseGapScreenPxExtra")] public float HouseGapScreenPxExtra { get; set; } = 0.6f;
        [JsonPropertyName("houseThinScreenPxFactor")] public float HouseThinScreenPxFactor { get; set; } = 0.55f;

        // Map SVG
        [JsonPropertyName("mapTargetWidthPx")] public float MapTargetWidthPx { get; set; } = 920f;
        [JsonPropertyName("mapMaxHeightPx")] public float MapMaxHeightPx { get; set; } = 900f;
        [JsonPropertyName("mapMarginPx")] public float MapMarginPx { get; set; } = 30f;
        [JsonPropertyName("mapTitleHeightPx")] public float MapTitleHeightPx { get; set; } = 66f;
        [JsonPropertyName("mapFooterHeightPx")] public float MapFooterHeightPx { get; set; } = 26f;

        // LOD
        [JsonPropertyName("lodSimpleTreeRadiusPx")] public float LodSimpleTreeRadiusPx { get; set; } = 3.2f;
        [JsonPropertyName("lodDetailTreeRadiusPxFactor")] public float LodDetailTreeRadiusPxFactor { get; set; } = 2.2f;
        [JsonPropertyName("lodRailTieMinScale")] public float LodRailTieMinScale { get; set; } = 0.015f;

        // Colors (hex)
        [JsonPropertyName("colorBackground")] public string ColorBackground { get; set; } = "#f0efe0";
        [JsonPropertyName("colorFloor")] public string ColorFloor { get; set; } = "#e7dfd0";
        [JsonPropertyName("colorWall")] public string ColorWall { get; set; } = "#3a332b";
        [JsonPropertyName("colorGap")] public string ColorGap { get; set; } = "#faf7ef";
        [JsonPropertyName("colorWindow")] public string ColorWindow { get; set; } = "#3a332b";
        [JsonPropertyName("colorDoor")] public string ColorDoor { get; set; } = "#3a332b";
        [JsonPropertyName("colorDoorSwing")] public string ColorDoorSwing { get; set; } = "#8a7f70";
        [JsonPropertyName("colorDash")] public string ColorDash { get; set; } = "#8a7f70";
        [JsonPropertyName("colorText")] public string ColorText { get; set; } = "#3a332b";
        [JsonPropertyName("colorSmallText")] public string ColorSmallText { get; set; } = "#6b6154";
        [JsonPropertyName("colorRoadAsphalt")] public string ColorRoadAsphalt { get; set; } = "#8f8b83";
        [JsonPropertyName("colorRoadGravel")] public string ColorRoadGravel { get; set; } = "#c8c3b5";
        [JsonPropertyName("colorRoadEdgeGravel")] public string ColorRoadEdgeGravel { get; set; } = "#a9a495";
        [JsonPropertyName("colorCenterline")] public string ColorCenterline { get; set; } = "#e8d44d";
        [JsonPropertyName("colorAlley")] public string ColorAlley { get; set; } = "#c9c3b2";
        [JsonPropertyName("colorDrivewayConcrete")] public string ColorDrivewayConcrete { get; set; } = "#dcd9d2";
        [JsonPropertyName("colorDrivewayGravel")] public string ColorDrivewayGravel { get; set; } = "#d6c9a3";
        [JsonPropertyName("colorDrivewayDirt")] public string ColorDrivewayDirt { get; set; } = "#bda882";
        [JsonPropertyName("colorWalk")] public string ColorWalk { get; set; } = "#e3e0d8";
        [JsonPropertyName("colorWater")] public string ColorWater { get; set; } = "#7f989b";
        [JsonPropertyName("colorWaterEdge")] public string ColorWaterEdge { get; set; } = "#a8bfc0";
        [JsonPropertyName("colorRailBallast")] public string ColorRailBallast { get; set; } = "#b7ae9c";
        [JsonPropertyName("colorRailRail")] public string ColorRailRail { get; set; } = "#4a453d";
        [JsonPropertyName("colorRailTie")] public string ColorRailTie { get; set; } = "#57503d";
        [JsonPropertyName("colorTree")] public string ColorTree { get; set; } = "#3f3b34";
        [JsonPropertyName("colorContourIndex")] public string ColorContourIndex { get; set; } = "#b3aa90";
        [JsonPropertyName("colorContourMinor")] public string ColorContourMinor { get; set; } = "#c8c0a7";
        [JsonPropertyName("colorMedian")] public string ColorMedian { get; set; } = "#8a9a6b";
        [JsonPropertyName("colorMedianEdge")] public string ColorMedianEdge { get; set; } = "#5f6b4c";

        // Viewer
        [JsonPropertyName("viewerMaxZoom")] public int ViewerMaxZoom { get; set; } = 300;
        [JsonPropertyName("viewerNiceSpanLadderFt")] public int[] ViewerNiceSpanLadder { get; set; } = new[]
        { 150, 300, 600, 1200, 2400, 4800, 9600, 19200, 38400, 76800, 153600 };
        [JsonPropertyName("viewerDebounceMs")] public int ViewerDebounceMs { get; set; } = 120;
        [JsonPropertyName("viewerCacheSize")] public int ViewerCacheSize { get; set; } = 24;

        public void Validate() { }
    }

    // =========================================================================
    // HELPER TYPES (legacy FloatRange/IntRange for backward compat)
    // =========================================================================

    /// <summary>A float range with min/max. Serializes as [min, max] array. Legacy - use FloatDistribution.</summary>
    [JsonConverter(typeof(FloatRangeConverter))]
    public readonly struct FloatRange
    {
        public readonly float Min, Max;
        public FloatRange(float min, float max) { Min = min; Max = max; }
        public void Validate(string name)
        {
            if (Min > Max) throw new ArgumentException($"{name}: Min ({Min}) > Max ({Max})");
            if (float.IsNaN(Min) || float.IsNaN(Max)) throw new ArgumentException($"{name}: NaN not allowed");
        }
        public bool Contains(float v) => v >= Min && v <= Max;
        public float Lerp(float t) => Min + (Max - Min) * Math.Clamp(t, 0, 1);
        public override string ToString() => $"[{Min}, {Max}]";
    }

    /// <summary>An integer range with inclusive min/max. Serializes as [min, max] array. Legacy - use IntDistribution.</summary>
    [JsonConverter(typeof(IntRangeConverter))]
    public readonly struct IntRange
    {
        public readonly int Min, Max;
        public IntRange(int min, int max) { Min = min; Max = max; }
        public void Validate(string name)
        {
            if (Min > Max) throw new ArgumentException($"{name}: Min ({Min}) > Max ({Max})");
        }
        public bool Contains(int v) => v >= Min && v <= Max;
        public override string ToString() => $"[{Min}, {Max}]";
    }

    internal sealed class FloatRangeConverter : JsonConverter<FloatRange>
    {
        public override FloatRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                reader.Read(); float min = reader.GetSingle();
                reader.Read(); float max = reader.GetSingle();
                reader.Read(); return new FloatRange(min, max);
            }
            throw new JsonException("FloatRange must be an array [min, max]");
        }
        public override void Write(Utf8JsonWriter writer, FloatRange value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.Min);
            writer.WriteNumberValue(value.Max);
            writer.WriteEndArray();
        }
    }

    internal sealed class IntRangeConverter : JsonConverter<IntRange>
    {
        public override IntRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                reader.Read(); int min = reader.GetInt32();
                reader.Read(); int max = reader.GetInt32();
                reader.Read(); return new IntRange(min, max);
            }
            throw new JsonException("IntRange must be an array [min, max]");
        }
        public override void Write(Utf8JsonWriter writer, IntRange value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.Min);
            writer.WriteNumberValue(value.Max);
            writer.WriteEndArray();
        }
    }
}