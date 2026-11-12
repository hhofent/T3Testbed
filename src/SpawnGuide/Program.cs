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

if (args.Contains("--serve"))
{
    // Tiny local HTTP server: serves the viewer, and /gen?seed=NNN generates an SVG on demand.
    // Lets the viewer use ANY seed without pre-baking all of them.
    int port = (int)GetUInt(args, "--port", 8735u);
    var listener = new System.Net.HttpListener();
    listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    listener.Start();
    Console.WriteLine($"SpawnGuide server on http://127.0.0.1:{port}/ (Ctrl+C to stop)");
    while (listener.IsListening)
    {
        var ctx = listener.GetContext();
        var url = ctx.Request.Url;
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
var mapMode = GetString(args, "--map", "all");   // rural | town | smalltown | country | all
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
var townSections = sections.Where(s => s.FileName.StartsWith("smalltown-") || s.FileName.StartsWith("country-")).ToList();
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
{
    var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "viewer-template.html");
    if (File.Exists(templatePath))
    {
        var maps = new List<string>();
        foreach (var s in smalltownSeeds)
            maps.Add(ViewerMapJson($"Small town — seed {s}", $"smalltown-{s}.svg", null, 1.55, outDir, s));
        foreach (var (sz, es) in new[] { ("hamlet", (string?)null), ("boyden", null), ("hull", "west"), ("hamlet", "north") })
        {
            string stem = $"country-{seed}-{sz}{(es != null ? "-" + es : "")}";
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
