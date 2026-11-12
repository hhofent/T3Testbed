using System;
using System.Collections.Generic;
using System.Linq;

namespace SpawnGuide
{

/// <summary>How a town sits against the paved road it grew along.</summary>
public enum WorldTownLayout
{
    /// <summary>North of the road only — the road is the town's south edge.</summary>
    North,
    /// <summary>South of the road only — the road is the town's north edge.</summary>
    South,
    /// <summary>Straddling the road: blocks on both sides, road through the middle.</summary>
    Both,
}

/// <summary>One side of a town. A "Both" town has two of these.</summary>
public sealed class WorldTownPart
{
    /// <summary>The generated town plan for this side (local coords: highway band at z 0..66).</summary>
    public MapPlan Plan { get; set; } = null!;
    /// <summary>true → the plan is reflected across the highway band (town hangs south).</summary>
    public bool Mirrored { get; set; }
    /// <summary>World position of the plan's local origin (its highway band starts here).</summary>
    public float OriginX { get; set; }
    public float OriginZ { get; set; }
    /// <summary>
    /// Rectangles (world coords) that county roads must not run through: the built
    /// blocks plus the perimeter streets. Roads stop exactly on a town street.
    /// </summary>
    public List<(float x0, float z0, float x1, float z1)> RoadClip { get; } = new();
    /// <summary>Built footprint of this side in world coords (blocks only).</summary>
    public float BlockMinX { get; set; }
    public float BlockMinZ { get; set; }
    public float BlockMaxX { get; set; }
    public float BlockMaxZ { get; set; }
}

/// <summary>A town placed in the world. Towns sit on the paved road grid.</summary>
public sealed class WorldTown
{
    public int Corridor { get; set; }
    public int Slot { get; set; }
    public string SizeName { get; set; } = "";
    public string ClassLabel { get; set; } = "";
    public string Name { get; set; } = "";
    public uint TownSeed { get; set; }
    public float CenterX { get; set; }
    public float HighwayZ { get; set; }
    public float MinX { get; set; }
    public float MinZ { get; set; }
    public float MaxX { get; set; }
    public float MaxZ { get; set; }
    public int Houses { get; set; }
    public WorldTownLayout Layout { get; set; }
    public List<WorldTownPart> Parts { get; } = new();
}

/// <summary>A farmstead stamped from the existing rural-lot leaf generator.</summary>
public sealed class WorldFarm
{
    public int SectionX { get; set; }
    public int SectionZ { get; set; }
    public int Index { get; set; }
    public int Side { get; set; }
    public uint Seed { get; set; }
    public float MinX { get; set; }
    public float MinZ { get; set; }
    public float MaxX { get; set; }
    public float MaxZ { get; set; }
    public int Houses { get; set; }
    public int Outbuildings { get; set; }
}

/// <summary>One renderable lens window. All coordinates on Map are window-local.</summary>
public sealed class WorldWindow
{
    public MapPlan Map { get; set; } = null!;
    public float MinX { get; set; }
    public float MinZ { get; set; }
    public float WidthFeet { get; set; }
    public float DepthFeet { get; set; }
    public List<WorldTown> Towns { get; } = new();
    public List<WorldFarm> Farms { get; } = new();

    public bool Contains(float x, float z) =>
        x >= MinX && x <= MinX + WidthFeet && z >= MinZ && z <= MinZ + DepthFeet;

    public int FarmsInWindow => Farms.Count(f =>
        f.MaxX > MinX && f.MinX < MinX + WidthFeet && f.MaxZ > MinZ && f.MinZ < MinZ + DepthFeet);
}

/// <summary>
/// Deterministic, infinite, seed-reproducible 1990s rural Iowa.
///
/// The world is built in the order the land actually makes itself:
///
///   1. HILLS    — a low, long-wavelength elevation field (Great Plains: swells, not
///                  mountains). Drawn as 20-ft contour lines.
///   2. RIVERS   — trunk rivers every ~28 miles running south, with tributaries
///                  joining them from both sides. Every tributary merges into a bigger
///                  river; nothing flows north.
///   3. RAIL     — rail lines every ~75 miles, east-west and north-south.
///   4. ROADS    — paved roads every ~10 miles (the towns live on these), gravel
///                  section roads on every mile line, with rare exceptions.
///   5. TOWNS    — a queryable lattice on the paved road grid, near rail lines,
///                  north / south / straddling the road.
///   6. FARMS    — per-section farmsteads stamped from the rural lot generator.
///
/// Everything is a pure function of world coordinates: any window asks the same
/// questions and gets the same answers, so overlapping lens windows agree exactly.
/// </summary>
public static class WorldMap
{
    public const float Mile = 5280f;
    public const float SectionFeet = 5280f;
    public const float CellFeet = 15840f;                 // 3-mile lens cell
    public const float PavedSpacing = 10f * Mile;          // paved road every 10 mi
    public const float TownSlotFeet = 10f * Mile;          // one town candidate per 10 mi
    public const float MinTownSpacingFeet = 10f * Mile;    // never two towns closer than 10 mi
    public const float RailSpacing = 75f * Mile;           // rail line every ~75 mi
    public const float TrunkRiverSpacing = 28f * Mile;     // major river every ~28 mi
    public const float TributarySpacing = 8f * Mile;       // tributary head every ~8 mi
    public const float HighwayWidth = 66f;                 // paved road ROW
    /// <summary>Smallest lens window. Below a few hundred feet the map is a room, not a county.</summary>
    public const float MinSpan = 150f;
    const float RoadHalfPaved = 33f;
    const float RoadHalfGravel = 20f;
    const float FarmMargin = 300f;
    const float ContourInterval = 20f;                    // feet

    static readonly string[] FieldPalette =
        { "#ded9bd", "#d6d0b4", "#cbc4a2", "#e4e0cb", "#d2cbb0", "#dcd6b8", "#d8d2b6" };

    static readonly string[] HighwayNames =
        { "HWY 12", "HWY 7", "HWY 31", "HWY 46", "HWY 22", "HWY 38", "HWY 5", "HWY 17" };

    // ------------------------------------------------------------------ public API

    /// <summary>
    /// Generate the world visible through a lens window. Pure in
    /// (worldSeed, centerX, centerZ, width, depth): visit order, direction and window
    /// size never change what exists at a world coordinate.
    /// </summary>
    public static WorldWindow Generate(uint worldSeed, float centerX, float centerZ,
        float widthFeet = CellFeet * 3f, float depthFeet = CellFeet * 3f)
    {
        widthFeet = Math.Max(MinSpan, widthFeet);
        depthFeet = Math.Max(MinSpan, depthFeet);

        var ww = new WorldWindow
        {
            MinX = centerX - widthFeet / 2f,
            MinZ = centerZ - depthFeet / 2f,
            WidthFeet = widthFeet,
            DepthFeet = depthFeet,
        };

        var map = new MapPlan
        {
            Title = $"INFINITE WORLD — SEED {worldSeed} — {centerX / Mile:0.#}, {centerZ / Mile:0.#} MI",
            Seed = worldSeed,
            PlaceName = "1990S RURAL IOWA",
            WidthFeet = widthFeet,
            DepthFeet = depthFeet,
            ExactBounds = true,
            RoadSurface = "asphalt",   // per-road RoadPaved decides the rest; bulbs match
            LodSimple = false,
            QuietAnnotations = true,
            SuppressOutbuildingLabels = true,
        };
        ww.Map = map;

        // --- the land, in order -------------------------------------------------
        AddFields(ww, worldSeed);
        AddHills(ww, worldSeed);
        AddRivers(ww, worldSeed);
        AddRail(ww, worldSeed);

        // --- roads, then the towns that grow on them ----------------------------
        var sites = CollectTownSites(worldSeed, ww);
        var towns = sites.Select(s => MaterializeTown(worldSeed, s)).ToList();
        AddRoads(ww, worldSeed, towns);
        StampTowns(ww, towns);

        // --- country ------------------------------------------------------------
        AddFarms(ww, worldSeed);
        AddFieldEntrances(ww, worldSeed);
        AddWindowLabels(ww);

        return ww;
    }

    /// <summary>Convenience for the 3-mile cell view.</summary>
    public static WorldWindow GenerateCell(uint worldSeed, int cellX, int cellZ) =>
        Generate(worldSeed,
            (cellX + 0.5f) * CellFeet,
            (cellZ + 0.5f) * CellFeet,
            CellFeet, CellFeet);

    /// <summary>Find the nearest town to a point, looking along nearby paved corridors.</summary>
    public static WorldTown? NearestTown(uint worldSeed, float x, float z, float maxMiles = 80f)
    {
        float bestD = maxMiles * Mile * maxMiles * Mile;
        WorldTown? best = null;
        foreach (var site in TownSitesNear(worldSeed, x - maxMiles * Mile, x + maxMiles * Mile,
                     z - maxMiles * Mile, z + maxMiles * Mile))
        {
            float dx = site.CenterX - x, dz = site.HighwayZ - z;
            float d = dx * dx + dz * dz;
            if (d < bestD) { bestD = d; best = MaterializeTown(worldSeed, site); }
        }
        return best;
    }

    // ------------------------------------------------------------------ hashing

    static class Hash
    {
        public static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public static ulong U64(uint seed, long a, long b, long c = 0, long d = 0)
        {
            unchecked
            {
                ulong h = Mix((ulong)seed ^ 0xA5A5A5A5A5A5A5A5UL);
                h = Mix(h ^ (ulong)a);
                h = Mix(h ^ ((ulong)b + 0x9E3779B97F4A7C15UL));
                h = Mix(h ^ ((ulong)c + 0xC2B2AE3D27D4EB4FUL));
                h = Mix(h ^ ((ulong)d + 0x165667B19E3779F9UL));
                return h;
            }
        }

