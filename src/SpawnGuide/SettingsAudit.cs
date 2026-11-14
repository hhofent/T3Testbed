using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace SpawnGuide
{
    /// <summary>Empirical wiring probe. A sampled no-change is not proof a setting is unused.</summary>
    public static class SettingsAudit
    {
        static IEnumerable<(string Path, object Value, MemberInfo Member)> Leaves(object owner, string prefix)
        {
            foreach (var p in owner.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead || !p.CanWrite || p.GetIndexParameters().Length != 0) continue;
                object? value = p.GetValue(owner);
                if (value == null) continue;
                string path = prefix + p.Name;
                if (p.PropertyType.IsClass && p.PropertyType.Namespace == typeof(SpawnConfig).Namespace && !typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType))
                    foreach (var leaf in Leaves(value, path + ".")) yield return leaf;
                else yield return (path, value, p);
            }
        }

        static string? Mutation(object value)
        {
            if (value is float f) return (f == 0 ? 0.2f : f * 1.25f).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value is int i) return (i + 1).ToString();
            if (value is bool b) return (!b).ToString();
            if (value is string s) return s + " audit";
            if (value.GetType().IsEnum) return Enum.GetValues(value.GetType()).Cast<object>().FirstOrDefault(v => !v.Equals(value))?.ToString();
            if (value is FloatDistribution d) return JsonSerializer.Serialize(new FloatDistribution(d.Shape, d.Min, Math.Min(float.MaxValue, d.Max + Math.Max(1, Math.Abs(d.Max) * 0.25f)), d.Mean, d.StdDev, d.Mode, d.Lambda, d.Alpha, d.BetaParam, d.Weight1, d.Value1, d.Value2, d.Concentrations), ConfigSerialization.JsonOptions);
            if (value is IntDistribution n) return Mutation(n.FloatDist);
            return null;
        }

        static string Output(SpawnConfig c, uint seed)
        {
            var results = new List<string>();
            foreach (string arch in new[] { "ranch", "farmhouse", "split-level" }) results.Add(SvgRenderer.Render(HouseGenerator.Generate(arch, seed, config: c.Houses)));
            results.Add(SvgMapRenderer.Render(MapGenerator.RuralLot(seed, c.Rural)));
            results.Add(SvgMapRenderer.Render(MapGenerator.TownBlock(seed, c.TownBlock)));
            foreach (string size in new[] { "crossroads", "hamlet", "village", "town" }) results.Add(SvgMapRenderer.Render(SmallTownGenerator.Generate(seed, size, config: c.SmallTown, townTiers: c.TownTiers)));
            results.Add(SvgMapRenderer.Render(CountrysideMap.Generate(seed, "hamlet", config: c.Countryside)));
            results.Add(SvgMapRenderer.Render(WorldMap.Generate(seed, 0, 0, config: c.World, noiseConfig: c.Noise).Map));
            return string.Join("\n", results);
        }

        public static int Run(string path)
        {
            var baseline = SpawnConfig.Default;
            const uint seed = 1993;
            string expected = Output(baseline, seed);
            var rows = new List<string> {
                "# Settings wiring audit — empirical inventory", "",
                "Reproduce from repository root: `dotnet src/SpawnGuide/bin/Release/net5.0/SpawnGuide.dll --audit-settings --audit-output port-report/settings-audit.md`.", "",
                "This is an incomplete T4 audit, not a claim that T4 passes. Probe seed: 1993. Views: three house archetypes, rural lot, town block, four smalltown sizes, countryside and a world window. No conditional rules/repair are applied to mutations. Before-state wiring is unverified. A no-change result requires source review and broader seeds; rare features may be absent. Collections require dedicated structural mutations. Source locations below identify declarations, not generation reads. Rendering settings consumed by the separate viewer may not affect these SVG probes.", "",
                "| Setting | Wired before? | Observed output effect now? | Declaration | Probe / limitation |", "|---|---|---|---|---|" };
            int changed = 0, unchanged = 0, rejected = 0, structural = 0;
            foreach (var leaf in Leaves(baseline, ""))
            {
                string result, note;
                string? mutation = Mutation(leaf.Value);
                if (leaf.Path == "SchemaVersion" || mutation == null) { structural++; result = "not probed"; note = "Structural/collection/metadata setting: needs dedicated test"; }
                else
                {
                    var copy = JsonSerializer.Deserialize<SpawnConfig>(JsonSerializer.Serialize(baseline, ConfigSerialization.JsonOptions), ConfigSerialization.JsonOptions)!;
                    try {
                        ConfigOverrides.Apply(copy, leaf.Path + "=" + mutation);
                        copy.Validate();
                        bool different = Output(copy, seed) != expected;
                        if (different) changed++; else unchanged++;
                        result = different ? "YES (sampled)" : "NO CHANGE (sampled)";
                        note = "Mutation: " + mutation;
                    } catch (Exception ex) { rejected++; result = "not established"; note = "Probe rejected: " + ex.Message.Replace("\n", " "); }
                }
                string declaration = leaf.Member.DeclaringType!.Name + "." + leaf.Member.Name;
                foreach (string file in Directory.EnumerateFiles("src/SpawnGuide", "*.cs"))
                {
                    string[] lines = File.ReadAllLines(file);
                    int start = Array.FindIndex(lines, l => l.Contains("class " + leaf.Member.DeclaringType.Name));
                    if (start < 0) continue;
                    int at = Array.FindIndex(lines, start, l => l.Contains(" " + leaf.Member.Name + " {"));
                    if (at >= 0) declaration = file.Replace('\\', '/') + ":" + (at + 1);
                }
                rows.Add($"| {leaf.Path} | unverified | {result} | {declaration} | {note.Replace("|", "\\|")} |");
            }
            rows.Insert(4, $"Inventory: {changed + unchanged + rejected + structural} settings; changed {changed}; unchanged {unchanged}; rejected probes {rejected}; structural/unprobed {structural}.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllLines(path, rows);
            Console.WriteLine(rows[4]);
            return 0;
        }
    }
}


