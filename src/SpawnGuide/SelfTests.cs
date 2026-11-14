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
            void Reject(string expression)
            {
                try { ConfigOverrides.ApplyArguments(SpawnConfig.Default, new[] { "--param", expression }); }
                catch (ArgumentException) { return; }
                throw new Exception("Accepted invalid override: " + expression);
            }
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