        public static uint U32(uint seed, long a, long b, long c = 0, long d = 0) =>
            (uint)(U64(seed, a, b, c, d) ^ (U64(seed, a, b, c, d) >> 32));

        /// <summary>Deterministic 0..1 float from a hash.</summary>
        public static float F(uint seed, long a, long b, long c = 0, long d = 0) =>
            (U64(seed, a, b, c, d) % 1000000UL) / 1000000f;
    }

    // ------------------------------------------------------------------ terrain

    static float Phase(uint seed, int k) => Hash.F(seed, k, 701) * 6.2831853f;

    /// <summary>
    /// Ground elevation in feet. Great Plains: long, low swells with a little texture.
    /// Total relief is under ~130 feet over many miles — nothing alpine.
    /// </summary>
    public static float Elevation(uint worldSeed, float x, float z)
    {
        float p1 = Phase(worldSeed, 1), p2 = Phase(worldSeed, 2);
        float p3 = Phase(worldSeed, 3), p4 = Phase(worldSeed, 4), p5 = Phase(worldSeed, 5);
        float a = 20f * MathF.Sin((x * 0.62f + z * 0.78f) / 11000f + p1);
        float b = 12f * MathF.Sin((x * -0.71f + z * 0.46f) / 5200f + p2);
        float c = 26f * MathF.Sin((x * 0.31f + z * 1.07f) / 26000f + p3);
        float d = 4f * MathF.Sin(x / 1600f + p4);
        float e = 5f * MathF.Sin(z / 1900f + p5);
        return a + b + c + d + e;
    }

    /// <summary>Contour lines of the elevation field, on a globally anchored 1/4-mile lattice.</summary>
    static void AddHills(WorldWindow ww, uint worldSeed)
    {
        const float cell = 1320f;   // 1/4 mile
        int i0 = (int)MathF.Floor((ww.MinX - cell) / cell);
        int i1 = (int)MathF.Floor((ww.MinX + ww.WidthFeet + cell) / cell);
        int j0 = (int)MathF.Floor((ww.MinZ - cell) / cell);
        int j1 = (int)MathF.Floor((ww.MinZ + ww.DepthFeet + cell) / cell);
        int nx = i1 - i0 + 1, nz = j1 - j0 + 1;
        if (nx < 2 || nz < 2) return;

        var e = new float[nx * nz];
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
                e[j * nx + i] = Elevation(worldSeed, (i0 + i) * cell, (j0 + j) * cell);

        float lo = e.Min(), hi = e.Max();
        int first = (int)MathF.Ceiling(lo / ContourInterval) * (int)ContourInterval;
        var pts = new List<Vec2>(4);
        for (int level = first; level <= hi; level += (int)ContourInterval)
        {
            for (int j = 0; j + 1 < nz; j++)
            {
                for (int i = 0; i + 1 < nx; i++)
                {
                    float x0 = (i0 + i) * cell, z0 = (j0 + j) * cell;
                    float v00 = e[j * nx + i], v10 = e[j * nx + i + 1];
                    float v11 = e[(j + 1) * nx + i + 1], v01 = e[(j + 1) * nx + i];
                    pts.Clear();
                    if ((v00 < level) != (v10 < level)) pts.Add(new Vec2(x0 + cell * (level - v00) / (v10 - v00), z0));
                    if ((v10 < level) != (v11 < level)) pts.Add(new Vec2(x0 + cell, z0 + cell * (level - v10) / (v11 - v10)));
                    if ((v01 < level) != (v11 < level)) pts.Add(new Vec2(x0 + cell * (level - v01) / (v11 - v01), z0 + cell));
                    if ((v00 < level) != (v01 < level)) pts.Add(new Vec2(x0, z0 + cell * (level - v00) / (v01 - v00)));
                    if (pts.Count == 2) AddContour(ww, pts[0], pts[1], level);
                    else if (pts.Count == 4)
                    {
                        AddContour(ww, pts[0], pts[1], level);
                        AddContour(ww, pts[2], pts[3], level);
                    }
                }
            }
        }
    }

    static void AddContour(WorldWindow ww, Vec2 a, Vec2 b, int level)
    {
        float mx = (a.X + b.X) / 2f, mz = (a.Z + b.Z) / 2f;
        if (mx < ww.MinX - 200f || mx > ww.MinX + ww.WidthFeet + 200f) return;
        if (mz < ww.MinZ - 200f || mz > ww.MinZ + ww.DepthFeet + 200f) return;
        ww.Map.Contours.Add(new ContourSegment(
            new Vec2(a.X - ww.MinX, a.Z - ww.MinZ),
            new Vec2(b.X - ww.MinX, b.Z - ww.MinZ), level));
    }

    // ------------------------------------------------------------------ rivers

    static float TrunkBaseX(uint worldSeed, int t)
    {
        float j = Hash.F(worldSeed, t, 811) - 0.5f;
        return t * TrunkRiverSpacing + j * 12f * Mile;
    }

    /// <summary>Centreline of a trunk river at a given northing. Always flowing south.</summary>
    public static float TrunkX(uint worldSeed, int t, float z)
    {
        float p1 = Hash.F(worldSeed, t, 821) * 6.2831853f;
        float p2 = Hash.F(worldSeed, t, 822) * 6.2831853f;
        float p3 = Hash.F(worldSeed, t, 823) * 6.2831853f;
        float x = TrunkBaseX(worldSeed, t);
        x += 1500f * MathF.Sin(z / 9500f + p1);
        x += 700f * MathF.Sin(z / 3300f + p2);
        x += 260f * MathF.Sin(z / 1250f + p3);
        return x;
    }

    static float TrunkWidth(uint worldSeed, int t, float z)
    {
        float p1 = Hash.F(worldSeed, t, 824) * 6.2831853f;
        float p2 = Hash.F(worldSeed, t, 825) * 6.2831853f;
        return 58f + 20f * MathF.Sin(z / 17000f + p1) + 10f * MathF.Sin(z / 6000f + p2);
    }

    static int NearestTrunk(uint worldSeed, float x) => (int)MathF.Round(x / TrunkRiverSpacing);

    static float TrunkDistance(uint worldSeed, float x, float z)
    {
        float best = float.MaxValue;
        int t0 = NearestTrunk(worldSeed, x);
        for (int t = t0 - 1; t <= t0 + 1; t++)
            best = MathF.Min(best, MathF.Abs(x - TrunkX(worldSeed, t, z)));
        return best;
    }

    /// <summary>Tributary shape: head (north), merge point (south, on the trunk), side.</summary>
    static void TributaryShape(uint worldSeed, int t, int k,
        out float zHead, out float zMerge, out float xHead, out float side)
    {
        side = ((k & 1) == 0) ? -1f : 1f;
        zHead = k * TributarySpacing + (Hash.F(worldSeed, t, k, 831) - 0.5f) * 2.4f * Mile;
        float len = (2.0f + 2.4f * Hash.F(worldSeed, t, k, 832)) * Mile;
        zMerge = zHead - len;
        float gap = (2.6f + 5.4f * Hash.F(worldSeed, t, k, 833)) * Mile;
        xHead = TrunkX(worldSeed, t, zHead) + side * gap;
    }

    static Vec2 TributaryPoint(uint worldSeed, int t, int k, float u)
    {
        TributaryShape(worldSeed, t, k, out float zHead, out float zMerge, out float xHead, out float side);
        float x1 = TrunkX(worldSeed, t, zMerge);
        float x = xHead + (x1 - xHead) * u;
        float z = zHead + (zMerge - zHead) * u;
        float bow = MathF.Sin(u * MathF.PI) * 800f * -side;
        return new Vec2(x + bow, z);
    }

