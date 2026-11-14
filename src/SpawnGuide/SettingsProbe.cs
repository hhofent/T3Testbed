using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    public static class SettingsProbe
    {
        sealed class Leaf
        {
            public string Path = "";
            public string[] Keys = Array.Empty<string>();
            public object Value = 0;
            public RangeAttribute? Range;
        }
        static IEnumerable<Leaf> Walk(object value, string path, string[] keys, RangeAttribute? range = null)
        {
            Type t = value.GetType();
            if (t == typeof(bool) || (!t.IsEnum && Type.GetTypeCode(t) >= TypeCode.SByte && Type.GetTypeCode(t) <= TypeCode.Decimal))
            { yield return new Leaf { Path = path, Keys = keys, Value = value, Range = range }; yield break; }
            if (t.IsEnum || value is string) yield break;
            if (value is IDictionary dict)
            {
                foreach (DictionaryEntry e in dict)
                    if (e.Value != null) foreach (var l in Walk(e.Value, path + "[" + e.Key + "]", keys.Concat(new[] { e.Key.ToString()! }).ToArray())) yield return l;
                yield break;
            }
            if (value is IEnumerable list)
            {
                int i = 0;
                foreach (var item in list) { if (item != null) foreach (var l in Walk(item, path + "[" + i + "]", keys.Concat(new[] { i.ToString() }).ToArray())) yield return l; i++; }
                yield break;
            }
            // IntDistribution serializes its underlying FloatDistribution directly.
            if (value is IntDistribution n) value = n.FloatDist;
            t = value.GetType();
            using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(value, ConfigSerialization.JsonOptions));
            var json = serialized.RootElement;
            foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                object? child;
                if (m is PropertyInfo p && p.CanRead && p.GetIndexParameters().Length == 0) child = p.GetValue(value);
                else if (m is FieldInfo f) child = f.GetValue(value);
                else continue;
                if (child == null || m.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
                string name = m.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? m.Name;
                if (m.Name == "BetaParam") name = "beta";
                string? key = json.ValueKind == JsonValueKind.Object ? json.EnumerateObject().Where(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(x => x.Name).FirstOrDefault() : null;
                if (json.ValueKind == JsonValueKind.Array && (value is FloatRange || value is IntRange))
                    key = m.Name == "Min" ? "0" : m.Name == "Max" ? "1" : null;
                if (key == null) continue;
                foreach (var l in Walk(child, path.Length == 0 ? m.Name : path + "." + m.Name, keys.Concat(new[] { key }).ToArray(), m.GetCustomAttribute<RangeAttribute>())) yield return l;
            }
        }
        static object? Tree(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object) return e.EnumerateObject().ToDictionary(p => p.Name, p => Tree(p.Value));
            if (e.ValueKind == JsonValueKind.Array) return e.EnumerateArray().Select(Tree).ToList();
            return e.Clone();
        }
        static SpawnConfig Mutate(string json, Leaf leaf, object value)
        {
            using var doc = JsonDocument.Parse(json);
            object root = Tree(doc.RootElement)!;
            object node = root;
            for (int i = 0; i < leaf.Keys.Length - 1; i++) node = node is Dictionary<string, object?> d ? d[leaf.Keys[i]]! : ((List<object?>)node)[int.Parse(leaf.Keys[i])]!;
            string last = leaf.Keys.Last();
            if (node is Dictionary<string, object?> obj) obj[last] = value; else ((List<object?>)node)[int.Parse(last)] = value;
            var copy = JsonSerializer.Deserialize<SpawnConfig>(JsonSerializer.Serialize(root), ConfigSerialization.JsonOptions)!;
            copy.Validate();
            return copy;
        }
        static IEnumerable<object> Nudges(Leaf l)
        {
            if (l.Value is bool b) { yield return !b; yield break; }
            double x = Convert.ToDouble(l.Value, CultureInfo.InvariantCulture);
            double step = Math.Abs(x) * .25;
            if (step == 0) step = l.Range == null ? .25 : (l.Range.Max - l.Range.Min) * .25;
            bool integer = l.Value is int || l.Value is uint || l.Value is long || l.Value is short || l.Value is byte;
            if (integer) step = Math.Max(1, Math.Round(step));
            foreach (int direction in new[] { -1, 1 })
            {
                double v = x + direction * step;
                if (l.Range != null) v = Math.Max(l.Range.Min, Math.Min(l.Range.Max, v));
                if (integer) v = Math.Round(v);
                if (l.Value is float) v = Math.Max(-float.MaxValue, Math.Min(float.MaxValue, v));
                object converted;
                try { converted = Convert.ChangeType(v, l.Value.GetType(), CultureInfo.InvariantCulture); }
                catch (OverflowException) { continue; }
                if (!converted.Equals(l.Value)) yield return converted;
            }
        }
        static string Hash(string svg) { using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(svg))); }
        static string Render(SpawnConfig c, uint seed, string map)
        {
            if (map.StartsWith("smalltown/")) return SvgMapRenderer.Render(SmallTownGenerator.Generate(seed, map.Substring(10), config: c.SmallTown, townTiers: c.TownTiers, commercial: c.Commercial));
            if (map == "country") return SvgMapRenderer.Render(CountrysideMap.Generate(seed, "hamlet", config: c.Countryside, townConfig: c));
            return SvgMapRenderer.Render(WorldMap.Generate(seed, 0, 0, config: c.World, noiseConfig: c.Noise, townConfig: c).Map);
        }
        static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        public static int Run(SpawnConfig baseline, string prefix = "", string output = "port-report/settings-probe")
        {
            string json = JsonSerializer.Serialize(baseline, ConfigSerialization.JsonOptions);
            var maps = new[] { "smalltown/crossroads", "smalltown/hamlet", "smalltown/village", "smalltown/town", "country", "world" };
            var seeds = new uint[] { 1993, 7, 42 };
            var expected = new Dictionary<string, string>();
            foreach (uint seed in seeds) foreach (string map in maps) expected[map + "@" + seed] = Hash(Render(baseline, seed, map));
            var csv = new List<string> { "path,status,affected_maps_and_seeds,mutations,errors" };
            var md = new List<string> { "# Automated settings probe", "", "Run from repository root, without --config: `dotnet src/SpawnGuide/bin/Release/net5.0/SpawnGuide.dll --probe-settings`.", "", "Reflection includes numeric/bool leaves, range/distribution fields, dictionary entries and arrays. Each mutation starts from an independent clone of the loaded configuration. Both directions use 25% (integers at least 1), clamped to declared ranges; zero uses 0.25 or 25% of its declared range. Bools flip. SHA-256 of SVG output is compared on seeds 1993, 7, 42 for four town sizes, country and the world origin window. ERROR takes precedence if any mutation fails validation or generation. DEAD means no sampled change, not proof of unreachable code. Schema/version and inactive distribution fields are included. No settings are wired by this tool.", "", "| Path | Status | Affected maps/seeds | Mutations | Errors |", "|---|---|---|---|---|" };
            var dead = new List<string> { "# Settings with no sampled output effect", "", "User decision pending: keep, wire later, or remove. DEAD is limited to the maps, seeds and mutations in settings-probe.md; viewer-only, house-only and inactive distribution settings can appear here. Nothing was removed or wired.", "" };
            int count = 0;
            var results = new System.Collections.Concurrent.ConcurrentBag<string[]>();
            var leaves = Walk(baseline, "", Array.Empty<string>()).Where(l => prefix.Split(',').Any(p => l.Path.StartsWith(p, StringComparison.Ordinal))).ToArray();
            System.Threading.Tasks.Parallel.ForEach(leaves, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, l =>
            {
                var affected = new SortedSet<string>(); var errors = new List<string>(); var mutations = new List<string>();
                foreach (object v in Nudges(l))
                {
                    string label = Convert.ToString(v, CultureInfo.InvariantCulture)!; mutations.Add(label);
                    SpawnConfig copy;
                    try { copy = Mutate(json, l, v); }
                    catch (Exception ex) { errors.Add(label + ": " + ex.Message); continue; }
                    foreach (uint seed in seeds) foreach (string map in maps)
                    {
                        try { if (Hash(Render(copy, seed, map)) != expected[map + "@" + seed]) affected.Add(map + "@" + seed); }
                        catch (Exception ex) { errors.Add(label + " " + map + "@" + seed + ": " + ex.Message); }
                    }
                }
                if (mutations.Count == 0) errors.Add("No distinct representable in-range mutation");
                string status = errors.Count > 0 ? "ERROR" : affected.Count > 0 ? "LIVE" : "DEAD";
                string[] cells = { l.Path, status, string.Join("; ", affected), string.Join("; ", mutations), string.Join("; ", errors).Replace("\r", " ").Replace("\n", " ") };
                results.Add(cells);
                int done = System.Threading.Interlocked.Increment(ref count);
                if (done % 100 == 0) Console.WriteLine("Probed " + done + "/" + leaves.Length);
            });
            foreach (var cells in results.OrderBy(r => r[0], StringComparer.Ordinal))
            {
                csv.Add(string.Join(",", cells.Select(Csv)));
                md.Add("| " + string.Join(" | ", cells.Select(x => x.Replace("|", "\\|"))) + " |");
                if (cells[1] == "DEAD") dead.Add("- `" + cells[0] + "`");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllLines(output + ".csv", csv);
            File.WriteAllLines(output + ".md", md);
            File.WriteAllLines(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, "dead-settings.md"), dead);
            Console.WriteLine("Probed " + count + "; reports written to " + output + ".{csv,md} and sibling dead-settings.md");
            return 0;
        }
    }
}

