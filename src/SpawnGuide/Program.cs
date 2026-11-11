using System;
using System.Collections.Generic;
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

var archetype = GetString(args, "--archetype", "all");
var mapMode = GetString(args, "--map", "all");   // rural | town | smalltown | country | all
var townSize = GetString(args, "--size", "hamlet"); // hamlet (boyden/hull later)
var seed = GetUInt(args, "--seed", 1993);
var seedsArg = GetString(args, "--seeds", "");   // comma list, e.g. --seeds 1993,42,2024 (smalltown only)
var extraSeeds = seedsArg.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
    .Select(s => uint.TryParse(s.Trim(), out var v) ? v : (uint?)null)
    .Where(v => v.HasValue).Select(v => v!.Value).ToList();
var smalltownSeeds = extraSeeds.Count > 0 ? extraSeeds : new List<uint> { seed };
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
        var m = SmallTownGenerator.Generate(s, townSize);
        sections.Add(MapSection(m, $"smalltown-{s}.svg"));
        if (smalltown == null) smalltown = m;   // featured: first seed
    }
}
if (mapMode is "country" or "all")
{
    // One countryside view per size class, so growth is visible test-to-test.
    foreach (var sz in new[] { "hamlet", "boyden", "hull" })
    {
        var m = CountrysideMap.Generate(seed, sz);
        sections.Add(MapSection(m, $"country-{seed}-{sz}.svg"));
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