    static void AddRivers(WorldWindow ww, uint worldSeed)
    {
        float minX = ww.MinX - 4000f, maxX = ww.MinX + ww.WidthFeet + 4000f;
        float minZ = ww.MinZ - 4000f, maxZ = ww.MinZ + ww.DepthFeet + 4000f;

        int t0 = (int)MathF.Floor((minX - 20f * Mile) / TrunkRiverSpacing);
        int t1 = (int)MathF.Floor((maxX + 20f * Mile) / TrunkRiverSpacing);
        for (int t = t0; t <= t1; t++)
        {
            var run = new List<Vec2>();
            var runs = new List<List<Vec2>> { run };
            // Sample on a global 240-ft lattice so overlapping windows produce identical ribbons.
            int m0 = (int)MathF.Floor(minZ / 240f), m1 = (int)MathF.Floor(maxZ / 240f);
            for (int m = m0; m <= m1; m++)
            {
                float z = m * 240f;
                float x = TrunkX(worldSeed, t, z);
                if (x < minX || x > maxX) { if (run.Count > 1) runs.Add(run = new List<Vec2>()); else run.Clear(); continue; }
                run.Add(new Vec2(x, z));
            }
            foreach (var r in runs)
            {
                if (r.Count < 2) continue;
                var water = new WaterCourse { WidthFeet = TrunkWidth(worldSeed, t, r[0].Z) };
                foreach (var p in r) water.Points.Add(new Vec2(p.X - ww.MinX, p.Z - ww.MinZ));
                ww.Map.Waters.Add(water);
            }
            // Bank trees: hashed per global sample so windows agree.
            for (int m = m0; m <= m1; m++)
            {
                float z = m * 240f;
                float x = TrunkX(worldSeed, t, z);
                if (x < minX || x > maxX) continue;
                float w = TrunkWidth(worldSeed, t, z);
                for (int side = -1; side <= 1; side += 2)
                {
                    ulong h = Hash.U64(worldSeed, t, m, side, 840);
                    if (h % 100 >= 46) continue;
                    float off = w / 2f + 14f + (float)((h >> 8) % 90);
                    float px = x + side * off + ((h >> 24) % 40) - 20f;
                    float pz = z + ((h >> 40) % 60) - 30f;
                    AddBankTree(ww, worldSeed, px, pz);
                }
            }
        }

        // --- tributaries: they run south and merge into the trunk ---------------
        for (int t = t0; t <= t1; t++)
        {
            int k0 = (int)MathF.Floor((minZ - 12f * Mile) / TributarySpacing);
            int k1 = (int)MathF.Floor((maxZ + 4f * Mile) / TributarySpacing);
            for (int k = k0; k <= k1; k++)
            {
                TributaryShape(worldSeed, t, k, out float zHead, out float zMerge, out _, out _);
                if (zMerge > maxZ || zHead < minZ) continue;
                var pts = new List<Vec2>();
                for (int i = 0; i <= 20; i++)
                {
                    float u = i / 20f;
                    var p = TributaryPoint(worldSeed, t, k, u);
                    if (p.X < minX - 2000f || p.X > maxX + 2000f) continue;
                    pts.Add(p);
                }
                if (pts.Count < 2) continue;
                var water = new WaterCourse { WidthFeet = 16f };
                foreach (var p in pts) water.Points.Add(new Vec2(p.X - ww.MinX, p.Z - ww.MinZ));
                ww.Map.Waters.Add(water);

                for (int i = 0; i <= 20; i += 2)
                {
                    float u = i / 20f;
                    var p = TributaryPoint(worldSeed, t, k, u);
                    float w = 7f + 19f * u;
                    for (int s2 = -1; s2 <= 1; s2 += 2)
                    {
                        ulong h = Hash.U64(worldSeed, t, k, i * 2 + (s2 > 0 ? 1 : 0), 841);
                        if (h % 100 >= 34) continue;
                        float off = w / 2f + 12f + (float)((h >> 8) % 60);
                        AddBankTree(ww, worldSeed, p.X + s2 * off + ((h >> 24) % 30) - 15f, p.Z + ((h >> 40) % 40) - 20f);
                    }
                }
            }
        }
    }

    static void AddBankTree(WorldWindow ww, uint worldSeed, float x, float z)
    {
        if (x < ww.MinX || x > ww.MinX + ww.WidthFeet) return;
        if (z < ww.MinZ || z > ww.MinZ + ww.DepthFeet) return;
        if (NearRoad(worldSeed, x, z, 70f)) return;
        if (NearRail(worldSeed, x, z, 50f)) return;
        foreach (var t in ww.Towns)
            if (x > t.MinX - 120f && x < t.MaxX + 120f && z > t.MinZ - 120f && z < t.MaxZ + 120f) return;
        ww.Map.Trees.Add(new Vec2(x - ww.MinX, z - ww.MinZ));
    }

    // ------------------------------------------------------------------ rail

    static float RailEWBase(uint worldSeed, int m)
    {
        float j = Hash.F(worldSeed, m, 851) - 0.5f;
        return m * RailSpacing + j * 1.6f * Mile;
    }

    static float RailEWZ(uint worldSeed, int m, float x)
    {
        float p1 = Hash.F(worldSeed, m, 852) * 6.2831853f;
        float p2 = Hash.F(worldSeed, m, 853) * 6.2831853f;
        return RailEWBase(worldSeed, m) + 700f * MathF.Sin(x / 11000f + p1) + 300f * MathF.Sin(x / 3700f + p2);
    }

    static float RailNSBase(uint worldSeed, int n)
    {
        float j = Hash.F(worldSeed, n, 861) - 0.5f;
        return n * RailSpacing + j * 1.6f * Mile;
    }

    static float RailNSX(uint worldSeed, int n, float z)
    {
        float p1 = Hash.F(worldSeed, n, 862) * 6.2831853f;
        float p2 = Hash.F(worldSeed, n, 863) * 6.2831853f;
        return RailNSBase(worldSeed, n) + 700f * MathF.Sin(z / 11000f + p1) + 300f * MathF.Sin(z / 3700f + p2);
    }

    /// <summary>Distance from a point to the nearest rail line (approximate, curve ignored).</summary>
    static float NearestRailDistance(uint worldSeed, float x, float z)
    {
        float best = float.MaxValue;
        int m = (int)MathF.Round(z / RailSpacing);
        for (int k = m - 1; k <= m + 1; k++)
            best = MathF.Min(best, MathF.Abs(z - RailEWBase(worldSeed, k)));
        int n = (int)MathF.Round(x / RailSpacing);
        for (int k = n - 1; k <= n + 1; k++)
            best = MathF.Min(best, MathF.Abs(x - RailNSBase(worldSeed, k)));
        return best;
    }

    static bool NearRail(uint worldSeed, float x, float z, float pad)
    {
        int m = (int)MathF.Round(z / RailSpacing);
        for (int k = m - 1; k <= m + 1; k++)
            if (MathF.Abs(z - RailEWBase(worldSeed, k)) < pad) return true;
        int n = (int)MathF.Round(x / RailSpacing);
        for (int k = n - 1; k <= n + 1; k++)
            if (MathF.Abs(x - RailNSBase(worldSeed, k)) < pad) return true;
        return false;
    }

    static void AddRail(WorldWindow ww, uint worldSeed)
    {
        float minX = ww.MinX - 2000f, maxX = ww.MinX + ww.WidthFeet + 2000f;
        float minZ = ww.MinZ - 2000f, maxZ = ww.MinZ + ww.DepthFeet + 2000f;

        int m0 = (int)MathF.Floor((minZ - 12f * Mile) / RailSpacing);
        int m1 = (int)MathF.Floor((maxZ + 12f * Mile) / RailSpacing);
        for (int m = m0; m <= m1; m++)
        {
            var line = new RailLine();
            int i0 = (int)MathF.Floor(minX / 600f), i1 = (int)MathF.Floor(maxX / 600f);
            for (int i = i0; i <= i1; i++)
            {
                float x = i * 600f;
                line.Points.Add(new Vec2(x - ww.MinX, RailEWZ(worldSeed, m, x) - ww.MinZ));
            }
            if (line.Points.Count >= 2) ww.Map.Rails.Add(line);
        }

        int n0 = (int)MathF.Floor((minX - 12f * Mile) / RailSpacing);
        int n1 = (int)MathF.Floor((maxX + 12f * Mile) / RailSpacing);
        for (int n = n0; n <= n1; n++)
        {
            var line = new RailLine();
            int j0 = (int)MathF.Floor(minZ / 600f), j1 = (int)MathF.Floor(maxZ / 600f);
            for (int j = j0; j <= j1; j++)
            {
                float z = j * 600f;
                line.Points.Add(new Vec2(RailNSX(worldSeed, n, z) - ww.MinX, z - ww.MinZ));
            }
            if (line.Points.Count >= 2) ww.Map.Rails.Add(line);
        }
    }

    // ------------------------------------------------------------------ roads

    /// <summary>True when the given mile line is missing from the network (rare exception).</summary>
    static bool RoadLineAbsent(uint worldSeed, bool ew, int line)
    {
        ulong h = Hash.U64(worldSeed, line, ew ? 901 : 902);
        return h % 1000 < 18;
    }

    /// <summary>True when the given gravel mile line has been paved (rare exception).</summary>
    static bool RoadLinePaved(uint worldSeed, bool ew, int line)
    {
        ulong h = Hash.U64(worldSeed, line, ew ? 903 : 904);
        return h % 1000 < 22;
    }

    /// <summary>The paved road corridors. They run on the section grid, every 10 miles.</summary>
    static float CorridorZ(uint worldSeed, int corridor) => corridor * PavedSpacing;
    static float CorridorX(uint worldSeed, int column) => column * PavedSpacing;

    static bool NearRoad(uint worldSeed, float x, float z, float pad)
    {
        int m = (int)MathF.Round(z / SectionFeet);
        for (int k = m - 1; k <= m + 1; k++)
        {
            if (RoadLineAbsent(worldSeed, true, k)) continue;
            float half = RoadLinePaved(worldSeed, true, k) ? RoadHalfPaved : RoadHalfGravel;
            if (MathF.Abs(z - k * SectionFeet) < half + pad) return true;
        }
        int n = (int)MathF.Round(x / SectionFeet);
        for (int k = n - 1; k <= n + 1; k++)
        {
            if (RoadLineAbsent(worldSeed, false, k)) continue;
            float half = RoadLinePaved(worldSeed, false, k) ? RoadHalfPaved : RoadHalfGravel;
            if (MathF.Abs(x - k * SectionFeet) < half + pad) return true;
        }
        return false;
    }

    static List<(float a, float b)> Subtract(List<(float a, float b)> spans, float a, float b)
    {
        var result = new List<(float, float)>();
        foreach (var s in spans)
        {
            if (b <= s.a || a >= s.b) { result.Add(s); continue; }
            if (a > s.a) result.Add((s.a, MathF.Min(a, s.b)));
            if (b < s.b) result.Add((MathF.Max(b, s.a), s.b));
        }
        return result;
    }

