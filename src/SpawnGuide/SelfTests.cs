using System;
using System.Globalization;

namespace SpawnGuide
{
    public static class SelfTests
    {
        public static int Run()
        {
            int passed = 0, failed = 0;
            void Test(string name, Action test)
            {
                try { test(); passed++; Console.WriteLine("[PASS] " + name); }
                catch (Exception ex) { failed++; Console.WriteLine("[FAIL] " + name + ": " + ex.Message); }
            }
            void Equal(object expected, object actual) { if (!expected.Equals(actual)) throw new Exception($"Expected {expected}, got {actual}"); }
            Test("BuilderGuy v2 migration preserves settings and initializes defaults", () => {
                var c = SpawnConfig.Default;
                c.SchemaVersion = 2;
                c.BuilderGuy = null!;
                c.Commercial.ElevatorWidthFt = 96;
                ConfigSerialization.Migrate(c, 2);
                Equal(3, c.SchemaVersion);
                Equal(96f, c.Commercial.ElevatorWidthFt);
                Equal(1990, c.BuilderGuy.SnapshotYear);
                Equal(0.7f, c.BuilderGuy.Commercial.DowntownShare);
                c.BuilderGuy.Setbacks.SidePercent = 12;
                ConfigSerialization.Migrate(c, 2);
                Equal(12d, c.BuilderGuy.Setbacks.SidePercent);
                var json = System.Text.Json.JsonSerializer.Serialize(c, ConfigSerialization.JsonOptions);
                var restored = System.Text.Json.JsonSerializer.Deserialize<SpawnConfig>(json, ConfigSerialization.JsonOptions)!;
                Equal(12d, restored.BuilderGuy.Setbacks.SidePercent);
            });
            Test("BuilderGuy scalar controls carry complete metadata", () => {
                foreach (var type in new[] { typeof(BuilderGuyConfig), typeof(BuilderGuyCommercialConfig), typeof(BuilderGuyHouseConfig), typeof(BuilderGuySetbackConfig), typeof(BuilderGuyRegionConfig) })
                    foreach (var p in type.GetProperties()) {
                        if (!p.PropertyType.IsValueType) continue;
                        foreach (var attribute in new[] { typeof(RangeAttribute), typeof(UnitsAttribute), typeof(RationaleAttribute), typeof(CategoryAttribute), typeof(SourceAttribute) })
                            if (!Attribute.IsDefined(p, attribute)) throw new Exception(type.Name + "." + p.Name + " missing " + attribute.Name);
                    }
            });
            void Reject(string expression)
            {
                try { ConfigOverrides.ApplyArguments(SpawnConfig.Default, new[] { "--param", expression }); }
                catch (ArgumentException) { return; }
                throw new Exception("Accepted invalid override: " + expression);
            }
            Test("nested towns preserve defaults and receive commercial overrides", () => {
                foreach (uint seed in new uint[] { 13, 31, 1993 }) {
                    var c = SpawnConfig.Default;
                    string Country(SpawnConfig? cfg) => SvgMapRenderer.Render(CountrysideMap.Generate(seed, "hamlet", townConfig: cfg));
                    string World(SpawnConfig? cfg) => SvgMapRenderer.Render(WorldMap.Generate(seed, 0, 0, townConfig: cfg).Map);
                    Equal(Country(null), Country(c));
                    Equal(World(null), World(c));
                    string before = Country(c);
                    c.Commercial.ElevatorWidthFt = 96f;
                    if (before == Country(c)) throw new Exception("Countryside discarded commercial override");
                    var near = WorldMap.NearestTown(seed, 0, 0);
                    if (near == null) throw new Exception("No test town");
                    string AtTown(SpawnConfig cfg) => SvgMapRenderer.Render(WorldMap.Generate(seed, near.CenterX, near.HighwayZ, townConfig: cfg).Map);
                    if (AtTown(SpawnConfig.Default) == AtTown(c)) throw new Exception("World discarded commercial override");
                }
            });
            Test("commercial wiring preserves four default town hashes", () => {
                string[] sizes = { "crossroads", "hamlet", "village", "town" };
                string[] hashes = { "10eb5b9b71e579c0a69023ac85a8401d6f01296c98ced6b9337ad5ef0cf09f42", "e6564d50c0520507b486cfc7e1de483bea3a6ead0451987089eebe08eb8012ad", "0f751409af8d231834601b56097405311812d15a8fabe5f8c37569f5d3ea0353", "cd041218749b9bce2e8d70f505a6e59fed44bbdec6dc84259e1f77023d9b7abc" };
                for (int i = 0; i < sizes.Length; i++) {
                    var c = SpawnConfig.Default;
                    string svg = SvgMapRenderer.Render(SmallTownGenerator.Generate(1993, sizes[i], config: c.SmallTown, townTiers: c.TownTiers, commercial: c.Commercial));
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    string hash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(svg))).Replace("-", "").ToLowerInvariant();
                    Equal(hashes[i], hash);
                }
            });
            Test("commercial controls affect towns across seeds", () => {
                foreach (string expression in new[] { "Commercial.ElevatorWidthFt=60", "Commercial.ElevatorDepthFt=100", "Commercial.ElevatorRightChance=0", "Commercial.BinSpacingFt=40", "Commercial.CrossStreetShopMax=0" }) {
                    bool changed = false;
                    foreach (uint seed in new uint[] { 13, 14, 31, 1993 }) {
                        var c = SpawnConfig.Default;
                        string before = SvgMapRenderer.Render(SmallTownGenerator.Generate(seed, "town", config: c.SmallTown, commercial: c.Commercial));
                        ConfigOverrides.Apply(c, expression);
                        string after = SvgMapRenderer.Render(SmallTownGenerator.Generate(seed, "town", config: c.SmallTown, commercial: c.Commercial));
                        changed |= before != after;
                    }
                    if (!changed) throw new Exception("No effect: " + expression);
                }
            });
            Test("commercial alias precedence preserves legacy profiles", () => {
                var c = SpawnConfig.Default;
                c.SmallTown.ElevatorWidth = 72;
                string Render(CommercialConfig? commercial) => SvgMapRenderer.Render(SmallTownGenerator.Generate(1993, "hamlet", config: c.SmallTown, commercial: commercial));
                Equal(Render(null), Render(c.Commercial));
                c.Commercial.ElevatorWidthFt = 80;
                if (Render(null) == Render(c.Commercial)) throw new Exception("Explicit Commercial value ignored");
            });
            Test("repeatable overrides and last value wins", () => {
                var c = SpawnConfig.Default;
                ConfigOverrides.ApplyArguments(c, new[] { "--param", "SmallTown.StreetRow=40", "--param", "smalltown.streetrow=42", "--param", "Rural.ShopChance=0.25" });
                Equal(42f, c.SmallTown.StreetRow); Equal(0.25f, c.Rural.ShopChance);
            });
            Test("override takes precedence over profile", () => {
                var c = ConfigProfiles.ApplyProfile(ConfigProfiles.GetNames()[0]);
                ConfigOverrides.ApplyArguments(c, new[] { "--param", "SmallTown.StreetRow=42" });
                Equal(42f, c.SmallTown.StreetRow);
            });
            Test("unknown path rejected", () => Reject("SmallTown.NoSuchSetting=1"));
            Test("unknown section rejected", () => Reject("Missing.StreetRow=1"));
            Test("malformed expression rejected", () => Reject("SmallTown.StreetRow"));
            Test("invalid numeric value rejected", () => Reject("SmallTown.StreetRow=wide"));
            Test("nonfinite value rejected", () => Reject("SmallTown.StreetRow=NaN"));
            Test("legacy probability validation rejects out of range", () => Reject("Rural.ShopChance=2"));
            Test("attribute range rejects out of range", () => Reject("Render.ViewerMaxZoom=0"));
            Test("invariant culture", () => {
                var previous = CultureInfo.CurrentCulture;
                try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); var c = SpawnConfig.Default; ConfigOverrides.Apply(c, "Rural.ShopChance=0.25"); Equal(0.25f, c.Rural.ShopChance); }
                finally { CultureInfo.CurrentCulture = previous; }
            });
            Test("missing argument rejected", () => {
                try { ConfigOverrides.ApplyArguments(SpawnConfig.Default, new[] { "--param" }); }
                catch (ArgumentException) { return; } throw new Exception("Accepted missing argument");
            });
            Test("sweep axis remains supported", () => ConfigOverrides.ApplyArguments(SpawnConfig.Default, new[] { "--analyze-sensitivity", "--param", "SmallTown.StreetRow" }));
            Test("immutable distribution bounds copied back", () => {
                var c = SpawnConfig.Default;
                ConfigOverrides.ApplyArguments(c, new[] { "--param", "SmallTown.LotFrontage.Min=45", "--param", "SmallTown.LotFrontage.Max=65" });
                Equal(45f, c.SmallTown.LotFrontage.Min); Equal(65f, c.SmallTown.LotFrontage.Max);
            });
            Test("inverted distribution rejected", () => Reject("SmallTown.LotFrontage.Min=10000"));
            foreach (string name in ConfigProfiles.GetNames())
            {
                Test("profile applies and JSON round-trips: " + name, () => {
                    var c = ConfigProfiles.ApplyProfile(name);
                    string json = System.Text.Json.JsonSerializer.Serialize(c, ConfigSerialization.JsonOptions);
                    var copy = System.Text.Json.JsonSerializer.Deserialize<SpawnConfig>(json, ConfigSerialization.JsonOptions)!;
                    Equal(json, System.Text.Json.JsonSerializer.Serialize(copy, ConfigSerialization.JsonOptions));
                });
            }
            Test("saved profile re-applies identically", () => {
                var profile = ConfigProfiles.Get("dense-suburban");
                string json = System.Text.Json.JsonSerializer.Serialize(profile, ConfigSerialization.JsonOptions);
                var copy = System.Text.Json.JsonSerializer.Deserialize<ConfigProfile>(json, ConfigSerialization.JsonOptions)!;
                var c = SpawnConfig.Default; copy.Apply(c);
                Equal(45f, c.SmallTown.LotFrontage.Min);
            });
            Test("inactive distribution parameters round-trip", () => {
                var d = new FloatDistribution(DistributionShape.Uniform, 2, 5, mean: 4, stdDev: 0.25f, alpha: 3);
                string json = System.Text.Json.JsonSerializer.Serialize(d, ConfigSerialization.JsonOptions);
                Equal(d, System.Text.Json.JsonSerializer.Deserialize<FloatDistribution>(json, ConfigSerialization.JsonOptions));
            });
            Test("binary cache preserves new config sections", () => {
                string path = System.IO.Path.GetTempFileName();
                try {
                    var c = SpawnConfig.Default; c.Noise.FarmDensity = 0.37f; c.TownTiers.KValue = 7;
                    ConfigSerialization.SaveBinary(c, path);
                    var loaded = ConfigSerialization.LoadBinary(path);
                    Equal(0.37f, loaded.Noise.FarmDensity); Equal(7, loaded.TownTiers.KValue);
                } finally { System.IO.File.Delete(path); }
            });
            Console.WriteLine($"Selftests: {passed} passed, {failed} failed (override coverage; remaining tier 1 tests pending).");
            return failed == 0 ? 0 : 1;
        }
    }
}
