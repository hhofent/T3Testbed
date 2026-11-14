using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SpawnGuide;

static class Program
{
    static uint GetUInt(string[] args, string name, uint fallback)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length && uint.TryParse(args[i + 1], out var v) ? v : fallback;
    }

    static string GetString(string[] args, string name, string fallback)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    static bool GetBool(string[] args, string name)
    {
        return Array.IndexOf(args, name) >= 0;
    }

    static readonly string[] ValidTownSizes = { "crossroads", "hamlet", "village", "town" };

    static bool IsValidTownSize(string sizeName)
    {
        return Array.IndexOf(ValidTownSizes, (sizeName ?? "").ToLowerInvariant()) >= 0;
    }

    // Load configuration
    static SpawnConfig LoadConfig(string[] args)
    {
        string configPath = GetString(args, "--config", "");
        if (string.IsNullOrEmpty(configPath))
        {
            string[] searchPaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "spawnconfig.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "out", "spawnconfig.json"),
            };
            foreach (var p in searchPaths)
            {
                if (File.Exists(p)) { configPath = p; break; }
            }
        }

        var config = string.IsNullOrEmpty(configPath)
            ? SpawnConfig.Default
            : SpawnConfig.Load(configPath);

        string densityArg = GetString(args, "--density", "");
        if (float.TryParse(densityArg, out float density) && density > 0)
        {
            config = SpawnConfig.WithDensityMultiplier(config, density);
        }

        // Apply profile if specified
        string profileName = GetString(args, "--profile", "");
        if (!string.IsNullOrEmpty(profileName))
        {
            config = ConfigProfiles.ApplyProfile(profileName, config);
        }

        // Apply conditional rules
        config.ApplyConditionalRules();

        if (args.Contains("--save-config"))
        {
            string savePath = GetString(args, "--save-config-path", Path.Combine(Directory.GetCurrentDirectory(), "spawnconfig.json"));
            config.Save(savePath);
            Console.WriteLine($"Saved config to {savePath}");
        }

        return config;
    }

    // Sensitivity analysis
    static void RunSensitivityAnalysis(string[] args, SpawnConfig config)
    {
        string paramPath = GetString(args, "--param", "");
        if (string.IsNullOrEmpty(paramPath))
        {
            Console.WriteLine("Usage: --analyze-sensitivity --param <path> --range <min,max> --steps <n> --seeds <n> --metrics <list> --output <file> [--spacing linear|log] [--parallel true|false]");
            return;
        }

        string rangeStr = GetString(args, "--range", "0.5,2.0");
        var rangeParts = rangeStr.Split(',');
        if (rangeParts.Length != 2 || !float.TryParse(rangeParts[0], out float minVal) || !float.TryParse(rangeParts[1], out float maxVal))
        {
            Console.WriteLine("Invalid --range format. Use 'min,max'");
            return;
        }

        int steps = (int)GetUInt(args, "--steps", 10);
        int seeds = (int)GetUInt(args, "--seeds", 20);
        string metricsStr = GetString(args, "--metrics", "houses,outbuildings,blocks,vacancy,roadLength,townArea,farmCount,riverLength,railLength");
        var metrics = metricsStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(m => m.Trim()).ToArray();
        var metricNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["houses"] = nameof(SensitivityResult.Houses), ["outbuildings"] = nameof(SensitivityResult.Outbuildings),
            ["blocks"] = nameof(SensitivityResult.Blocks), ["vacancy"] = nameof(SensitivityResult.Vacancy),
            ["roadLength"] = nameof(SensitivityResult.RoadLength), ["townArea"] = nameof(SensitivityResult.TownArea),
            ["farmCount"] = nameof(SensitivityResult.FarmCount), ["riverLength"] = nameof(SensitivityResult.RiverLength),
            ["railLength"] = nameof(SensitivityResult.RailLength),
        };
        var unknownMetrics = metrics.Where(m => !metricNames.ContainsKey(m)).ToArray();
        if (unknownMetrics.Length > 0) { Console.WriteLine($"Unknown metric(s): {string.Join(", ", unknownMetrics)}"); return; }
        string outputPath = GetString(args, "--output", "sensitivity.csv");
        string spacing = GetString(args, "--spacing", "linear");
        int parallelIndex = Array.IndexOf(args, "--parallel");
        bool parallel = parallelIndex >= 0 && (parallelIndex + 1 >= args.Length || !bool.TryParse(args[parallelIndex + 1], out var parallelValue) || parallelValue);

        Console.WriteLine($"Sensitivity Analysis: {paramPath} from {minVal} to {maxVal} in {steps} steps, {seeds} seeds each");

        var results = new List<SensitivityResult>();
        var paramValues = new float[steps];
        for (int i = 0; i < steps; i++)
        {
            float t = steps == 1 ? 0.5f : (float)i / (steps - 1);
            paramValues[i] = spacing == "log" ? minVal * (float)Math.Pow(maxVal / minVal, t) : minVal + (maxVal - minVal) * t;
        }

        int totalRuns = paramValues.Length * seeds;
        int completed = 0;

        var lockObj = new object();

        Action<int, float> runOne = (seedIdx, val) =>
        {
            uint seed = (uint)(1993 + seedIdx * 10007);
            var testConfig = config.DeepClone();
            SetConfigValue(testConfig, paramPath, val);

            // Generate world
            var ww = WorldMap.Generate(seed, 0f, 0f, WorldMap.CellFeet * 3f, WorldMap.CellFeet * 3f, testConfig.World, testConfig.Noise);
            var smalltown = SmallTownGenerator.Generate(seed, "hamlet", false, TownContext.None, testConfig.SmallTown);

            var result = new SensitivityResult
            {
                ParameterValue = val,
                Seed = seed,
                Houses = ww.Map.Houses.Count,
                Outbuildings = ww.Map.Outbuildings.Count,
                Blocks = smalltown.LotLines.Count,
                Vacancy = smalltown.LotLines.Count(l => !smalltown.Houses.Any(h =>
                    h.Plan.Footprint.Average(p => p.X) >= l.X && h.Plan.Footprint.Average(p => p.X) < l.X + l.W &&
                    h.Plan.Footprint.Average(p => p.Z) >= l.Z && h.Plan.Footprint.Average(p => p.Z) < l.Z + l.H)),
                RoadLength = ww.Map.Roads.Sum(r => r.W * r.H),
                TownArea = ww.Towns.Sum(t => (t.MaxX - t.MinX) * (t.MaxZ - t.MinZ)),
                FarmCount = ww.Farms.Count,
                RiverLength = ww.Map.Waters.Sum(w => w.Points.Count),
                RailLength = ww.Map.Rails.Sum(r => r.Points.Count),
            };

            lock (lockObj)
            {
                results.Add(result);
                completed++;
                if (completed % 10 == 0) Console.Write($"  {completed}/{totalRuns} ");
            }
        };

        if (parallel)
        {
            Parallel.ForEach(paramValues, val =>
            {
                for (int s = 0; s < seeds; s++) runOne(s, val);
            });
        }
        else
        {
            foreach (var val in paramValues)
                for (int s = 0; s < seeds; s++)
                    runOne(s, val);
        }

        Console.WriteLine($"\nCompleted {completed} runs. Writing {outputPath}...");

        // Write CSV
        using (var sw = new StreamWriter(outputPath))
        {
            sw.WriteLine("ParameterValue,Seed," + string.Join(",", metrics));
            foreach (var r in results.OrderBy(x => x.ParameterValue).ThenBy(x => x.Seed))
            {
                var values = metrics.Select(m => typeof(SensitivityResult).GetField(metricNames[m])!.GetValue(r));
                sw.WriteLine($"{r.ParameterValue},{r.Seed},{string.Join(",", values)}");
            }
        }

        // Summary
        Console.WriteLine("\nSummary (mean ± std):");
        foreach (var val in paramValues)
        {
            var subset = results.Where(r => Math.Abs(r.ParameterValue - val) < 0.001f).ToList();
            if (subset.Count == 0) continue;
            Console.WriteLine($"  {paramPath}={val:F3}: " + string.Join(", ", metrics.Select(m => { var field = typeof(SensitivityResult).GetField(metricNames[m])!; var nums = subset.Select(x => Convert.ToDouble(field.GetValue(x))).ToArray(); double mean = nums.Average(); double sd = nums.Length <= 1 ? 0 : Math.Sqrt(nums.Select(n => (n - mean) * (n - mean)).Average()); return $"{m}={mean:F1}±{sd:F1}"; })));
        }

        Console.WriteLine($"\nDone. Results saved to {outputPath}");
    }

    static float Mean(IEnumerable<int> values) { var arr = values.ToArray(); return arr.Length == 0 ? 0 : (float)arr.Average(); }
    static float StdDev(IEnumerable<int> values) { var arr = values.ToArray(); if (arr.Length <= 1) return 0; float m = (float)arr.Average(); return (float)Math.Sqrt(arr.Select(x => (x - m) * (x - m)).Average()); }

    class SensitivityResult
    {
        public float ParameterValue;
        public uint Seed;
        public int Houses;
        public int Outbuildings;
        public int Blocks;
        public int Vacancy;
        public float RoadLength;
        public float TownArea;
        public int FarmCount;
        public int RiverLength;
        public int RailLength;
    }

    // Config value setter (simplified - uses reflection)
    static void SetConfigValue(SpawnConfig config, string path, float value)
    {
        string[] parts = path.Split('.');
        object current = config;
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
            targetProp.SetValue(current, Convert.ChangeType(value, targetProp.PropertyType));
        else if (targetField != null)
            targetField.SetValue(current, Convert.ChangeType(value, targetField.FieldType));
    }

    // Deep clone helper
    static SpawnConfig DeepClone(this SpawnConfig config)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        return System.Text.Json.JsonSerializer.Deserialize<SpawnConfig>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    static int Main(string[] args)
    {
        var config = LoadConfig(args);

        if (args.Contains("--probe-rail"))
        {
            uint pSeed = GetUInt(args, "--seed", 1993u);
            WorldMap.ProbeRail(pSeed);
            return 0;
        }

        if (args.Contains("--verify"))
        {
            uint vSeed = GetUInt(args, "--seed", 1993u);
            Console.WriteLine($"SpawnGuide world verification — seed {vSeed}");
            var checks = WorldMap.Verify(vSeed);
            int failed = 0;
            foreach (var c in checks)
            {
                Console.WriteLine($"  [{(c.Pass ? "PASS" : "FAIL")}] {c.Name} — {c.Detail}");
                if (!c.Pass) failed++;
            }
            Console.WriteLine(failed == 0 ? "all checks passed" : $"{failed} check(s) failed");
            return failed == 0 ? 0 : 1;
        }

        if (args.Contains("--analyze-sensitivity"))
        {
            RunSensitivityAnalysis(args, config);
            return 0;
        }

        if (args.Contains("--list-profiles"))
        {
            Console.WriteLine("Built-in profiles:");
            foreach (var name in ConfigProfiles.GetNames())
            {
                var p = ConfigProfiles.Get(name);
                Console.WriteLine($"  {name}: {p.Description}");
            }
            return 0;
        }

        if (args.Contains("--migrate-config"))
        {
            string filePath = GetString(args, "--migrate-config", "");
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Console.WriteLine("Usage: --migrate-config <path>");
                return 1;
            }
            ConfigSerialization.BackupConfig(filePath);
            var cfg = SpawnConfig.Load(filePath);
            cfg.Save(filePath);
            Console.WriteLine($"Migrated {filePath} to schema v{ConfigSerialization.CurrentSchemaVersion}");
            return 0;
        }

        if (args.Contains("--validate-config"))
        {
            string filePath = GetString(args, "--validate-config", "");
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Console.WriteLine("Usage: --validate-config <path>");
                return 1;
            }
            var cfg = SpawnConfig.Load(filePath);
            var results = ConfigValidator.Validate(cfg);
            int errors = results.Count(r => r.Severity == ValidationSeverity.Error);
            int warnings = results.Count(r => r.Severity == ValidationSeverity.Warning);
            int repairs = results.Count(r => r.Severity == ValidationSeverity.Repair);
            Console.WriteLine($"Validation: {errors} errors, {warnings} warnings, {repairs} repairs");
            foreach (var r in results)
                Console.WriteLine($"  [{r.Severity}] {r.ParameterPath}: {r.Message}");
            return errors == 0 ? 0 : 1;
        }

        if (args.Contains("--diff-config"))
        {
            int idx = Array.IndexOf(args, "--diff-config");
            if (idx < 0 || idx + 2 >= args.Length)
            {
                Console.WriteLine("Usage: --diff-config <old> <new>");
                return 1;
            }
            string oldPath = args[idx + 1], newPath = args[idx + 2];
            if (!File.Exists(oldPath) || !File.Exists(newPath))
            {
                Console.WriteLine("Both files must exist");
                return 1;
            }
            var oldCfg = SpawnConfig.Load(oldPath);
            var newCfg = SpawnConfig.Load(newPath);
            string diff = ConfigSerialization.Diff(oldCfg, newCfg);
            Console.WriteLine(diff);
            return 0;
        }

        if (args.Contains("--test-profiles"))
        {
            Console.WriteLine("Testing all profiles round-trip...");
            foreach (var name in ConfigProfiles.GetNames())
            {
                var cfg = ConfigProfiles.ApplyProfile(name);
                cfg.ValidateAndRepair();
                Console.WriteLine($"  {name}: OK");
            }
            Console.WriteLine("All profiles validated.");
            return 0;
        }

        if (args.Contains("--serve"))
        {
            int port = (int)GetUInt(args, "--port", 8735u);
            var listener = new System.Net.HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            var worldCache = new Dictionary<string, string>();
            var worldCacheOrder = new Queue<string>();
            Console.WriteLine($"SpawnGuide server on http://127.0.0.1:{port}/ (Ctrl+C to stop)");
            while (listener.IsListening)
            {
                var ctx = listener.GetContext();
                var url = ctx.Request.Url;
                try { ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*"); } catch { }
                if (ctx.Request.HttpMethod == "HEAD")
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.Close();
                    continue;
                }
                if (url?.LocalPath == "/world-center")
                {
                    try
                    {
                        var qs = (url.Query ?? "").TrimStart('?');
                        var q = qs.Split('&', StringSplitOptions.RemoveEmptyEntries)
                            .ToDictionary(p => p.Split('=')[0], p => p.Split('=').Length > 1 ? p.Split('=')[1] : "", StringComparer.OrdinalIgnoreCase);
                        if (!uint.TryParse(q.GetValueOrDefault("seed", ""), out var s)) s = 1993;
                        var near = WorldMap.NearestTown(s, 0f, 0f, 80f);
                        float cx = near != null ? (near.MinX + near.MaxX) / 2f : 0f;
                        float cz = near != null ? (near.MinZ + near.MaxZ) / 2f : 0f;
                        float span = WorldMap.CellFeet * 3f;
                        string json = $"{{\"seed\":{s},\"cx\":{cx.ToString("0.##", CultureInfo.InvariantCulture)},\"cz\":{cz.ToString("0.##", CultureInfo.InvariantCulture)},\"span\":{span.ToString("0.##", CultureInfo.InvariantCulture)}}}";
                        ctx.Response.ContentType = "application/json; charset=utf-8";
                        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("world-center error: " + ex.Message);
                    }
                    finally { try { ctx.Response.Close(); } catch { } }
                    continue;
                }
                if (url?.LocalPath == "/world")
                {
                    try
                    {
                        var qs = (url.Query ?? "").TrimStart('?');
                        var q = qs.Split('&', StringSplitOptions.RemoveEmptyEntries)
                            .ToDictionary(p => p.Split('=')[0], p => p.Split('=').Length > 1 ? p.Split('=')[1] : "", StringComparer.OrdinalIgnoreCase);
                        if (!uint.TryParse(q.GetValueOrDefault("seed", ""), out var s)) s = 1993;
                        if (!float.TryParse(q.GetValueOrDefault("cx", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var cx)) cx = 0;
                        if (!float.TryParse(q.GetValueOrDefault("cz", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var cz)) cz = 0;
                        if (!float.TryParse(q.GetValueOrDefault("span", "47520"), NumberStyles.Float, CultureInfo.InvariantCulture, out var span)) span = 47520f;
                        span = Math.Clamp(span, config.World.MinSpan, WorldMap.Mile * 30f);
                        bool farLod = q.GetValueOrDefault("lod", "1") == "0";
                        float drawW;
                        if (float.TryParse(q.GetValueOrDefault("w", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var wq) && wq > 0f)
                            drawW = Math.Clamp(wq, 200f, 3600f);
                        else
                        {
                            float pp = float.TryParse(q.GetValueOrDefault("ppf", "0.03"), NumberStyles.Float, CultureInfo.InvariantCulture, out var p2) && p2 > 0f ? p2 : 0.03f;
                            drawW = Math.Clamp(pp * span, 200f, 3600f);
                        }
                        string key = string.Join("|", s.ToString(CultureInfo.InvariantCulture), cx.ToString("R", CultureInfo.InvariantCulture), cz.ToString("R", CultureInfo.InvariantCulture), span.ToString("R", CultureInfo.InvariantCulture), farLod ? "far" : "detail", drawW.ToString("R", CultureInfo.InvariantCulture));
                        if (!worldCache.TryGetValue(key, out var svg))
                        {
                            var ww = WorldMap.Generate(s, cx, cz, span, span);
                            if (farLod) ww.Map.LodSimple = true;
                            svg = SvgMapRenderer.Render(ww.Map, bare: true, targetWidth: drawW);
                            worldCache[key] = svg;
                            worldCacheOrder.Enqueue(key);
                            while (worldCacheOrder.Count > 24)
                            {
                                var old = worldCacheOrder.Dequeue();
                                worldCache.Remove(old);
                            }
                        }
                        ctx.Response.ContentType = "image/svg+xml; charset=utf-8";
                        var bytes = System.Text.Encoding.UTF8.GetBytes(svg);
                        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("world error: " + ex.Message);
                    }
                    finally { try { ctx.Response.Close(); } catch { } }
                    continue;
                }
                if (url?.LocalPath == "/gen")
                {
                    try
                    {
                        var qs = (url.Query ?? "").TrimStart('?');
                        var q = qs.Split('&', StringSplitOptions.RemoveEmptyEntries).ToDictionary(p => p.Split('=')[0], p => p.Split('=').Length > 1 ? p.Split('=')[1] : "", StringComparer.OrdinalIgnoreCase);
                        if (!uint.TryParse(q.GetValueOrDefault("seed", ""), out var s)) s = 1993;
                        var sz = q.GetValueOrDefault("size", "hamlet");
                        if (!IsValidTownSize(sz))
                        {
                            ctx.Response.StatusCode = 400;
                            ctx.Response.ContentType = "text/plain; charset=utf-8";
                            var msg = $"Invalid size '{sz}'. Valid sizes: crossroads, hamlet, village, town.";
                            var errBytes = System.Text.Encoding.UTF8.GetBytes(msg);
                            ctx.Response.OutputStream.Write(errBytes, 0, errBytes.Length);
                            ctx.Response.Close();
                            continue;
                        }
                        var map = SmallTownGenerator.Generate(s, sz, false, null, config.SmallTown, config.TownTiers);
                        var svg = SvgMapRenderer.Render(map);
                        ctx.Response.ContentType = "image/svg+xml";
                        var bytes = System.Text.Encoding.UTF8.GetBytes(svg);
                        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("gen error: " + ex.Message);
                    }
                    finally { try { ctx.Response.Close(); } catch { } }
                    continue;
                }
                if (url?.LocalPath == "/params")
                {
                    // Serve the parameter explorer UI
                    var paramExplorerPath = Path.Combine(Directory.GetCurrentDirectory(), "out", "param-explorer.html");
                    if (File.Exists(paramExplorerPath))
                    {
                        ctx.Response.ContentType = "text/html; charset=utf-8";
                        ctx.Response.OutputStream.Write(File.ReadAllBytes(paramExplorerPath), 0, (int)new FileInfo(paramExplorerPath).Length);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 404;
                        var bytes = System.Text.Encoding.UTF8.GetBytes("Parameter explorer not found. Run with --generate-param-explorer first.");
                        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    }
                    ctx.Response.Close();
                    continue;
                }
                var localPath = url?.LocalPath == "/" ? "/viewer.html" : url?.LocalPath;
                var path = Path.Combine(Directory.GetCurrentDirectory(), "out", localPath?.TrimStart('/') ?? "viewer.html");
                if (File.Exists(path))
                {
                    var mime = path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? "text/html" : path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? "image/svg+xml" : "application/octet-stream";
                    ctx.Response.ContentType = mime;
                    ctx.Response.OutputStream.Write(File.ReadAllBytes(path), 0, (int)new FileInfo(path).Length);
                }
                ctx.Response.Close();
            }
            return 0;
        }

        // --- Main generation ---
        var archetype = GetString(args, "--archetype", "all");
        var mapMode = GetString(args, "--map", "all");
        var townSize = GetString(args, "--size", "hamlet");
        var seed = GetUInt(args, "--seed", 1993);
        var seedsArg = GetString(args, "--seeds", "");
        var extraSeeds = seedsArg.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => uint.TryParse(s.Trim(), out var v) ? v : (uint?)null)
            .Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var bake = args.Contains("--bake");
        var bakedPool = new List<uint> { 1993, 42, 2024, 7, 97, 123, 555, 777, 1001, 1337, 1999, 2718, 4096 };
        var smalltownSeeds = extraSeeds.Count > 0 ? extraSeeds : bake ? bakedPool : new List<uint> { seed };
        var outDir = GetString(args, "--out", Path.Combine(Directory.GetCurrentDirectory(), "out"));
        Directory.CreateDirectory(outDir);

        var sections = new List<HtmlSection>();

        HtmlSection MapSection(MapPlan map, string fileName)
        {
            var svg = SvgMapRenderer.Render(map);
            File.WriteAllText(Path.Combine(outDir, fileName), svg);
            return new HtmlSection
            {
                Title = map.Title,
                FileName = fileName,
                Svg = svg,
                Facts =
                {
                    ("seed", map.Seed.ToString()),
                    ("span", $"{map.WidthFeet:0}' × {map.DepthFeet:0}'"),
                    ("houses", map.Houses.Count.ToString()),
                    ("outbuildings", map.Outbuildings.Count.ToString()),
                    ("driveways", map.Driveways.Count.ToString()),
                    ("walks", map.Walks.Count.ToString()),
                },
            };
        }

        // --- Maps first ---
        MapPlan? rural = null, town = null, smalltown = null;
        if (mapMode is "rural" or "all")
        {
            rural = MapGenerator.RuralLot(seed, config.Rural);
            sections.Add(MapSection(rural, $"rural-{seed}.svg"));
        }
        if (mapMode is "town" or "all")
        {
            town = MapGenerator.TownBlock(seed, config.TownBlock);
            sections.Add(MapSection(town, $"town-{seed}.svg"));
        }
        if (mapMode is "smalltown" or "all")
        {
            foreach (var s in smalltownSeeds)
            {
                var m = SmallTownGenerator.Generate(s, townSize, detail: false, ctx: null, config: config.SmallTown, townTiers: config.TownTiers);
                sections.Add(MapSection(m, $"smalltown-{s}.svg"));
                if (smalltown == null) smalltown = m;
            }
        }
        if (mapMode is "country" or "all")
        {
            foreach (var (sz, es) in new[] { ("hamlet", (string?)null), ("village", null), ("town", "west"), ("hamlet", "north") })
            {
                string stem = $"country-{seed}-{sz}{(es != null ? "-" + es : "")}";
                var m = CountrysideMap.Generate(seed, sz, detail: false, edge: es, config: config.Countryside);
                sections.Add(MapSection(m, $"{stem}.svg"));
                var md = CountrysideMap.Generate(seed, sz, detail: true, edge: es, config: config.Countryside);
                File.WriteAllText(Path.Combine(outDir, $"{stem}-detail.svg"), SvgMapRenderer.Render(md));
            }
        }

        // Infinite world
        WorldWindow? world = null;
        if (mapMode is "world" or "all")
        {
            var near = WorldMap.NearestTown(seed, 0f, 0f, 80f);
            float wcx = near != null ? (near.MinX + near.MaxX) / 2f : 0f;
            float wcz = near != null ? (near.MinZ + near.MaxZ) / 2f : 0f;
            float span = WorldMap.CellFeet * 3f;
            string ctr = GetString(args, "--center", "");
            if (ctr.Contains(','))
            {
                var parts = ctr.Split(',');
                if (parts.Length == 2 &&
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float cx0) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float cz0))
                { wcx = cx0; wcz = cz0; }
            }
            span = GetUInt(args, "--span", 0) > 0 ? GetUInt(args, "--span", 0) : span;
            world = WorldMap.Generate(seed, wcx, wcz, span, span, config.World, config.Noise);
            string worldFile = $"world-{seed}-9mi.svg";
            string worldSvg = SvgMapRenderer.Render(world.Map, bare: true, targetWidth: 1400f);
            File.WriteAllText(Path.Combine(outDir, worldFile), worldSvg);
            sections.Add(new HtmlSection
            {
                Title = $"INFINITE WORLD — 9 MI LENS WINDOW — SEED {seed}",
                FileName = worldFile,
                Svg = worldSvg,
                Facts =
                {
                    ("center", $"{wcx / WorldMap.Mile:0.#}, {wcz / WorldMap.Mile:0.#} mi"),
                    ("span", "9 × 9 mi (3 × 3 cells)"),
                    ("towns", string.Join(", ", world.Towns.Select(t => t.Name))),
                    ("farms", world.FarmsInWindow.ToString()),
                    ("houses", world.Map.Houses.Count.ToString()),
                    ("outbuildings", world.Map.Outbuildings.Count.ToString()),
                    ("creeks", world.Map.Waters.Count.ToString()),
                },
            });

            if (Environment.GetEnvironmentVariable("SPAWNGUIDE_PROBE") == "1")
                foreach (var t in world.Towns)
                    Console.Error.WriteLine($"PROBE town {t.Name} cx={t.CenterX:0} hwZ={t.HighwayZ:0} spur={t.Rail.HasRailSpur} off={t.Rail.SpurOffsetFt:0} railZ={t.Rail.RailZ:0} minX={t.MinX:0} maxX={t.MaxX:0} minZ={t.MinZ:0} maxZ={t.MaxZ:0} layout={t.Layout}");

            var cell = WorldMap.Generate(seed, wcx, wcz, WorldMap.CellFeet, WorldMap.CellFeet, config.World, config.Noise);
            string cellFile = $"world-{seed}-cell.svg";
            string cellSvg = SvgMapRenderer.Render(cell.Map, bare: true, targetWidth: 1100f);
            File.WriteAllText(Path.Combine(outDir, cellFile), cellSvg);
            sections.Add(new HtmlSection
            {
                Title = $"INFINITE WORLD — SINGLE 3-MILE CELL — SEED {seed}",
                FileName = cellFile,
                Svg = cellSvg,
                Facts =
                {
                    ("center", $"{wcx / WorldMap.Mile:0.#}, {wcz / WorldMap.Mile:0.#} mi"),
                    ("span", "3 × 3 mi"),
                    ("towns", string.Join(", ", cell.Towns.Select(t => t.Name))),
                    ("farms", cell.FarmsInWindow.ToString()),
                    ("houses", cell.Map.Houses.Count.ToString()),
                },
            });
        }

        // Houses
        var archetypes = archetype == "all" ? HouseGenerator.Archetypes : new[] { archetype };
        foreach (var name in archetypes)
        {
            uint archetypeSeed = archetype == "all" ? seed + (uint)Array.IndexOf(HouseGenerator.Archetypes, name) * 7919u : seed;
            var plan = HouseGenerator.Generate(name, archetypeSeed, garageMode: "integral", config: config.Houses);
            string svg = SvgRenderer.Render(plan);
            string fileName = $"{plan.Archetype}-{plan.Seed}.svg";
            File.WriteAllText(Path.Combine(outDir, fileName), svg);

            int windows = plan.Walls.Sum(w => w.Openings.Count(o => o.Kind == OpeningKind.Window));
            int doors = plan.Walls.Sum(w => w.Openings.Count(o => o.Kind == OpeningKind.Door));
            int garages = plan.Walls.Sum(w => w.Openings.Count(o => o.Kind == OpeningKind.GarageDoor));

            sections.Add(new HtmlSection
            {
                Title = plan.Archetype.ToUpperInvariant(),
                FileName = fileName,
                Svg = svg,
                Facts =
                {
                    ("footprint", $"{plan.WidthFeet:0.#}' × {plan.DepthFeet:0.#}'"),
                    ("stories", plan.Storeys.ToString()),
                    ("walls", plan.Walls.Count.ToString()),
                    ("windows", windows.ToString()),
                    ("doors", doors.ToString()),
                    ("garage doors", garages.ToString()),
                    ("roof", plan.RoofNote),
                    ("siding", plan.SidingNote),
                },
            });

            Console.WriteLine(AsciiRenderer.Render(plan));
            Console.WriteLine($"walls {plan.Walls.Count} | openings {plan.OpeningCount} | roof: {plan.RoofNote} | siding: {plan.SidingNote}");
            Console.WriteLine($"wrote {Path.Combine(outDir, fileName)}");
            Console.WriteLine();
        }

        if (rural != null)
        {
            var counts = rural.Outbuildings.GroupBy(o => o.Kind).Select(g => $"{g.Key} ×{g.Count()}");
            Console.WriteLine($"rural farmstead: {rural.Outbuildings.Count} outbuildings ({string.Join(", ", counts)}), {rural.Driveways.Count} lanes, {rural.Trees.Count} trees");
        }
        if (town != null)
        {
            Console.WriteLine($"town block: {town.Houses.Count} houses, {town.LotLines.Count} lot lines, {(town.Alleys.Count > 0 ? "with alley" : "no alley")}, {town.Trees.Count} trees");
        }
        foreach (var s in sections.Where(s => s.FileName.StartsWith("smalltown-") || s.FileName.StartsWith("country-")))
            Console.WriteLine($"{s.FileName}: {s.Facts.First(f => f.Item1 == "houses").Item2} houses, {s.Facts.First(f => f.Item1 == "outbuildings").Item2} buildings/anchors, {s.Facts.First(f => f.Item1 == "span").Item2}");
        if (world != null)
            Console.WriteLine($"world: {world.Towns.Count} town(s), {world.FarmsInWindow} farms in window, {world.Map.Houses.Count} houses, {world.Map.Outbuildings.Count} outbuildings, {world.Map.Waters.Count} creek runs");

        string indexPath = Path.Combine(outDir, "index.html");
        File.WriteAllText(indexPath, HtmlIndex.Render(sections));
        Console.WriteLine($"wrote {indexPath}");

        var featured = sections.FirstOrDefault(s => s.FileName.StartsWith("smalltown-"))
            ?? sections.FirstOrDefault(s => s.FileName.StartsWith("rural-"))
            ?? sections[0];
        string examplePath = Path.Combine(outDir, "example.html");
        File.WriteAllText(examplePath, HtmlIndex.Render(new List<HtmlSection> { featured }));
        Console.WriteLine($"wrote {examplePath}");

        var townSections = sections.Where(s => s.FileName.StartsWith("smalltown-") || s.FileName.StartsWith("country-") || s.FileName.StartsWith("world-")).ToList();
        if (townSections.Count > 1)
        {
            string galleryPath = Path.Combine(outDir, "examples.html");
            File.WriteAllText(galleryPath, HtmlIndex.Render(townSections));
            Console.WriteLine($"wrote {galleryPath}");
        }
        foreach (var s in townSections)
            Console.WriteLine($"gallery: {s.FileName}");

        // Viewer
        static string SvgDataUri(string fileName)
            => "data:image/svg+xml;utf8," + File.ReadAllText(fileName)
                .Replace("%", "%25").Replace("<", "%3C").Replace(">", "%3E").Replace("#", "%23")
                .Replace("\"", "%22").Replace("'", "%27").Replace("\\", "%5C")
                .Replace("\r", "").Replace("\n", "");
        static string ViewerMapJson(string name, string farFile, string? nearFile, double lodSwitch, string outDir, uint? seedForJump = null)
            => $"{{\"name\":\"{name}\",\"far\":\"{SvgDataUri(Path.Combine(outDir, farFile))}\"{(nearFile != null ? $",\"near\":\"{SvgDataUri(Path.Combine(outDir, nearFile))}\"" : "")},\"lodSwitch\":{lodSwitch.ToString(CultureInfo.InvariantCulture)},\"file\":\"{farFile}\"{(seedForJump.HasValue ? $",\"seed\":{seedForJump.Value}" : "")}}}";
        static string ViewerWorldJson(string name, string farFile, double cxFeet, double czFeet, double spanFeet, string outDir, uint worldSeed)
            => $"{{\"name\":\"{name}\",\"far\":\"{SvgDataUri(Path.Combine(outDir, farFile))}\",\"lodSwitch\":2.0,\"file\":\"{farFile}\",\"seed\":{worldSeed},\"world\":{{\"seed\":{worldSeed},\"cx\":{cxFeet.ToString("0.##", CultureInfo.InvariantCulture)},\"cz\":{czFeet.ToString("0.##", CultureInfo.InvariantCulture)},\"span\":{spanFeet.ToString("0.##", CultureInfo.InvariantCulture)}}}}}";
        {
            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "viewer-template.html");
            if (File.Exists(templatePath))
            {
                var maps = new List<string>();
                if (world != null)
                {
                    double wcx = world.MinX + world.WidthFeet / 2.0;
                    double wcz = world.MinZ + world.DepthFeet / 2.0;
                    maps.Add(ViewerWorldJson($"Infinite world — seed {seed} — 9 mi window", $"world-{seed}-9mi.svg", wcx, wcz, world.WidthFeet, outDir, seed));
                }
                foreach (var s in smalltownSeeds)
                    if (File.Exists(Path.Combine(outDir, $"smalltown-{s}.svg")))
                        maps.Add(ViewerMapJson($"Small town — seed {s}", $"smalltown-{s}.svg", null, 1.55, outDir, s));
                foreach (var (sz, es) in new[] { ("hamlet", (string?)null), ("village", null), ("town", "west"), ("hamlet", "north") })
                {
                    string stem = $"country-{seed}-{sz}{(es != null ? "-" + es : "")}";
                    if (!File.Exists(Path.Combine(outDir, $"{stem}.svg")) || !File.Exists(Path.Combine(outDir, $"{stem}-detail.svg")))
                        continue;
                    string label = $"3 mi cell — {sz}{(es != null ? " (partial " + es + " edge)" : "")} — seed {seed}";
                    maps.Add(ViewerMapJson(label, $"{stem}.svg", $"{stem}-detail.svg", 2.6, outDir));
                }
                string viewer = File.ReadAllText(templatePath)
                    .Replace("/*__MAPS__*/[]", "[" + string.Join(",", maps) + "]");
                string viewerPath = Path.Combine(outDir, "viewer.html");
                File.WriteAllText(viewerPath, viewer);
                Console.WriteLine($"wrote {viewerPath} ({new FileInfo(viewerPath).Length / 1024 / 1024} MB, self-contained)");
            }
            else Console.WriteLine("viewer-template.html not found; skipping viewer.html");
        }

        return 0;
    }
}
