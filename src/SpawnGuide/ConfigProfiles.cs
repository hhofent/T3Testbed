using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// A named configuration profile with parameter overrides.
    /// Profiles can be layered: base config + profile overrides = final config.
    /// </summary>
    public sealed class ConfigProfile
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public Dictionary<string, object> Overrides { get; set; } = new();
        public bool IsBuiltIn { get; set; } = false;

        public ConfigProfile() { }

        public ConfigProfile(string name, string description, Dictionary<string, object> overrides, bool builtIn = false)
        {
            Name = name; Description = description; Overrides = overrides; IsBuiltIn = builtIn;
        }

        /// <summary>Apply this profile's overrides to a config object.</summary>
        public void Apply(SpawnConfig config)
        {
            foreach (var (path, value) in Overrides)
            {
                SetValue(config, path, value);
            }
        }

        private void SetValue(object root, string path, object value)
        {
            string[] parts = path.Split('.');
            object current = root;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                var type = current.GetType();
                var prop = type.GetProperty(parts[i], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop != null) { current = prop.GetValue(current); continue; }
                var field = type.GetField(parts[i], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (field != null) { current = field.GetValue(current); continue; }
                return;
            }

            if (current == null) return;

            var targetType = current.GetType();
            var lastPart = parts[^1];
            var targetProp = targetType.GetProperty(lastPart, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            var targetField = targetType.GetField(lastPart, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

            if (targetProp != null && targetProp.CanWrite)
            {
                targetProp.SetValue(current, Convert.ChangeType(value, targetProp.PropertyType));
            }
            else if (targetField != null)
            {
                targetField.SetValue(current, Convert.ChangeType(value, targetField.FieldType));
            }
        }
    }

    /// <summary>
    /// Registry of built-in configuration profiles.
    /// </summary>
    public static class ConfigProfiles
    {
        private static readonly Dictionary<string, ConfigProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new();

        static ConfigProfiles()
        {
            RegisterBuiltInProfiles();
        }

        /// <summary>Get a built-in profile by name.</summary>
        public static ConfigProfile Get(string name)
        {
            lock (_lock)
            {
                if (_profiles.TryGetValue(name, out var profile)) return profile;
                throw new ArgumentException($"Profile '{name}' not found. Available: {string.Join(", ", _profiles.Keys)}");
            }
        }

        /// <summary>Get all registered profile names.</summary>
        public static IReadOnlyList<string> GetNames()
        {
            lock (_lock) return new List<string>(_profiles.Keys).AsReadOnly();
        }

        /// <summary>Get all profiles.</summary>
        public static IReadOnlyList<ConfigProfile> GetAll()
        {
            lock (_lock) return new List<ConfigProfile>(_profiles.Values).AsReadOnly();
        }

        /// <summary>Register a custom profile.</summary>
        public static void Register(ConfigProfile profile)
        {
            lock (_lock) _profiles[profile.Name] = profile;
        }

        /// <summary>Create a config with a profile applied.</summary>
        public static SpawnConfig ApplyProfile(string profileName, SpawnConfig baseConfig = null)
        {
            var config = baseConfig ?? SpawnConfig.Default;
            var profile = Get(profileName);
            profile.Apply(config);
            return config;
        }

        /// <summary>Create a config with multiple profiles applied in order.</summary>
        public static SpawnConfig ApplyProfiles(IEnumerable<string> profileNames, SpawnConfig baseConfig = null)
        {
            var config = baseConfig ?? SpawnConfig.Default;
            foreach (var name in profileNames)
            {
                Get(name).Apply(config);
            }
            return config;
        }

        private static void RegisterBuiltInProfiles()
        {
            // 1. DEFAULT - 1990s rural Iowa baseline
            Register(new ConfigProfile("default",
                "1990s rural Iowa baseline - all parameters at calibrated defaults",
                new Dictionary<string, object>(), true));

            // 2. DENSE-SUBURBAN - Smaller lots, more houses, tighter spacing
            Register(new ConfigProfile("dense-suburban",
                "Smaller lots, higher density - suburban fringe feel",
                new Dictionary<string, object>
                {
                    ["SmallTown.ResidentialDensity"] = 1.5f,
                    ["SmallTown.LotFrontage.Min"] = 45f,
                    ["SmallTown.LotFrontage.Max"] = 65f,
                    ["SmallTown.LotSetback.Min"] = 5f,
                    ["SmallTown.LotSetback.Max"] = 12f,
                    ["SmallTown.HousePlacementJitter"] = 1f,
                    ["TownBlock.HouseDensity"] = 1.3f,
                    ["TownBlock.LotFrontage.Min"] = 55f,
                    ["TownBlock.LotFrontage.Max"] = 75f,
                    ["TownBlock.Setback.Min"] = 6f,
                    ["TownBlock.Setback.Max"] = 15f,
                    ["Rural.LotWidth.Min"] = 180f,
                    ["Rural.LotWidth.Max"] = 240f,
                    ["Rural.LotDepth.Min"] = 240f,
                    ["Rural.LotDepth.Max"] = 360f,
                    ["Rural.HouseSetback.Min"] = 25f,
                    ["Rural.HouseSetback.Max"] = 45f,
                    ["World.FarmDensity"] = 0.25f,
                }, true));

            // 3. SPARSE-FRONTIER - Larger lots, fewer towns, lower density
            Register(new ConfigProfile("sparse-frontier",
                "Larger lots, fewer towns - frontier/edge of settlement",
                new Dictionary<string, object>
                {
                    ["SmallTown.ResidentialDensity"] = 0.5f,
                    ["SmallTown.CommercialDensity"] = 0.6f,
                    ["SmallTown.CivicDensity"] = 0.7f,
                    ["SmallTown.LotFrontage.Min"] = 70f,
                    ["SmallTown.LotFrontage.Max"] = 100f,
                    ["SmallTown.LotSetback.Min"] = 12f,
                    ["SmallTown.LotSetback.Max"] = 25f,
                    ["TownBlock.HouseDensity"] = 0.6f,
                    ["TownBlock.LotFrontage.Min"] = 85f,
                    ["TownBlock.LotFrontage.Max"] = 110f,
                    ["TownBlock.Setback.Min"] = 15f,
                    ["TownBlock.Setback.Max"] = 30f,
                    ["Rural.LotWidth.Min"] = 280f,
                    ["Rural.LotWidth.Max"] = 380f,
                    ["Rural.LotDepth.Min"] = 400f,
                    ["Rural.LotDepth.Max"] = 550f,
                    ["Rural.HouseSetback.Min"] = 50f,
                    ["Rural.HouseSetback.Max"] = 80f,
                    ["Rural.OutbuildingDensity"] = 0.7f,
                    ["World.TownDensity"] = 0.3f,
                    ["World.FarmDensity"] = 0.1f,
                    ["World.TownRailProximityMi"] = 15f,
                    ["Countryside.FarmsteadDensity"] = 0.6f,
                }, true));

            // 4. MODERN-IOWA - 2020s: larger houses, attached garages, modern siding
            Register(new ConfigProfile("modern-iowa",
                "2020s Iowa: larger houses, mostly attached garages, modern siding colors",
                new Dictionary<string, object>
                {
                    ["Houses.RanchWidth.Min"] = 44f,
                    ["Houses.RanchWidth.Max"] = 58f,
                    ["Houses.RanchDepth.Min"] = 30f,
                    ["Houses.RanchDepth.Max"] = 40f,
                    ["Houses.RanchGarageWidth.Min"] = 20f,
                    ["Houses.RanchGarageWidth.Max"] = 26f,
                    ["Houses.FarmhouseWidth.Min"] = 32f,
                    ["Houses.FarmhouseWidth.Max"] = 42f,
                    ["Houses.FarmhouseDepth.Min"] = 34f,
                    ["Houses.FarmhouseDepth.Max"] = 44f,
                    ["Houses.SplitLevelWidth.Min"] = 34f,
                    ["Houses.SplitLevelWidth.Max"] = 44f,
                    ["Houses.SplitLevelDepth.Min"] = 34f,
                    ["Houses.SplitLevelDepth.Max"] = 44f,
                    ["Houses.SplitLevelGarageWidth.Min"] = 18f,
                    ["Houses.SplitLevelGarageWidth.Max"] = 24f,
                    ["Houses.GarageModeIntegralChance"] = 0.70f,
                    ["Houses.GarageModeDetachedChance"] = 0.20f,
                    ["Houses.GarageModeNoneChance"] = 0.10f,
                    ["Houses.SidingOptions"] = new[]
                    {
                        "White vinyl", "Light Gray vinyl", "Beige vinyl", "Blue-Gray vinyl",
                        "Brick front + Light Gray vinyl sides", "Fiber cement (modern)", "Dark Bronze vinyl"
                    },
                    ["SmallTown.GarageModeIntegralChance"] = 0.70f,
                    ["SmallTown.GarageModeDetachedChance"] = 0.20f,
                    ["SmallTown.GarageModeNoneChance"] = 0.10f,
                    ["SmallTown.DrivewaySurface"] = "concrete",
                    ["SmallTown.WalkSurface"] = "concrete",
                    ["TownBlock.DrivewaySurface"] = "concrete",
                    ["TownBlock.WalkSurface"] = "concrete",
                    ["Rural.DrivewaySurface"] = "concrete",
                    ["Rural.WalkSurface"] = "concrete",
                    ["World.RoadLinePavedChance"] = 0.05f,
                    ["World.RoadLineAbsentChance"] = 0.005f,
                }, true));

            // 5. RAILROAD-TOWN - Towns forced on rail, heavy industrial
            Register(new ConfigProfile("railroad-town",
                "Classic railroad towns: every town on rail, heavy industrial, elevators dominate",
                new Dictionary<string, object>
                {
                    ["World.TownRailProximityMi"] = 2f,
                    ["World.RailSpacingMi"] = 30f,
                    ["World.RailSpurMaxDistanceMi"] = 1f,
                    ["World.RailSpurAlwaysHasElevator"] = true,
                    ["SmallTown.IndustrialDensity"] = 2.0f,
                    ["SmallTown.CommercialDensity"] = 0.8f,
                    ["SmallTown.RailIndustrialLotWidth.Min"] = 180f,
                    ["SmallTown.RailIndustrialLotWidth.Max"] = 300f,
                    ["SmallTown.RailIndustrialRowSpacing.Min"] = 40f,
                    ["SmallTown.RailIndustrialRowSpacing.Max"] = 60f,
                    ["SmallTown.ElevatorBinCount.Min"] = 6,
                    ["SmallTown.ElevatorBinCount.Max"] = 10,
                    ["Rural.BinCount.Min"] = 4,
                    ["Rural.BinCount.Max"] = 8,
                    ["World.TownClassCrossroadsChance"] = 0.1f,
                    ["World.TownClassHamletChance"] = 0.2f,
                    ["World.TownClassVillageChance"] = 0.4f,
                    ["World.TownClassTownChance"] = 0.3f,
                }, true));

            // 6. RIVER-VALLEY - More tributaries, floodplain farms, wetlands
            Register(new ConfigProfile("river-valley",
                "River valley setting: more tributaries, floodplain farms, wetlands",
                new Dictionary<string, object>
                {
                    ["NoiseConfig.TrunkRiverSpacingMi.Mean"] = 15f,
                    ["NoiseConfig.TrunkRiverSpacingMi.StdDev"] = 4f,
                    ["NoiseConfig.TributarySpacingMi.Mean"] = 4f,
                    ["NoiseConfig.TributarySpacingMi.StdDev"] = 1f,
                    ["NoiseConfig.TributaryLengthBaseMi.Mean"] = 3f,
                    ["NoiseConfig.RiverMeanderBaseAmpFt"] = 2000f,
                    ["NoiseConfig.RiverMeanderBaseWavelengthFt"] = 12000f,
                    ["NoiseConfig.RiverBankTreeChance"] = 0.7f,
                    ["NoiseConfig.TributaryBankTreeChance"] = 0.55f,
                    ["World.FarmDensity"] = 0.1f,
                    ["World.FarmMarginFt"] = 500f,
                    ["Rural.ShelterbeltChance"] = 0.3f,
                    ["Rural.ShelterbeltTreeSpacing"] = 12f,
                    ["NoiseConfig.WetlandDensity"] = 2.0f,
                    ["SmallTown.ResidentialDensity"] = 0.8f,
                    ["Countryside.FieldFeatureDensity"] = 1.5f,
                }, true));

            // 7. TESTING-MINIMAL - Tiny world, few features, fast generation
            Register(new ConfigProfile("testing-minimal",
                "Minimal features for fast testing and debugging",
                new Dictionary<string, object>
                {
                    ["SmallTown.ResidentialDensity"] = 0.1f,
                    ["SmallTown.CommercialDensity"] = 0.1f,
                    ["SmallTown.IndustrialDensity"] = 0.1f,
                    ["SmallTown.CivicDensity"] = 0.1f,
                    ["SmallTown.LandmarkDensity"] = 0.1f,
                    ["SmallTown.MaxExtrasPerTown"] = 0,
                    ["SmallTown.FourSideBlockChance"] = 0.5f,
                    ["TownBlock.HouseDensity"] = 0.1f,
                    ["TownBlock.AlleyChance"] = 0f,
                    ["TownBlock.StreetTreeChance"] = 0f,
                    ["Rural.OutbuildingDensity"] = 0.1f,
                    ["Rural.ShopChance"] = 0f,
                    ["Rural.PoleBarnChance"] = 0f,
                    ["Rural.GambrelBarnChance"] = 0f,
                    ["Rural.ShelterbeltChance"] = 0f,
                    ["World.TownDensity"] = 0.1f,
                    ["World.FarmDensity"] = 0.05f,
                    ["World.RailSpacingMi"] = 200f,
                    ["World.PavedSpacingMi"] = 50f,
                    ["Countryside.FarmsteadDensity"] = 0.1f,
                    ["Countryside.FieldFeatureDensity"] = 0.1f,
                    ["Render.ViewerMaxZoom"] = 50f,
                }, true));

            // 8. TESTING-MAX - Everything maxed, stress test
            Register(new ConfigProfile("testing-max",
                "Maximum density and features for stress testing",
                new Dictionary<string, object>
                {
                    ["SmallTown.ResidentialDensity"] = 3.0f,
                    ["SmallTown.CommercialDensity"] = 3.0f,
                    ["SmallTown.IndustrialDensity"] = 3.0f,
                    ["SmallTown.CivicDensity"] = 3.0f,
                    ["SmallTown.LandmarkDensity"] = 3.0f,
                    ["SmallTown.MaxExtrasPerTown"] = 5,
                    ["SmallTown.FourSideBlockChance"] = 1.0f,
                    ["SmallTown.RowAlleyChance"] = 1.0f,
                    ["SmallTown.StreetTreeChance"] = 1.0f,
                    ["TownBlock.HouseDensity"] = 3.0f,
                    ["TownBlock.AlleyChance"] = 1.0f,
                    ["TownBlock.StreetTreeChance"] = 1.0f,
                    ["Rural.OutbuildingDensity"] = 3.0f,
                    ["Rural.ShopChance"] = 1.0f,
                    ["Rural.PoleBarnChance"] = 1.0f,
                    ["Rural.GambrelBarnChance"] = 1.0f,
                    ["Rural.ShelterbeltChance"] = 1.0f,
                    ["World.TownDensity"] = 3.0f,
                    ["World.FarmDensity"] = 1.0f,
                    ["World.RailSpacingMi"] = 20f,
                    ["World.PavedSpacingMi"] = 5f,
                    ["World.TownRailProximityMi"] = 20f,
                    ["Countryside.FarmsteadDensity"] = 3.0f,
                    ["Countryside.FieldFeatureDensity"] = 3.0f,
                    ["Render.ViewerMaxZoom"] = 500f,
                }, true));
        }
    }
}