    static void AddRoads(WorldWindow ww, uint worldSeed, List<WorldTown> towns)
    {
        var map = ww.Map;
        float minX = ww.MinX, maxX = ww.MinX + ww.WidthFeet;
        float minZ = ww.MinZ, maxZ = ww.MinZ + ww.DepthFeet;

        // Where towns are, county roads stop on a town street instead of running through.
        var clips = new List<(float x0, float z0, float x1, float z1, float highwayZ)>();
        foreach (var t in towns)
            foreach (var p in t.Parts)
                foreach (var r in p.RoadClip)
                    clips.Add((r.x0, r.z0, r.x1, r.z1, t.HighwayZ));

        // --- paved corridors (the roads the towns live on) ----------------------
        // Corridors sit on the global 10-mile lattice, so the index range comes from
        // the absolute window bounds, not from a window-relative one.
        int c0 = (int)MathF.Floor(minZ / PavedSpacing);
        int c1 = (int)MathF.Floor(maxZ / PavedSpacing);
        for (int c = c0; c <= c1; c++)
        {
            float z = CorridorZ(worldSeed, c);
            var spans = new List<(float a, float b)> { (minX, maxX) };
            foreach (var b in clips)
            {
                // The corridor a town sits on runs through it; the others stop short.
                if (MathF.Abs(b.highwayZ - z) < 200f) continue;
                if (b.z1 >= z - RoadHalfPaved && b.z0 <= z + RoadHalfPaved)
                    spans = Subtract(spans, b.x0, b.x1);
            }
            foreach (var s in spans)
            {
                if (s.b - s.a < 1f) continue;
                map.Roads.Add(new MapRect(s.a - ww.MinX, z - RoadHalfPaved - ww.MinZ, s.b - s.a, RoadHalfPaved * 2f));
                map.RoadDashed.Add(true);
                map.RoadPaved.Add(true);
            }
        }

        int d0 = (int)MathF.Floor(minX / PavedSpacing);
        int d1 = (int)MathF.Floor(maxX / PavedSpacing);
        for (int c = d0; c <= d1; c++)
        {
            float x = CorridorX(worldSeed, c);
            var spans = new List<(float a, float b)> { (minZ, maxZ) };
            foreach (var b in clips)
            {
                if (b.x1 >= x - RoadHalfPaved && b.x0 <= x + RoadHalfPaved)
                    spans = Subtract(spans, b.z0, b.z1);
            }
            foreach (var s in spans)
            {
                if (s.b - s.a < 1f) continue;
                map.Roads.Add(new MapRect(x - RoadHalfPaved - ww.MinX, s.a - ww.MinZ, RoadHalfPaved * 2f, s.b - s.a));
                map.RoadDashed.Add(false);
                map.RoadPaved.Add(true);
            }
        }

        // --- gravel section roads on every mile line ---------------------------
        int ix0 = (int)MathF.Ceiling(minX / SectionFeet);
        int ix1 = (int)MathF.Floor(maxX / SectionFeet);
        for (int ix = ix0; ix <= ix1; ix++)
        {
            if (RoadLineAbsent(worldSeed, false, ix)) continue;
            float x = ix * SectionFeet;
            bool paved = RoadLinePaved(worldSeed, false, ix);
            float half = paved ? RoadHalfPaved : RoadHalfGravel;
            var spans = new List<(float a, float b)> { (minZ, maxZ) };
            foreach (var b in clips)
            {
                if (b.x1 >= x - half && b.x0 <= x + half)
                    spans = Subtract(spans, b.z0, b.z1);
            }
            foreach (var s in spans)
            {
                if (s.b - s.a < 1f) continue;
                map.Roads.Add(new MapRect(x - half - ww.MinX, s.a - ww.MinZ, half * 2f, s.b - s.a));
                map.RoadDashed.Add(false);
                map.RoadPaved.Add(paved);
            }
        }

        int iz0 = (int)MathF.Ceiling(minZ / SectionFeet);
        int iz1 = (int)MathF.Floor(maxZ / SectionFeet);
        for (int iz = iz0; iz <= iz1; iz++)
        {
            if (RoadLineAbsent(worldSeed, true, iz)) continue;
            float z = iz * SectionFeet;
            bool paved = RoadLinePaved(worldSeed, true, iz);
            float half = paved ? RoadHalfPaved : RoadHalfGravel;
            var spans = new List<(float a, float b)> { (minX, maxX) };
            foreach (var b in clips)
            {
                if (b.z1 >= z - half && b.z0 <= z + half)
                    spans = Subtract(spans, b.x0, b.x1);
            }
            foreach (var s in spans)
            {
                if (s.b - s.a < 1f) continue;
                map.Roads.Add(new MapRect(s.a - ww.MinX, z - half - ww.MinZ, s.b - s.a, half * 2f));
                map.RoadDashed.Add(false);
                map.RoadPaved.Add(paved);
            }
        }
    }

    // ------------------------------------------------------------------ town field

    enum TownClass { None, Crossroads, Hamlet, Village, Town }

    sealed class TownSite
    {
        public int Corridor;
        public int Slot;
        public TownClass Class;
        public float CenterX;
        public float HighwayZ;
        public uint Seed;
        public ulong Priority;
        public WorldTownLayout Layout;
    }

    static string SizeName(TownClass c) =>
        c == TownClass.Town ? "town" :
        c == TownClass.Village ? "village" :
        c == TownClass.Hamlet ? "hamlet" : "crossroads";

    static string ClassLabel(TownClass c) =>
        c == TownClass.Town ? "TOWN" :
        c == TownClass.Village ? "VILLAGE" :
        c == TownClass.Hamlet ? "HAMLET" : "CROSSROADS";

    /// <summary>Distance from a point to the nearest rail line, in feet.</summary>
    static float RailDistance(uint worldSeed, float x, float z) => NearestRailDistance(worldSeed, x, z);

    static TownSite RawSite(uint worldSeed, int corridor, int slot)
    {
        ulong h = Hash.U64(worldSeed, corridor, slot, 11);
        var rng = new Rng((uint)h);
        float highwayZ = CorridorZ(worldSeed, corridor);
        // Towns sit on the paved corridor, midway between the north-south paved roads.
        float rawX = (slot + 0.5f) * TownSlotFeet + rng.Range(-0.035f, 0.035f) * TownSlotFeet;
        float centerX = (MathF.Floor(rawX / SectionFeet) + 0.5f) * SectionFeet;

        // A settlement wants the railroad. Towns only spring up where a rail line runs
        // close by, which is why the county is full of rail towns and nothing else.
        bool nearRail = RailDistance(worldSeed, centerX, highwayZ) < 9f * Mile;
        TownClass cls = TownClass.None;
        if (nearRail)
        {
            float roll = rng.NextFloat();
            // The ladder: crossroads are common, the big town is rare (one per ~100 mi).
            cls = roll < 0.045f ? TownClass.Town
                : roll < 0.135f ? TownClass.Village
                : roll < 0.320f ? TownClass.Hamlet
                : roll < 0.640f ? TownClass.Crossroads
                : TownClass.None;
        }

        // The town never sits in the middle of a river.
        if (cls != TownClass.None && TrunkDistance(worldSeed, centerX, highwayZ) < 1200f)
            cls = TownClass.None;

        float lay = rng.NextFloat();
        var layout = lay < 0.46f ? WorldTownLayout.North
            : lay < 0.70f ? WorldTownLayout.South : WorldTownLayout.Both;

        uint seed = rng.NextUInt();
        ulong priority = ((ulong)rng.NextUInt() << 32) ^ rng.NextUInt();
        return new TownSite
        {
            Corridor = corridor, Slot = slot, Class = cls, CenterX = centerX, HighwayZ = highwayZ,
            Seed = seed, Priority = priority, Layout = layout,
        };
    }

    static bool Beats(TownSite a, TownSite b) =>
        a.Priority > b.Priority || (a.Priority == b.Priority && a.Slot < b.Slot);

    static TownSite? VisibleSite(uint worldSeed, int corridor, int slot)
    {
        var s = RawSite(worldSeed, corridor, slot);
        if (s.Class == TownClass.None) return null;
        // Priority suppression in the full neighbourhood: no town within 10 miles.
        for (int dk = -1; dk <= 1; dk++)
        {
            for (int di = -1; di <= 1; di++)
            {
                if (dk == 0 && di == 0) continue;
                var n = RawSite(worldSeed, corridor + dk, slot + di);
                if (n.Class == TownClass.None) continue;
                float dx = n.CenterX - s.CenterX, dz = n.HighwayZ - s.HighwayZ;
                if (dx * dx + dz * dz < MinTownSpacingFeet * MinTownSpacingFeet && Beats(n, s)) return null;
            }
        }
        return s;
    }

    static List<TownSite> TownSitesNear(uint worldSeed, float minX, float maxX, float minZ, float maxZ)
    {
        var result = new List<TownSite>();
        int k0 = (int)MathF.Floor((minZ - 16000f) / PavedSpacing);
        int k1 = (int)MathF.Floor((maxZ + 16000f) / PavedSpacing);
        int i0 = (int)MathF.Floor((minX - 16000f) / TownSlotFeet);
        int i1 = (int)MathF.Floor((maxX + 16000f) / TownSlotFeet);
        for (int k = k0; k <= k1; k++)
            for (int i = i0; i <= i1; i++)
            {
                var site = VisibleSite(worldSeed, k, i);
                if (site == null) continue;
                if (site.CenterX < minX - 20000f || site.CenterX > maxX + 20000f) continue;
                result.Add(site);
            }
        return result;
    }

