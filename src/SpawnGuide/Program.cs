using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SpawnGuide;

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
    Environment.ExitCode = failed == 0 ? 0 : 1;
    return;
}

if (args.Contains("--serve"))
{
    // Tiny local HTTP server: serves the viewer, /gen for small towns, and /world for
    // any lens window of the infinite map. Lets the viewer stream cells it has never
    // baked — the lens is a pure query, so the same coordinates always return the same map.
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
            // The viewer probes "/" with HEAD to detect a live spawner.
            ctx.Response.StatusCode = 200;
            ctx.Response.Close();
            continue;
        }
        if (url?.LocalPath == "/world-center")
        {
            // Metadata for the viewer's live seed jump: put the lens on a real town.
            try
            {
                var qs = (url.Query ?? "").TrimStart('?');
                var q = qs.Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .ToDictionary(p => p.Split('=')[0], p => p.Split('=').Length > 1 ? p.Split('=')[1] : "", StringComparer.OrdinalIgnoreCase);
                if (!uint.TryParse(q.GetValueOrDefault("seed", ""), out var s)) s = 1993;
                var near = WorldMap.NearestTown(s, 0f, 0f, 80f);
                float cx = near?.CenterX ?? 0f;
                float cz = (near?.HighwayZ ?? 0f) + WorldMap.Mile;
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
                span = Math.Clamp(span, WorldMap.Mile, WorldMap.Mile * 30f);
                bool farLod = q.GetValueOrDefault("lod", "1") == "0";
                string key = $"{s}|{cx:0}/{cz:0}/{span:0}|{(farLod ? "far" : "detail")}";
                if (!worldCache.TryGetValue(key, out var svg))
                {
                    var ww = WorldMap.Generate(s, cx, cz, span, span);
                    if (farLod) ww.Map.LodSimple = true;
                    svg = SvgMapRenderer.Render(ww.Map, bare: true, targetWidth: 1400f);
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
                var map = SmallTownGenerator.Generate(s, sz);
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
        // serve the viewer file (and any out/*.svg) — just pass them through
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
    return;
}

var archetype = GetString(args, "--archetype", "all");
var mapMode = GetString(args, "--map", "all");   // rural | town | smalltown | country | world | all
var townSize = GetString(args, "--size", "hamlet"); // hamlet (boyden/hull later)
var seed = GetUInt(args, "--seed", 1993);
var seedsArg = GetString(args, "--seeds", "");   // comma list, e.g. --seeds 1993,42,2024 (smalltown only)
var extraSeeds = seedsArg.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
    .Select(s => uint.TryParse(s.Trim(), out var v) ? v : (uint?)null)
    .Where(v => v.HasValue).Select(v => v!.Value).ToList();
var bake = args.Contains("--bake");   // bake the full random-seed pool for the viewer
var bakedPool = new List<uint> { 1993, 42, 2024, 7, 97, 123, 555, 777, 1001, 1337, 1999, 2718, 4096 };
var smalltownSeeds = extraSeeds.Count > 0 ? extraSeeds : bake ? bakedPool : new List<uint> { seed };
var outDir = GetString(args, "--out", Path.Combine(Directory.GetCurrentDirectory(), "out"));
Directory.CreateDirectory(outDir);
foreach (var stale in Directory.EnumerateFiles(outDir, "*.svg")) File.Delete(stale);

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

// --- Maps first: that's the current focus. ---
MapPlan? rural = null, town = null, smalltown = null;
if (mapMode is "rural" or "all")
{
    rural = MapGenerator.RuralLot(seed);
    sections.Add(MapSection(rural, $"rural-{seed}.svg"));
}
if (mapMode is "town" or "all")
{
    town = MapGenerator.TownBlock(seed);
    sections.Add(MapSection(town, $"town-{seed}.svg"));
}
if (mapMode is "smalltown" or "all")
{
    foreach (var s in smalltownSeeds)
    {
        var m = SmallTownGenerator.Generate(s, townSize);               // embed layer (quiet)
        sections.Add(MapSection(m, $"smalltown-{s}.svg"));
        if (smalltown == null) smalltown = m;   // featured: first seed
    }
}
if (mapMode is "country" or "all")
{
    // The 3-mile cell, four ways: two full towns and two partial towns (town clipped by
    // the cell boundary — partial town + rural land, as the real section grid looks).
    foreach (var (sz, es) in new[] { ("hamlet", (string?)null), ("boyden", null), ("hull", "west"), ("hamlet", "north") })
    {
        string stem = $"country-{seed}-{sz}{(es != null ? "-" + es : "")}";
        var m = CountrysideMap.Generate(seed, sz, detail: false, edge: es);
        sections.Add(MapSection(m, $"{stem}.svg"));
        var md = CountrysideMap.Generate(seed, sz, detail: true, edge: es);
        File.WriteAllText(Path.Combine(outDir, $"{stem}-detail.svg"), SvgMapRenderer.Render(md));
    }
}

// --- Infinite world: 3×3-cell lens window + one 3-mile cell. ---
WorldWindow? world = null;
if (mapMode is "world" or "all")
{
    var near = WorldMap.NearestTown(seed, 0f, 0f, 80f);
    float wcx = near?.CenterX ?? 0f;
    float wcz = (near?.HighwayZ ?? 0f) + WorldMap.Mile;
    float span = WorldMap.CellFeet * 3f;
    world = WorldMap.Generate(seed, wcx, wcz, span, span);
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

    var cell = WorldMap.Generate(seed, wcx, wcz, WorldMap.CellFeet, WorldMap.CellFeet);
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

// --- Houses. ---
var archetypes = archetype == "all" ? HouseGenerator.Archetypes : new[] { archetype };
foreach (var name in archetypes)
{
    // Same base seed per archetype → each archetype gets its own deterministic stream.
    uint archetypeSeed = archetype == "all" ? seed + (uint)Array.IndexOf(HouseGenerator.Archetypes, name) * 7919u : seed;
    var plan = HouseGenerator.Generate(name, archetypeSeed);
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

// Default phone-friendly output: the rural farmstead alone.
var featured = sections.FirstOrDefault(s => s.FileName.StartsWith("smalltown-"))
    ?? sections.FirstOrDefault(s => s.FileName.StartsWith("rural-"))
    ?? sections[0];
string examplePath = Path.Combine(outDir, "example.html");
File.WriteAllText(examplePath, HtmlIndex.Render(new List<HtmlSection> { featured }));
Console.WriteLine($"wrote {examplePath}");

// Multi-example gallery (links, not embeds) when more than one smalltown was generated.
var townSections = sections.Where(s => s.FileName.StartsWith("smalltown-") || s.FileName.StartsWith("country-") || s.FileName.StartsWith("world-")).ToList();
if (townSections.Count > 1)
{
    string galleryPath = Path.Combine(outDir, "examples.html");
    File.WriteAllText(galleryPath, HtmlIndex.Render(townSections));
    Console.WriteLine($"wrote {galleryPath}");
}
foreach (var s in townSections)
    Console.WriteLine($"gallery: {s.FileName}");

// Zoomable viewer: pan/zoom with far↔detail LOD crossfade. Maps inlined as data URIs so the
// single viewer.html works everywhere (file://, t3 embed, any static host) with no sibling files.
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
        foreach (var (sz, es) in new[] { ("hamlet", (string?)null), ("boyden", null), ("hull", "west"), ("hamlet", "north") })
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