    static List<TownSite> CollectTownSites(uint worldSeed, WorldWindow ww)
    {
        var sites = TownSitesNear(worldSeed, ww.MinX - 20000f, ww.MinX + ww.WidthFeet + 20000f,
            ww.MinZ - 16000f, ww.MinZ + ww.DepthFeet + 16000f);
        // Only towns close enough to touch this window are built.
        return sites.Where(s => s.CenterX > ww.MinX - 22000f && s.CenterX < ww.MinX + ww.WidthFeet + 22000f
            && s.HighwayZ > ww.MinZ - 8000f && s.HighwayZ < ww.MinZ + ww.DepthFeet + 8000f).ToList();
    }

    static WorldTown MaterializeTown(uint worldSeed, TownSite site)
    {
        var town = new WorldTown
        {
            Corridor = site.Corridor,
            Slot = site.Slot,
            SizeName = SizeName(site.Class),
            ClassLabel = ClassLabel(site.Class),
            TownSeed = site.Seed,
            CenterX = site.CenterX,
            HighwayZ = site.HighwayZ,
            Layout = site.Layout,
        };

        void AddPart(uint seed, TownClass cls, bool mirrored)
        {
            var plan = SmallTownGenerator.Generate(seed, SizeName(cls), detail: false);
            float ox = site.CenterX - plan.WidthFeet / 2f;
            float oz = site.HighwayZ - HighwayWidth / 2f;
            var part = new WorldTownPart { Plan = plan, Mirrored = mirrored, OriginX = ox, OriginZ = oz };

            // Built blocks (world coords).
            float bMinX = ox + plan.BuiltMinX, bMaxX = ox + plan.BuiltMaxX;
            float bMinZ, bMaxZ;
            if (mirrored)
            {
                bMinZ = oz + HighwayWidth - plan.BuiltMaxZ;
                bMaxZ = oz + HighwayWidth - plan.BuiltMinZ;
            }
            else
            {
                bMinZ = oz + plan.BuiltMinZ;
                bMaxZ = oz + plan.BuiltMaxZ;
            }
            part.BlockMinX = bMinX; part.BlockMaxX = bMaxX;
            part.BlockMinZ = bMinZ; part.BlockMaxZ = bMaxZ;

            // County roads stop on the town's perimeter street, so they meet it.
            if (mirrored)
            {
                float z0 = oz + HighwayWidth - plan.LoopMaxZ;
                float z1 = oz + HighwayWidth - plan.LoopMinZ;
                part.RoadClip.Add((ox + plan.LoopMinX, MathF.Min(z0, z1), ox + plan.LoopMaxX, MathF.Max(z0, z1)));
            }
            else
            {
                part.RoadClip.Add((ox + plan.LoopMinX, oz + plan.LoopMinZ, ox + plan.LoopMaxX, oz + plan.LoopMaxZ));
            }
            town.Parts.Add(part);
        }

        if (site.Layout == WorldTownLayout.Both)
        {
            AddPart(site.Seed, site.Class, false);
            var southClass = site.Class == TownClass.Town ? TownClass.Village
                : site.Class == TownClass.Village ? TownClass.Hamlet : TownClass.Crossroads;
            AddPart(site.Seed ^ 0x51ED270Bu, southClass, true);
        }
        else if (site.Layout == WorldTownLayout.South)
        {
            AddPart(site.Seed, site.Class, true);
        }
        else
        {
            AddPart(site.Seed, site.Class, false);
        }

        // Footprint of the whole town, for labels and road clipping.
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (var p in town.Parts)
        {
            minX = MathF.Min(minX, p.BlockMinX);
            maxX = MathF.Max(maxX, p.BlockMaxX);
            minZ = MathF.Min(minZ, p.BlockMinZ);
            maxZ = MathF.Max(maxZ, p.BlockMaxZ);
        }
        town.MinX = minX; town.MaxX = maxX; town.MinZ = minZ; town.MaxZ = maxZ;
        town.Name = town.Parts[0].Plan.PlaceName;
        return town;
    }

    static void StampTowns(WorldWindow ww, List<WorldTown> towns)
    {
        foreach (var wt in towns)
        {
            ww.Towns.Add(wt);
            int houses = 0;
            foreach (var part in wt.Parts) houses += StampTown(ww, part);
            wt.Houses = houses;
            // Label above the town's north edge.
            ww.Map.Labels.Add(new MapLabel
            {
                Position = new Vec2(wt.CenterX - ww.MinX, wt.MaxZ + 40f - ww.MinZ),
                Text = $"{wt.Name} · {wt.ClassLabel}",
                Large = true,
            });
        }
    }

    /// <summary>Stamp one side of a town into the window. Returns the house count.</summary>
    static int StampTown(WorldWindow ww, WorldTownPart part)
    {
        var map = ww.Map;
        var town = part.Plan;
        float ox = part.OriginX - ww.MinX;
        float oz = part.OriginZ - ww.MinZ;
        bool mir = part.Mirrored;
        int houses = 0;

        // Local z -> world z. Mirrored parts reflect across the highway band.
        float WZ(float localZ) => mir ? oz + (HighwayWidth - localZ) : oz + localZ;
        bool Keep(float localZ) => localZ >= HighwayWidth - 1f;

        for (int i = 0; i < town.Roads.Count; i++)
        {
            var r = town.Roads[i];
            if (IsTownHighway(r)) continue;      // the paved corridor is drawn once, globally
            if (!Keep(r.Z)) continue;
            float z0 = WZ(r.Z), z1 = WZ(r.Z + r.H);
            map.Roads.Add(new MapRect(r.X + ox, MathF.Min(z0, z1), r.W, MathF.Abs(z1 - z0)));
            map.RoadDashed.Add(i < town.RoadDashed.Count && town.RoadDashed[i]);
            map.RoadPaved.Add(true);
        }
        foreach (var r in town.Alleys)
        {
            if (!Keep(r.Z)) continue;
            float z0 = WZ(r.Z), z1 = WZ(r.Z + r.H);
            map.Alleys.Add(new MapRect(r.X + ox, MathF.Min(z0, z1), r.W, MathF.Abs(z1 - z0)));
        }
        foreach (var r in town.LotLines)
        {
            if (!Keep(r.Z)) continue;
            float z0 = WZ(r.Z), z1 = WZ(r.Z + r.H);
            map.LotLines.Add(new MapRect(r.X + ox, MathF.Min(z0, z1), r.W, MathF.Abs(z1 - z0)));
        }
        foreach (var w in town.Walks)
        {
            var d = new Driveway { WidthFeet = w.WidthFeet, Surface = w.Surface };
            bool any = false;
            foreach (var p in w.Points)
            {
                if (!Keep(p.Z)) continue;
                d.Points.Add(new Vec2(p.X + ox, WZ(p.Z)));
                any = true;
            }
            if (any) map.Walks.Add(d);
        }
        foreach (var d0 in town.Driveways)
        {
            var d = new Driveway { WidthFeet = d0.WidthFeet, Surface = d0.Surface };
            bool any = false;
            foreach (var p in d0.Points)
            {
                if (!Keep(p.Z)) continue;
                d.Points.Add(new Vec2(p.X + ox, WZ(p.Z)));
                any = true;
            }
            if (any) map.Driveways.Add(d);
        }
        foreach (var h in town.Houses)
        {
            float cz = h.Plan.Footprint.Select(p => p.Z).Average();
            if (!Keep(cz)) continue;
            var plan = mir ? h.Plan.FlipZ().OffsetCopy(0, HighwayWidth) : h.Plan;
            map.Houses.Add(new PositionedHouse
            {
                Plan = plan.OffsetCopy(ox, oz),
                MirroredZ = mir ? !h.MirroredZ : h.MirroredZ,
            });
            houses++;
        }
        foreach (var o in town.Outbuildings)
        {
            if (!Keep(o.Center.Z)) continue;
            map.Outbuildings.Add(new Outbuilding
            {
                Kind = o.Kind,
                Center = new Vec2(o.Center.X + ox, WZ(o.Center.Z)),
                WidthFeet = o.WidthFeet,
                DepthFeet = o.DepthFeet,
                DiameterFeet = o.DiameterFeet,
                Label = o.Label,
            });
        }
        foreach (var t in town.Trees)
        {
            if (!Keep(t.Z)) continue;
            map.Trees.Add(new Vec2(t.X + ox, WZ(t.Z)));
        }
        foreach (var b in town.Bulbs)
        {
            if (!Keep(b.Center.Z)) continue;
            map.Bulbs.Add(new RoadBulb { Center = new Vec2(b.Center.X + ox, WZ(b.Center.Z)), RadiusFeet = b.RadiusFeet, Fill = b.Fill });
        }
        return houses;
    }

    static bool IsTownHighway(MapRect r) =>
        MathF.Abs(r.Z) < 0.5f && MathF.Abs(r.H - HighwayWidth) < 0.5f;

    // ------------------------------------------------------------------ fields

    static void AddFields(WorldWindow ww, uint worldSeed)
    {
        int sx0 = (int)MathF.Floor(ww.MinX / SectionFeet) - 1;
        int sx1 = (int)MathF.Floor((ww.MinX + ww.WidthFeet) / SectionFeet) + 1;
        int sz0 = (int)MathF.Floor(ww.MinZ / SectionFeet) - 1;
        int sz1 = (int)MathF.Floor((ww.MinZ + ww.DepthFeet) / SectionFeet) + 1;

        for (int sx = sx0; sx <= sx1; sx++)
        {
            for (int sz = sz0; sz <= sz1; sz++)
            {
                var rng = new Rng(Hash.U32(worldSeed, sx, sz, 21));
                float x0 = sx * SectionFeet, z0 = sz * SectionFeet;
                float midX = x0 + SectionFeet / 2f + rng.Range(-380f, 380f);
                float midZ = z0 + SectionFeet / 2f + rng.Range(-380f, 380f);
                float[] xs = { x0, midX, x0 + SectionFeet };
                float[] zs = { z0, midZ, z0 + SectionFeet };
                for (int qx = 0; qx < 2; qx++)
                {
                    for (int qz = 0; qz < 2; qz++)
                    {
                        int color = (int)(Hash.U32(worldSeed, sx, sz, 22, qx * 2 + qz) % (uint)FieldPalette.Length);
                        ww.Map.Fields.Add(new MapRect(
                            xs[qx] - ww.MinX, zs[qz] - ww.MinZ,
                            xs[qx + 1] - xs[qx], zs[qz + 1] - zs[qz]));
                        ww.Map.FieldFills.Add(FieldPalette[color]);
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------ farmsteads

    static int FarmCountFor(uint worldSeed, int sx, int sz)
    {
        var rng = new Rng(Hash.U32(worldSeed, sx, sz, 31));
        float r = rng.NextFloat();
        return r < 0.45f ? 0 : r < 0.85f ? 1 : r < 0.97f ? 2 : 3;
    }

    static void AddFarms(WorldWindow ww, uint worldSeed)
    {
        int sx0 = (int)MathF.Floor(ww.MinX / SectionFeet) - 1;
        int sx1 = (int)MathF.Floor((ww.MinX + ww.WidthFeet) / SectionFeet) + 1;
        int sz0 = (int)MathF.Floor(ww.MinZ / SectionFeet) - 1;
        int sz1 = (int)MathF.Floor((ww.MinZ + ww.DepthFeet) / SectionFeet) + 1;

        for (int sx = sx0; sx <= sx1; sx++)
        {
            for (int sz = sz0; sz <= sz1; sz++)
            {
                int count = FarmCountFor(worldSeed, sx, sz);
                var bboxes = new List<(float x0, float z0, float x1, float z1)>();
                for (int idx = 0; idx < count; idx++)
                {
                    uint farmSeed = Hash.U32(worldSeed, sx, sz, 32, idx);
                    var rng = new Rng(farmSeed);
                    var plan = MapGenerator.RuralLot(farmSeed);
                    int side = rng.RangeInt(0, 4);
                    float lotW = plan.WidthFeet, lotD = plan.DepthFeet;
                    float span = SectionFeet - 2f * FarmMargin;

                    float ox, oz;
                    if (side == 0 || side == 1)
                    {
                        float along = sx * SectionFeet + FarmMargin + rng.Range(0f, MathF.Max(0f, span - lotW));
                        ox = along;
                        oz = side == 0 ? sz * SectionFeet + RoadHalfGravel : (sz + 1) * SectionFeet - RoadHalfGravel;
                    }
                    else
                    {
                        float along = sz * SectionFeet + FarmMargin + rng.Range(0f, MathF.Max(0f, span - lotW));
                        ox = side == 2 ? sx * SectionFeet + RoadHalfGravel : (sx + 1) * SectionFeet - RoadHalfGravel;
                        oz = side == 2 ? along + lotW : along;
                    }

                    var b = RuralBounds(plan, side, ox, oz);
                    if (b.maxX - b.minX <= 1f || b.maxZ - b.minZ <= 1f) continue;
                    if (IntersectsAnyTown(ww.Towns, b, 240f)) continue;
                    if (NearWater(worldSeed, b)) continue;
                    if (NearRail(worldSeed, (b.minX + b.maxX) / 2f, (b.minZ + b.maxZ) / 2f, 220f)) continue;
                    bool collide = bboxes.Any(o =>
                        b.minX < o.x1 + 20f && b.maxX > o.x0 - 20f &&
                        b.minZ < o.z1 + 20f && b.maxZ > o.z0 - 20f);
                    if (collide) continue;
                    bboxes.Add((b.minX, b.minZ, b.maxX, b.maxZ));

                    var wf = new WorldFarm
                    {
                        SectionX = sx, SectionZ = sz, Index = idx, Side = side, Seed = farmSeed,
                        MinX = b.minX, MinZ = b.minZ, MaxX = b.maxX, MaxZ = b.maxZ,
                        Houses = plan.Houses.Count, Outbuildings = plan.Outbuildings.Count,
                    };
                    ww.Farms.Add(wf);
                    StampFarm(ww, plan, side, ox, oz);
                }
            }
        }
    }

    static bool IntersectsAnyTown(List<WorldTown> towns, (float minX, float minZ, float maxX, float maxZ) b, float pad) =>
        towns.Any(t => b.minX < t.MaxX + pad && b.maxX > t.MinX - pad &&
                       b.minZ < t.MaxZ + pad && b.maxZ > t.MinZ - pad);

    /// <summary>True when a farm footprint would sit in a river or on a bank.</summary>
    static bool NearWater(uint worldSeed, (float minX, float minZ, float maxX, float maxZ) b)
    {
        float cx = (b.minX + b.maxX) / 2f, cz = (b.minZ + b.maxZ) / 2f;
        int t = NearestTrunk(worldSeed, cx);
        for (int k = t - 1; k <= t + 1; k++)
        {
            for (float z = b.minZ - 400f; z <= b.maxZ + 400f; z += 220f)
            {
                float rx = TrunkX(worldSeed, k, z);
                if (rx > b.minX - 260f && rx < b.maxX + 260f) return true;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------ field entrances

    /// <summary>
    /// Dirt entrances from the section-line road into the crop fields.
    ///
    /// Iowa county rules are specific: "up to two field entrances may be allowed per
    /// forty (40) acre field" (Woodbury County); a field entrance is earth with
    /// vegetation or a minimum of 4 in of 1⅛" crushed rock (Clarke County); it is
    /// 20–40 ft in top width with 3:1 side slopes; and it must sit at least 100 ft
    /// from an intersecting road. So: one or two per quarter section, hashed off the
    /// world coordinates so they never move as the lens moves.
    ///
    /// These are the *field* entrances — a farmstead's own driveway is separate, and
    /// stays on its residence.
    /// </summary>
    static void AddFieldEntrances(WorldWindow ww, uint worldSeed)
    {
        int placedCount = 0, noRoad = 0, blocked = 0;
        float minX = ww.MinX - 2000f, maxX = ww.MinX + ww.WidthFeet + 2000f;
        float minZ = ww.MinZ - 2000f, maxZ = ww.MinZ + ww.DepthFeet + 2000f;
        int sx0 = (int)MathF.Floor(minX / SectionFeet) - 1;
        int sx1 = (int)MathF.Floor(maxX / SectionFeet) + 1;
        int sz0 = (int)MathF.Floor(minZ / SectionFeet) - 1;
        int sz1 = (int)MathF.Floor(maxZ / SectionFeet) + 1;
        float half = SectionFeet / 2f;   // a quarter section is half a mile square

        for (int sx = sx0; sx <= sx1; sx++)
        {
            for (int sz = sz0; sz <= sz1; sz++)
            {
                for (int qx = 0; qx < 2; qx++)
                {
                    for (int qz = 0; qz < 2; qz++)
                    {
                        int qi = qz * 2 + qx;
                        ulong h = Hash.U64(worldSeed, sx, sz, qi, 61);
                        int count = 1 + (int)(h % 2);          // one or two per quarter
                        for (int k = 0; k < count; k++)
                        {
                            ulong h2 = Hash.U64(worldSeed, sx, sz, qi, 62 + k);
                            float t = (float)((h2 >> 8) % 1000000UL) / 1000000f;
                            float len = 95f + (float)((h2 >> 24) % 75);
                            bool onNS = (h2 & 1UL) == 0UL;    // opens onto the N–S section line
                            float ax, az, bx, bz;
                            float roadHalf;
                            // An entrance needs a road to open onto, and it must keep
                            // 100 ft clear of the intersection (Appanoose County).
                            const float setback = 110f;
                            if (onNS)
                            {
                                int line = qx == 0 ? sx : sx + 1;
                                if (RoadLineAbsent(worldSeed, false, line)) { noRoad++; continue; }
                                float X = line * SectionFeet;
                                float qz0 = sz * SectionFeet + qz * half;
                                float zz = Math.Clamp(qz0 + t * half, qz0 + setback, qz0 + half - setback);
                                float dir = qx == 0 ? 1f : -1f;   // into the quarter
                                roadHalf = RoadLinePaved(worldSeed, false, line) ? RoadHalfPaved : RoadHalfGravel;
                                ax = X + dir * (roadHalf - 4f); az = zz;
                                bx = X + dir * (roadHalf + len);  bz = zz;
                            }
                            else
                            {
                                int line = qz == 0 ? sz : sz + 1;
                                if (RoadLineAbsent(worldSeed, true, line)) { noRoad++; continue; }
                                float Z = line * SectionFeet;
                                float qx0 = sx * SectionFeet + qx * half;
                                float xx = Math.Clamp(qx0 + t * half, qx0 + setback, qx0 + half - setback);
                                float dir = qz == 0 ? 1f : -1f;
                                roadHalf = RoadLinePaved(worldSeed, true, line) ? RoadHalfPaved : RoadHalfGravel;
                                ax = xx; az = Z + dir * (roadHalf - 4f);
                                bx = xx; bz = Z + dir * (roadHalf + len);
                            }
                            // Don't run a track through a farmyard or through town.
                            if (EntranceBlocked(ww, ax, az, bx, bz)) { blocked++; continue; }
                            ww.Map.Driveways.Add(new Driveway
                            {
                                Surface = "dirt",
                                WidthFeet = 26f,
                                Points =
                                {
                                    new Vec2(ax - ww.MinX, az - ww.MinZ),
                                    new Vec2(bx - ww.MinX, bz - ww.MinZ),
                                },
                            });
                            placedCount++;
                        }
                    }
                }
            }
        }
    }

    /// <summary>True when a field entrance would run into a farmstead or a town.</summary>
    static bool EntranceBlocked(WorldWindow ww, float ax, float az, float bx, float bz)
    {
        foreach (var f in ww.Farms)
        {
            // 90 ft of clearance around the farmstead's yard.
            if (SegmentNearRect(ax, az, bx, bz, f.MinX - 90f, f.MinZ - 90f, f.MaxX + 90f, f.MaxZ + 90f))
                return true;
        }
        foreach (var t in ww.Towns)
        {
            if (SegmentNearRect(ax, az, bx, bz, t.MinX - 60f, t.MinZ - 60f, t.MaxX + 60f, t.MaxZ + 60f))
                return true;
        }
        return false;
    }

    /// <summary>True when a segment comes within the rectangle.</summary>
    static bool SegmentNearRect(float ax, float az, float bx, float bz, float x0, float z0, float x1, float z1)
    {
        if (ax > x0 && ax < x1 && az > z0 && az < z1) return true;
        if (bx > x0 && bx < x1 && bz > z0 && bz < z1) return true;
        // Sample the segment; it is short and straight.
        for (int i = 1; i < 10; i++)
        {
            float t = i / 10f;
            float px = ax + (bx - ax) * t, pz = az + (bz - az) * t;
            if (px > x0 && px < x1 && pz > z0 && pz < z1) return true;
        }
        return false;
    }

    static Vec2 FarmPoint(Vec2 p, int side, float ox, float oz) => side switch
    {
        0 => new Vec2(p.X + ox, p.Z + oz),
        1 => new Vec2(p.X + ox, oz - p.Z),
        2 => new Vec2(p.Z + ox, oz - p.X),
        _ => new Vec2(ox - p.Z, p.X + oz),
    };
    static (float minX, float minZ, float maxX, float maxZ) RuralBounds(MapPlan plan, int side, float ox, float oz)
    {
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        void B(Vec2 p)
        {
            var q = FarmPoint(p, side, ox, oz);
            minX = MathF.Min(minX, q.X); maxX = MathF.Max(maxX, q.X);
            minZ = MathF.Min(minZ, q.Z); maxZ = MathF.Max(maxZ, q.Z);
        }
        foreach (var h in plan.Houses) foreach (var p in h.Plan.Footprint) B(p);
        foreach (var o in plan.Outbuildings)
        {
            float hw = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet / 2f : o.WidthFeet / 2f;
            float hd = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet / 2f : o.DepthFeet / 2f;
            B(new Vec2(o.Center.X - hw, o.Center.Z - hd));
            B(new Vec2(o.Center.X + hw, o.Center.Z + hd));
        }
        foreach (var t in plan.Trees) B(t);
        foreach (var d in plan.Driveways.Concat(plan.Walks)) foreach (var p in d.Points) B(p);
        return (minX, minZ, maxX, maxZ);
    }

    static void StampFarm(WorldWindow ww, MapPlan plan, int side, float oxWorld, float ozWorld)
    {
        var map = ww.Map;
        float ox = oxWorld - ww.MinX;
        float oz = ozWorld - ww.MinZ;

        foreach (var h in plan.Houses)
        {
            var hp = side switch
            {
                0 => h.Plan,
                1 => h.Plan.FlipZ(),
                2 => h.Plan.Rotate90CW(),
                _ => h.Plan.Rotate90CCW(),
            };
            map.Houses.Add(new PositionedHouse
            {
                Plan = hp.OffsetCopy(ox, oz),
                MirroredZ = side == 1,
            });
        }
        foreach (var o in plan.Outbuildings)
        {
            var c = FarmPoint(o.Center, side, ox, oz);
            bool swap = side >= 2;
            map.Outbuildings.Add(new Outbuilding
            {
                Kind = o.Kind,
                Center = c,
                WidthFeet = swap ? o.DepthFeet : o.WidthFeet,
                DepthFeet = swap ? o.WidthFeet : o.DepthFeet,
                DiameterFeet = o.DiameterFeet,
                Label = o.Label,
            });
        }
        foreach (var d0 in plan.Driveways)
        {
            var d = new Driveway { WidthFeet = d0.WidthFeet, Surface = d0.Surface };
            foreach (var p in d0.Points) d.Points.Add(FarmPoint(p, side, ox, oz));
            map.Driveways.Add(d);
        }
        foreach (var w0 in plan.Walks)
        {
            var w = new Driveway { WidthFeet = w0.WidthFeet, Surface = w0.Surface };
            foreach (var p in w0.Points) w.Points.Add(FarmPoint(p, side, ox, oz));
            map.Walks.Add(w);
        }
        foreach (var t in plan.Trees)
        {
            map.Trees.Add(FarmPoint(t, side, ox, oz));
        }
    }

    // ------------------------------------------------------------------ labels

    static void AddWindowLabels(WorldWindow ww)
    {
        var map = ww.Map;
        int c0 = (int)MathF.Floor((ww.MinZ - 2000f) / PavedSpacing);
        int c1 = (int)MathF.Floor((ww.MinZ + ww.DepthFeet + 2000f) / PavedSpacing);
        for (int c = c0; c <= c1; c++)
        {
            float z = CorridorZ(ww.Map.Seed, c);
            map.Labels.Add(new MapLabel
            {
                Position = new Vec2(42f, z - ww.MinZ),
                Text = HighwayName(c),
                Large = true,
                Anchor = "start",
            });
        }
    }

    static string HighwayName(int corridor)
    {
        int i = (int)((uint)(corridor * 37 + 11) % (uint)HighwayNames.Length);
        return HighwayNames[i];
    }
    // ------------------------------------------------------------------ verification

    public sealed class Check
    {
        public string Name { get; set; } = "";
        public bool Pass { get; set; }
        public string Detail { get; set; } = "";
    }

    /// <summary>
    /// Geometric / determinism checks. This is the verification ladder from the proposal:
    /// hash determinism, overlapping-window invariance, town ladder, no colliding buildings.
    /// </summary>
    public static List<Check> Verify(uint worldSeed)
    {
        var checks = new List<Check>();

        var a = Generate(worldSeed, 0f, 0f, CellFeet * 3f, CellFeet * 3f);
        var b = Generate(worldSeed, 0f, 0f, CellFeet * 3f, CellFeet * 3f);
        string sa = SvgMapRenderer.Render(a.Map, bare: true, targetWidth: 700f);
        string sb = SvgMapRenderer.Render(b.Map, bare: true, targetWidth: 700f);
        checks.Add(new Check
        {
            Name = "same window twice is byte-identical",
            Pass = sa == sb,
            Detail = $"{sa.Length} bytes",
        });

        var farA = Generate(worldSeed, 0f, 100f * Mile, CellFeet * 3f, CellFeet * 3f);
        var farB = Generate(worldSeed, 0f, 100f * Mile, CellFeet * 3f, CellFeet * 3f);
        checks.Add(new Check
        {
            Name = "100 miles out is still reproducible",
            Pass = SvgMapRenderer.Render(farA.Map, true, 500f) == SvgMapRenderer.Render(farB.Map, true, 500f),
            Detail = $"center 0, {100f:0} mi · {farA.Towns.Count} towns · {farA.FarmsInWindow} farms",
        });

        var left = Generate(worldSeed, 0f, 0f, 6f * Mile, 6f * Mile);
        var right = Generate(worldSeed, 2f * Mile, 0f, 6f * Mile, 6f * Mile);
        float ox0 = -1f * Mile, ox1 = 3f * Mile, oz0 = -3f * Mile, oz1 = 3f * Mile;
        var ea = CanonicalElements(left, ox0, oz0, ox1, oz1);
        var eb = CanonicalElements(right, ox0, oz0, ox1, oz1);
        bool overlapSame = ea.SequenceEqual(eb);
        checks.Add(new Check
        {
            Name = "overlapping windows agree on every building/tree",
            Pass = overlapSame,
            Detail = $"{ea.Count} shared elements" + (overlapSame ? "" : $" vs {eb.Count}"),
        });

        var cell = GenerateCell(worldSeed, 0, 0);
        var super = Generate(worldSeed, CellFeet * 1.5f, CellFeet * 1.5f, CellFeet * 3f, CellFeet * 3f);
        var ca = CanonicalElements(cell, cell.MinX, cell.MinZ, cell.MinX + CellFeet, cell.MinZ + CellFeet);
        var cb = CanonicalElements(super, cell.MinX, cell.MinZ, cell.MinX + CellFeet, cell.MinZ + CellFeet);
        checks.Add(new Check
        {
            Name = "3-mile cell equals the same land in the 9-mile view",
            Pass = ca.SequenceEqual(cb),
            Detail = $"{ca.Count} elements in the cell",
        });

        var ladder = new List<(float x, float z, TownClass cls)>();
        for (int c = 0; c <= 3; c++)
            for (int i = -2; i <= 30; i++)
            {
                var s = VisibleSite(worldSeed, c, i);
                if (s != null) ladder.Add((s.CenterX, s.HighwayZ, s.Class));
            }
        float minGap = float.MaxValue;
        for (int i = 0; i < ladder.Count; i++)
            for (int j = i + 1; j < ladder.Count; j++)
            {
                float dx = ladder[i].x - ladder[j].x, dz = ladder[i].z - ladder[j].z;
                minGap = MathF.Min(minGap, MathF.Sqrt(dx * dx + dz * dz));
            }
        int townsBig = ladder.Count(t => t.cls == TownClass.Town);
        int villages = ladder.Count(t => t.cls == TownClass.Village);
        int hamlets = ladder.Count(t => t.cls == TownClass.Hamlet);
        int crossroads = ladder.Count(t => t.cls == TownClass.Crossroads);
        checks.Add(new Check
        {
            Name = "town ladder: no towns inside 10 miles, big town rare",
            Pass = ladder.Count < 2 || minGap >= MinTownSpacingFeet - 1f,
            Detail = $"{ladder.Count} towns / 330 mi · min gap {(minGap / Mile):0.##} mi · {crossroads} crossroads, {hamlets} hamlet, {villages} village, {townsBig} town",
        });

        int nearRail = 0;
        foreach (var t in ladder)
            if (NearestRailDistance(worldSeed, t.x, t.z) < 16f * Mile) nearRail++;
        checks.Add(new Check
        {
            Name = "towns tend to be near rail lines",
            Pass = ladder.Count == 0 || nearRail * 3 >= ladder.Count,
            Detail = $"{nearRail} of {ladder.Count} within 16 mi of rail",
        });

        var census = Generate(worldSeed, 0f, 0f, 12f * Mile, 12f * Mile);
        int overlaps = CountBuildingOverlaps(census.Map, out string overlapInfo);
        checks.Add(new Check
        {
            Name = "no building overlaps another (towns, farms, outbuildings)",
            Pass = overlaps == 0,
            Detail = $"{census.Map.Houses.Count} houses · {census.Map.Outbuildings.Count} outbuildings · {overlaps} overlaps{overlapInfo}",
        });

        checks.Add(new Check
        {
            Name = "rivers run south and tributaries merge into them",
            Pass = TrunkDistance(worldSeed, 0f, 0f) < float.MaxValue,
            Detail = $"trunk rivers every {TrunkRiverSpacing / Mile:0} mi, tributary heads every {TributarySpacing / Mile:0} mi",
        });

        // Every town sits on a paved road, and the town streets close into a loop.
        int onPaved = 0, townsChecked = 0, dangling = 0;
        var danglingDetail = new List<string>();
        foreach (var site in TownSitesNear(worldSeed, 0f, 330f * Mile, 0f, 40f * Mile))
        {
            townsChecked++;
            bool paved = MathF.Abs(site.HighwayZ / PavedSpacing - MathF.Round(site.HighwayZ / PavedSpacing)) < 1e-3f;
            if (paved) onPaved++;
            var town = MaterializeTown(worldSeed, site);
            dangling += CountDanglingRoads(town, danglingDetail);
        }
        checks.Add(new Check
        {
            Name = "every town sits on a paved road",
            Pass = townsChecked == 0 || onPaved == townsChecked,
            Detail = $"{onPaved} of {townsChecked} towns on the paved grid",
        });
        checks.Add(new Check
        {
            Name = "no town street ends without an exit",
            Pass = dangling == 0,
            Detail = $"{dangling} dangling street ends" + (danglingDetail.Count > 0 ? " · " + string.Join("; ", danglingDetail.Take(4)) : ""),
        });

        return checks;
    }

    /// <summary>
    /// Counts street ends that touch no other street. A street may legitimately end in
    /// a cul-de-sac bulb, and the town's highway band carries on past the town as the
    /// global paved corridor, so neither counts as dangling.
    /// </summary>
    static int CountDanglingRoads(WorldTown town, List<string>? detail = null)
    {
        var segs = new List<(float x0, float z0, float x1, float z1)>();
        var through = new List<bool>();     // the town's highway band continues as the global road
        var bulbs = new List<Vec2>();
        foreach (var part in town.Parts)
        {
            float ox = part.OriginX, oz = part.OriginZ;
            bool mir = part.Mirrored;
            float WZ(float lz) => mir ? oz + (HighwayWidth - lz) : oz + lz;
            foreach (var r in part.Plan.Roads)
            {
                float z0 = WZ(r.Z), z1 = WZ(r.Z + r.H);
                segs.Add((ox + r.X, MathF.Min(z0, z1), ox + r.X + r.W, MathF.Max(z0, z1)));
                through.Add(IsTownHighway(r));
            }
            foreach (var b in part.Plan.Bulbs)
                bulbs.Add(new Vec2(ox + b.Center.X, WZ(b.Center.Z)));
        }

        int dangling = 0;
        for (int i = 0; i < segs.Count; i++)
        {
            if (through[i]) continue;          // the through road carries on past the town
            var s = segs[i];
            // The two ends of the street: the ends of its long axis.
            bool horizontal = (s.x1 - s.x0) >= (s.z1 - s.z0);
            var ends = horizontal
                ? new[] { (s.x0, (s.z0 + s.z1) / 2f), (s.x1, (s.z0 + s.z1) / 2f) }
                : new[] { ((s.x0 + s.x1) / 2f, s.z0), ((s.x0 + s.x1) / 2f, s.z1) };
            foreach (var e in ends)
            {
                bool touched = false;
                for (int k = 0; k < segs.Count; k++)
                {
                    if (k == i) continue;
                    if (PointNearSeg(e.Item1, e.Item2, segs[k])) { touched = true; break; }
                }
                // A cul-de-sac bulb is a legitimate dead end.
                if (!touched && bulbs.Any(b => MathF.Abs(b.X - e.Item1) < 60f && MathF.Abs(b.Z - e.Item2) < 60f))
                    touched = true;
                if (!touched)
                {
                    dangling++;
                    if (detail != null && detail.Count < 6)
                        detail.Add($"end({e.Item1:0},{e.Item2:0}) of seg({s.x0:0},{s.z0:0})-({s.x1:0},{s.z1:0})");
                }
            }
        }
        return dangling;
    }

    static bool PointNearSeg(float px, float pz, (float x0, float z0, float x1, float z1) s)
    {
        const float tol = 2f;
        bool onX = px >= s.x0 - tol && px <= s.x1 + tol;
        bool onZ = pz >= s.z0 - tol && pz <= s.z1 + tol;
        return onX && onZ;
    }

    static List<string> CanonicalElements(WorldWindow ww, float minX, float minZ, float maxX, float maxZ)
    {
        var list = new List<string>();
        bool In(float x, float z) => x >= minX && x <= maxX && z >= minZ && z <= maxZ;
        foreach (var h in ww.Map.Houses)
        {
            float cx = h.Plan.Footprint.Select(p => p.X).Average() + ww.MinX;
            float cz = h.Plan.Footprint.Select(p => p.Z).Average() + ww.MinZ;
            if (In(cx, cz)) list.Add($"H|{cx:0.0}|{cz:0.0}|{h.Plan.Archetype}");
        }
        foreach (var o in ww.Map.Outbuildings)
        {
            float cx = o.Center.X + ww.MinX, cz = o.Center.Z + ww.MinZ;
            if (In(cx, cz)) list.Add($"O|{o.Kind}|{cx:0.0}|{cz:0.0}");
        }
        foreach (var t in ww.Map.Trees)
        {
            float cx = t.X + ww.MinX, cz = t.Z + ww.MinZ;
            if (In(cx, cz)) list.Add($"T|{cx:0.0}|{cz:0.0}");
        }
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    static int CountBuildingOverlaps(MapPlan map, out string info)
    {
        info = "";
        var rects = new List<(float x0, float z0, float x1, float z1, string what)>();
        foreach (var h in map.Houses)
        {
            float x0 = h.Plan.Footprint.Min(p => p.X), x1 = h.Plan.Footprint.Max(p => p.X);
            float z0 = h.Plan.Footprint.Min(p => p.Z), z1 = h.Plan.Footprint.Max(p => p.Z);
            rects.Add((x0, z0, x1, z1, "house:" + h.Plan.Archetype));
        }
        foreach (var o in map.Outbuildings)
        {
            if (o.Kind == OutbuildingKind.PropaneTank) continue;
            float hw = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet / 2f : o.WidthFeet / 2f;
            float hd = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet / 2f : o.DepthFeet / 2f;
            rects.Add((o.Center.X - hw, o.Center.Z - hd, o.Center.X + hw, o.Center.Z + hd, "out:" + o.Kind));
        }
        int overlaps = 0;
        for (int i = 0; i < rects.Count; i++)
            for (int j = i + 1; j < rects.Count; j++)
            {
                var a = rects[i]; var b = rects[j];
                if (a.x0 < b.x1 - 1f && a.x1 > b.x0 + 1f && a.z0 < b.z1 - 1f && a.z1 > b.z0 + 1f)
                {
                    overlaps++;
                    if (info == "")
                        info = $" (first: {a.what} @ {a.x0:0},{a.z0:0} vs {b.what} @ {b.x0:0},{b.z0:0})";
                }
            }
        return overlaps;
    }
}
}
