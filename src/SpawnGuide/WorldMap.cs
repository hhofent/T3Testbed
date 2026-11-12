using System;
using System.Collections.Generic;
using System.Linq;

namespace SpawnGuide
{

/// <summary>
/// A town placed in the world. Towns sit on the continuous east-west highway grid,
/// centred in a one-mile section, with their commercial highway strip on the section
/// line. That keeps every town on a real through-road and keeps the town footprint
/// inside the section grid (the hull preset overhangs a section line by ~58 ft).
/// </summary>
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
/// Design decisions that came out of grilling docs/proposal-infinite-map.md:
///  - Towns are a *queryable* lattice, not an unknowable Poisson field: one candidate
///    per 10-mile slot on each 24-mile east-west highway corridor, with deterministic
///    priority suppression inside 10 miles. Any window can enumerate the same towns.
///  - Rendering is window-based, not cell-based. Cells are the lens request unit; the
///    content function is pure in world coordinates. Tiling independent 3-mi cells
///    would duplicate boundary roads and clip buildings at every seam.
///  - Farmsteads get per-section hashed RNG streams, so adding/removing anything in a
///    neighbouring section cannot perturb them.
///  - County section roads are clipped around town footprints, where the town street
///    network takes over; the town streets themselves keep the "attach at both ends"
///    rule. The highway through a town is continuous and never clipped.
///  - The town south fringe is dropped in world mode so the highway is the town's
///    south edge — the real George/Boyden/Hull pattern and the cleanest seam.
/// </summary>
public static class WorldMap
{
    public const float Mile = 5280f;
    public const float SectionFeet = 5280f;
    public const float CellFeet = 15840f;                 // 3-mile lens cell
    public const float HighwaySpacing = 24f * Mile;        // east-west highway every 24 mi
    public const float TownSlotFeet = 10f * Mile;          // one town candidate per 10 mi
    public const float MinTownSpacingFeet = 10f * Mile;    // never two towns closer than 10 mi
    const float RoadHalf = 33f;                            // 66-ft section/highway ROW
    const float CreekSpacing = 8f * Mile;
    const float FarmMargin = 300f;

    static readonly string[] FieldPalette =
        { "#ded9bd", "#d6d0b4", "#cbc4a2", "#e4e0cb", "#d2cbb0", "#dcd6b8", "#d8d2b6" };

    static readonly string[] HighwayNames =
        { "HWY 9", "HWY 3", "HWY 44", "HWY 60", "HWY 71", "HWY 20", "HWY 34", "HWY 59" };

    // ------------------------------------------------------------------ public API

    /// <summary>
    /// Generate the world visible through a lens window. Pure in
    /// (worldSeed, centerX, centerZ, width, depth): visit order, direction and window
    /// size never change what exists at a world coordinate.
    /// </summary>
    public static WorldWindow Generate(uint worldSeed, float centerX, float centerZ,
        float widthFeet = CellFeet * 3f, float depthFeet = CellFeet * 3f)
    {
        widthFeet = Math.Max(Mile, widthFeet);
        depthFeet = Math.Max(Mile, depthFeet);

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
            RoadSurface = "asphalt",
            LodSimple = false,
            QuietAnnotations = true,
            SuppressOutbuildingLabels = true,
        };
        ww.Map = map;

        AddFields(ww, worldSeed);
        AddTowns(ww, worldSeed);
        AddRoads(ww, worldSeed);
        AddCreeks(ww, worldSeed);
        AddFarms(ww, worldSeed);
        AddWindowLabels(ww);

        return ww;
    }

    /// <summary>Convenience for the 3-mile cell view.</summary>
    public static WorldWindow GenerateCell(uint worldSeed, int cellX, int cellZ) =>
        Generate(worldSeed,
            (cellX + 0.5f) * CellFeet,
            (cellZ + 0.5f) * CellFeet,
            CellFeet, CellFeet);

    /// <summary>Find the nearest town to a point, looking along nearby highway corridors.</summary>
    public static WorldTown? NearestTown(uint worldSeed, float x, float z, float maxMiles = 80f)
    {
        float bestD = maxMiles * Mile * maxMiles * Mile;
        WorldTown? best = null;
        foreach (var site in TownsNear(worldSeed, x - maxMiles * Mile, x + maxMiles * Mile,
                     z - maxMiles * Mile, z + maxMiles * Mile))
        {
            float dx = site.CenterX - x, dz = site.HighwayZ - z;
            float d = dx * dx + dz * dz;
            if (d < bestD) { bestD = d; best = site; }
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
    }

    // ------------------------------------------------------------------ town field

    enum TownClass { None, Hamlet, Boyden, Hull }

    sealed class TownSite
    {
        public int Corridor;
        public int Slot;
        public TownClass Class;
        public float RawX;
        public float CenterX;
        public float HighwayZ;
        public uint Seed;
        public ulong Priority;
    }

    static TownSite RawSite(uint worldSeed, int corridor, int slot)
    {
        ulong h = Hash.U64(worldSeed, corridor, slot, 11);
        var rng = new Rng((uint)h);
        float roll = rng.NextFloat();
        TownClass cls = roll < 0.10f ? TownClass.Hull
            : roll < 0.40f ? TownClass.Boyden
            : roll < 0.76f ? TownClass.Hamlet
            : TownClass.None;
        float jitter = rng.Range(-0.15f, 0.15f) * TownSlotFeet;
        float rawX = (slot + 0.5f) * TownSlotFeet + jitter;
        uint seed = rng.NextUInt();
        ulong priority = ((ulong)rng.NextUInt() << 32) ^ rng.NextUInt();
        float highwayZ = corridor * HighwaySpacing;
        // Towns are centred in a section so no county grid road cuts the block grid.
        float centerX = (MathF.Floor(rawX / SectionFeet) + 0.5f) * SectionFeet;
        return new TownSite
        {
            Corridor = corridor, Slot = slot, Class = cls, RawX = rawX,
            CenterX = centerX, HighwayZ = highwayZ, Seed = seed, Priority = priority,
        };
    }

    static bool Beats(TownSite a, TownSite b) =>
        a.Priority > b.Priority || (a.Priority == b.Priority && a.Slot < b.Slot);

    static TownSite? VisibleSite(uint worldSeed, int corridor, int slot)
    {
        var s = RawSite(worldSeed, corridor, slot);
        if (s.Class == TownClass.None) return null;
        // Hash-based priority suppression: the strongest candidate inside 10 mi survives.
        // This is a query, not a global pass — the same answer from any window.
        for (int dj = -1; dj <= 1; dj += 2)
        {
            var n = RawSite(worldSeed, corridor, slot + dj);
            if (n.Class == TownClass.None) continue;
            if (MathF.Abs(n.CenterX - s.CenterX) < MinTownSpacingFeet && Beats(n, s)) return null;
        }
        return s;
    }

    static string SizeName(TownClass c) =>
        c == TownClass.Hull ? "hull" : c == TownClass.Boyden ? "boyden" : "hamlet";

    static string ClassName(TownClass c) =>
        c == TownClass.Hull ? "HULL" : c == TownClass.Boyden ? "BOYDEN" : "HAMLET";

    /// <summary>Towns whose footprint overlaps the query rectangle (expanded a little).</summary>
    static List<WorldTown> TownsNear(uint worldSeed, float minX, float maxX, float minZ, float maxZ)
    {
        var result = new List<WorldTown>();
        int k0 = (int)MathF.Floor((minZ - 4000f) / HighwaySpacing);
        int k1 = (int)MathF.Floor((maxZ + 4000f) / HighwaySpacing);
        int i0 = (int)MathF.Floor((minX - 12000f) / TownSlotFeet);
        int i1 = (int)MathF.Floor((maxX + 12000f) / TownSlotFeet);
        for (int k = k0; k <= k1; k++)
        {
            for (int i = i0; i <= i1; i++)
            {
                var site = VisibleSite(worldSeed, k, i);
                if (site == null) continue;
                var wt = MaterializeTown(worldSeed, site);
                if (wt.MaxX < minX - 200f || wt.MinX > maxX + 200f ||
                    wt.MaxZ < minZ - 200f || wt.MinZ > maxZ + 200f) continue;
                result.Add(wt);
            }
        }
        return result;
    }

    static WorldTown MaterializeTown(uint worldSeed, TownSite site)
    {
        var town = SmallTownGenerator.Generate(site.Seed, SizeName(site.Class), detail: false);
        float ox = site.CenterX - town.WidthFeet / 2f;
        float oz = site.HighwayZ - RoadHalf;
        var b = TownContentBounds(town, ox, oz);
        return new WorldTown
        {
            Corridor = site.Corridor,
            Slot = site.Slot,
            SizeName = SizeName(site.Class),
            ClassLabel = ClassName(site.Class),
            Name = town.PlaceName,
            TownSeed = site.Seed,
            CenterX = site.CenterX,
            HighwayZ = site.HighwayZ,
            MinX = b.minX, MinZ = b.minZ, MaxX = b.maxX, MaxZ = b.maxZ,
            Houses = town.Houses.Count,
        };
    }

    static void AddTowns(WorldWindow ww, uint worldSeed)
    {
        var towns = TownsNear(worldSeed, ww.MinX, ww.MinX + ww.WidthFeet, ww.MinZ, ww.MinZ + ww.DepthFeet);
        foreach (var wt in towns)
        {
            ww.Towns.Add(wt);
            var town = SmallTownGenerator.Generate(wt.TownSeed, wt.SizeName, detail: false);
            float ox = wt.CenterX - town.WidthFeet / 2f;
            float oz = wt.HighwayZ - RoadHalf;
            StampTown(ww, town, wt, ox, oz);
        }
    }

    /// <summary>World-space bounds of everything the world stamper will keep from a town.</summary>
    static (float minX, float minZ, float maxX, float maxZ) TownContentBounds(MapPlan town, float ox, float oz)
    {
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        void B(float x, float z) { minX = MathF.Min(minX, x); maxX = MathF.Max(maxX, x); minZ = MathF.Min(minZ, z); maxZ = MathF.Max(maxZ, z); }
        void R(MapRect r) { B(ox + r.X, oz + r.Z); B(ox + r.X + r.W, oz + r.Z + r.H); }

        for (int i = 0; i < town.Roads.Count; i++)
        {
            var r = town.Roads[i];
            if (IsTownHighway(r)) continue;
            R(r);
        }
        foreach (var r in town.Alleys) R(r);
        foreach (var r in town.LotLines) R(r);
        foreach (var h in town.Houses)
        {
            if (CenterZ(h.Plan) < -1f) continue;
            foreach (var p in h.Plan.Footprint) B(ox + p.X, oz + p.Z);
        }
        foreach (var o in town.Outbuildings)
        {
            if (o.Center.Z < -1f) continue;
            float hw = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet / 2f : o.WidthFeet / 2f;
            float hd = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet / 2f : o.DepthFeet / 2f;
            B(ox + o.Center.X - hw, oz + o.Center.Z - hd);
            B(ox + o.Center.X + hw, oz + o.Center.Z + hd);
        }
        foreach (var t in town.Trees) B(ox + t.X, oz + t.Z);
        foreach (var d in town.Driveways.Concat(town.Walks))
            foreach (var p in d.Points)
                if (p.Z >= -1f) B(ox + p.X, oz + p.Z);
        foreach (var b in town.Bulbs) { B(ox + b.Center.X - b.RadiusFeet, oz + b.Center.Z - b.RadiusFeet); B(ox + b.Center.X + b.RadiusFeet, oz + b.Center.Z + b.RadiusFeet); }
        if (minX == float.MaxValue) return (ox, oz, ox + town.WidthFeet, oz + town.DepthFeet);
        return (minX, minZ, maxX, maxZ);
    }

    static float CenterZ(HousePlan h) => h.Footprint.Select(p => p.Z).Average();

    static bool IsTownHighway(MapRect r) =>
        MathF.Abs(r.Z) < 0.5f && MathF.Abs(r.H - 66f) < 0.5f;

    static void StampTown(WorldWindow ww, MapPlan town, WorldTown wt, float oxWorld, float ozWorld)
    {
        var map = ww.Map;
        float ox = oxWorld - ww.MinX;
        float oz = ozWorld - ww.MinZ;

        for (int i = 0; i < town.Roads.Count; i++)
        {
            var r = town.Roads[i];
            if (IsTownHighway(r)) continue;    // the continuous global highway is drawn once
            map.Roads.Add(new MapRect(r.X + ox, r.Z + oz, r.W, r.H));
            map.RoadDashed.Add(i < town.RoadDashed.Count && town.RoadDashed[i]);
        }
        foreach (var r in town.Alleys)
            map.Alleys.Add(new MapRect(r.X + ox, r.Z + oz, r.W, r.H));
        foreach (var r in town.LotLines)
            map.LotLines.Add(new MapRect(r.X + ox, r.Z + oz, r.W, r.H));
        foreach (var w in town.Walks)
        {
            if (!w.Points.Any(p => p.Z >= -1f)) continue;
            var d = new Driveway { WidthFeet = w.WidthFeet, Surface = w.Surface };
            foreach (var p in w.Points) d.Points.Add(new Vec2(p.X + ox, p.Z + oz));
            map.Walks.Add(d);
        }
        foreach (var d0 in town.Driveways)
        {
            if (!d0.Points.Any(p => p.Z >= -1f)) continue;
            var d = new Driveway { WidthFeet = d0.WidthFeet, Surface = d0.Surface };
            foreach (var p in d0.Points) d.Points.Add(new Vec2(p.X + ox, p.Z + oz));
            map.Driveways.Add(d);
        }
        foreach (var h in town.Houses)
        {
            if (CenterZ(h.Plan) < -1f) continue;    // south-of-highway fringe: not in world mode
            map.Houses.Add(new PositionedHouse
            {
                Plan = h.Plan.OffsetCopy(ox, oz),
                MirroredZ = h.MirroredZ,
            });
        }
        foreach (var o in town.Outbuildings)
        {
            if (o.Center.Z < -1f) continue;
            map.Outbuildings.Add(new Outbuilding
            {
                Kind = o.Kind,
                Center = new Vec2(o.Center.X + ox, o.Center.Z + oz),
                WidthFeet = o.WidthFeet,
                DepthFeet = o.DepthFeet,
                DiameterFeet = o.DiameterFeet,
                Label = o.Label,
            });
        }
        foreach (var t in town.Trees)
            map.Trees.Add(new Vec2(t.X + ox, t.Z + oz));
        foreach (var b in town.Bulbs)
            map.Bulbs.Add(new RoadBulb { Center = new Vec2(b.Center.X + ox, b.Center.Z + oz), RadiusFeet = b.RadiusFeet, Fill = b.Fill });

        map.Labels.Add(new MapLabel
        {
            Position = new Vec2(wt.CenterX - ww.MinX, wt.MaxZ - ww.MinZ - 26f),
            Text = $"{wt.Name} · {wt.ClassLabel}",
            Large = true,
        });
    }

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

    // ------------------------------------------------------------------ roads

    static bool IsHighway(float z)
    {
        float k = z / HighwaySpacing;
        return MathF.Abs(k - MathF.Round(k)) < 0.001f;
    }

    static string HighwayName(int corridor)
    {
        if (corridor == 0) return "HWY 18";
        int i = (int)((uint)(corridor * 37 + 11) % (uint)HighwayNames.Length);
        return HighwayNames[i];
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

    static void AddRoads(WorldWindow ww, uint worldSeed)
    {
        var map = ww.Map;
        float minX = ww.MinX, maxX = ww.MinX + ww.WidthFeet;
        float minZ = ww.MinZ, maxZ = ww.MinZ + ww.DepthFeet;

        int ix0 = (int)MathF.Ceiling(minX / SectionFeet);
        int ix1 = (int)MathF.Floor(maxX / SectionFeet);
        for (int ix = ix0; ix <= ix1; ix++)
        {
            float x = ix * SectionFeet;
            var spans = new List<(float a, float b)> { (minZ, maxZ) };
            foreach (var t in ww.Towns)
            {
                if (t.MaxX >= x - RoadHalf && t.MinX <= x + RoadHalf)
                    spans = Subtract(spans, t.MinZ, t.MaxZ);
            }
            foreach (var s in spans)
            {
                if (s.b - s.a < 1f) continue;
                map.Roads.Add(new MapRect(x - RoadHalf - ww.MinX, s.a - ww.MinZ, RoadHalf * 2f, s.b - s.a));
                map.RoadDashed.Add(false);
            }
        }

        int iz0 = (int)MathF.Ceiling(minZ / SectionFeet);
        int iz1 = (int)MathF.Floor(maxZ / SectionFeet);
        for (int iz = iz0; iz <= iz1; iz++)
        {
            float z = iz * SectionFeet;
            bool highway = IsHighway(z);
            var spans = new List<(float a, float b)> { (minX, maxX) };
            foreach (var t in ww.Towns)
            {
                if (highway && MathF.Abs(t.HighwayZ - z) < 0.5f) continue;   // through-road
                if (t.MaxZ >= z - RoadHalf && t.MinZ <= z + RoadHalf)
                    spans = Subtract(spans, t.MinX, t.MaxX);
            }
            foreach (var s in spans)
            {
                if (s.b - s.a < 1f) continue;
                map.Roads.Add(new MapRect(s.a - ww.MinX, z - RoadHalf - ww.MinZ, s.b - s.a, RoadHalf * 2f));
                map.RoadDashed.Add(highway);
            }
        }
    }

    // ------------------------------------------------------------------ creeks

    static float CreekX(uint seed, int band, float z)
    {
        ulong h = Hash.U64(seed, band, 77);
        float p1 = ((h & 0xFFFF) / 65535f) * MathF.PI * 2f;
        float p2 = (((h >> 16) & 0xFFFF) / 65535f) * MathF.PI * 2f;
        return band * CreekSpacing
            + 2600f * MathF.Sin(z / 7000f + p1)
            + 900f * MathF.Sin(z / 2300f + p2);
    }

    static float CreekWidth(uint seed, int band)
    {
        ulong h = Hash.U64(seed, band, 78);
        return 42f + (h % 34);
    }

    static bool InsideTown(List<WorldTown> towns, float x, float z, float pad)
    {
        foreach (var t in towns)
            if (x >= t.MinX - pad && x <= t.MaxX + pad && z >= t.MinZ - pad && z <= t.MaxZ + pad)
                return true;
        return false;
    }

    static bool NearSectionRoad(float x, float z, float pad)
    {
        float dx = MathF.Abs(x / SectionFeet - MathF.Round(x / SectionFeet)) * SectionFeet;
        float dz = MathF.Abs(z / SectionFeet - MathF.Round(z / SectionFeet)) * SectionFeet;
        return dx < pad || dz < pad;
    }

    static void AddCreeks(WorldWindow ww, uint worldSeed)
    {
        int c0 = (int)MathF.Floor((ww.MinX - 5000f) / CreekSpacing);
        int c1 = (int)MathF.Floor((ww.MinX + ww.WidthFeet + 5000f) / CreekSpacing);
        for (int c = c0; c <= c1; c++)
        {
            float width = CreekWidth(worldSeed, c);
            var run = new List<Vec2>();
            var runs = new List<List<Vec2>> { run };
            for (float z = ww.MinZ - 2000f; z <= ww.MinZ + ww.DepthFeet + 2000f; z += 380f)
            {
                float x = CreekX(worldSeed, c, z);
                if (InsideTown(ww.Towns, x, z, 140f))
                {
                    if (run.Count >= 2) { run = new List<Vec2>(); runs.Add(run); }
                    else run.Clear();
                    continue;
                }
                run.Add(new Vec2(x, z));
            }
            foreach (var r in runs)
            {
                if (r.Count < 2) continue;
                var water = new WaterCourse { WidthFeet = width };
                foreach (var p in r) water.Points.Add(new Vec2(p.X - ww.MinX, p.Z - ww.MinZ));
                ww.Map.Waters.Add(water);

                // Tree-lined banks: the Floyd River / West Branch pattern. Never close to a
                // section road, and never inside a town (runs already exclude towns).
                var tr = new Rng(Hash.U32(worldSeed, c, 79));
                for (int i = 0; i < r.Count; i += 2)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (!tr.Chance(0.62f)) continue;
                        float off = width / 2f + tr.Range(18f, 85f);
                        var p = new Vec2(r[i].X + side * off + tr.Range(-12f, 12f), r[i].Z + tr.Range(-40f, 40f));
                        if (InsideTown(ww.Towns, p.X, p.Z, 80f)) continue;
                        if (NearSectionRoad(p.X, p.Z, 42f)) continue;
                        ww.Map.Trees.Add(new Vec2(p.X - ww.MinX, p.Z - ww.MinZ));
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
                        oz = side == 0 ? sz * SectionFeet + RoadHalf : (sz + 1) * SectionFeet - RoadHalf;
                    }
                    else
                    {
                        float along = sz * SectionFeet + FarmMargin + rng.Range(0f, MathF.Max(0f, span - lotW));
                        ox = side == 2 ? sx * SectionFeet + RoadHalf : (sx + 1) * SectionFeet - RoadHalf;
                        oz = side == 2 ? along + lotW : along;
                    }

                    var b = RuralBounds(plan, side, ox, oz);
                    if (b.maxX - b.minX <= 1f || b.maxZ - b.minZ <= 1f) continue;
                    if (IntersectsAnyTown(ww.Towns, b, 240f)) continue;
                    if (NearCreek(worldSeed, b)) continue;
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

    static bool NearCreek(uint worldSeed, (float minX, float minZ, float maxX, float maxZ) b)
    {
        int c0 = (int)MathF.Floor((b.minX - 5000f) / CreekSpacing);
        int c1 = (int)MathF.Floor((b.maxX + 5000f) / CreekSpacing);
        for (int c = c0; c <= c1; c++)
        {
            for (float z = b.minZ; z <= b.maxZ; z += 220f)
            {
                float cx = CreekX(worldSeed, c, z);
                if (cx > b.minX - 160f && cx < b.maxX + 160f) return true;
            }
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
            map.Trees.Add(FarmPoint(t, side, ox, oz));
    }

    // ------------------------------------------------------------------ labels

    static void AddWindowLabels(WorldWindow ww)
    {
        var map = ww.Map;
        int iz0 = (int)MathF.Ceiling(ww.MinZ / SectionFeet);
        int iz1 = (int)MathF.Floor((ww.MinZ + ww.DepthFeet) / SectionFeet);
        for (int iz = iz0; iz <= iz1; iz++)
        {
            float z = iz * SectionFeet;
            if (!IsHighway(z)) continue;
            int corridor = (int)MathF.Round(z / HighwaySpacing);
            map.Labels.Add(new MapLabel
            {
                Position = new Vec2(42f, z - ww.MinZ),
                Text = HighwayName(corridor),
                Large = true,
                Anchor = "start",
            });
        }
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

        var ladder = new List<(float x, TownClass cls)>();
        for (int i = -2; i <= 30; i++)
        {
            var s = VisibleSite(worldSeed, 0, i);
            if (s != null) ladder.Add((s.CenterX, s.Class));
        }
        ladder.Sort((x, y) => x.x.CompareTo(y.x));
        float minGap = float.MaxValue;
        for (int i = 1; i < ladder.Count; i++) minGap = MathF.Min(minGap, ladder[i].x - ladder[i - 1].x);
        int hulls = ladder.Count(t => t.cls == TownClass.Hull);
        int boydens = ladder.Count(t => t.cls == TownClass.Boyden);
        int hamlets = ladder.Count(t => t.cls == TownClass.Hamlet);
        checks.Add(new Check
        {
            Name = "town ladder: no towns inside 10 miles, hull rare",
            Pass = ladder.Count < 2 || minGap >= MinTownSpacingFeet - 1f,
            Detail = $"{ladder.Count} towns / 330 mi · min gap {(minGap / Mile):0.##} mi · {hamlets} hamlet, {boydens} boyden, {hulls} hull",
        });

        var census = Generate(worldSeed, 0f, 0f, 10f * Mile, 10f * Mile);
        int overlaps = CountBuildingOverlaps(census.Map, out string overlapInfo);
        checks.Add(new Check
        {
            Name = "no building overlaps another (towns, farms, outbuildings)",
            Pass = overlaps == 0,
            Detail = $"{census.Map.Houses.Count} houses · {census.Map.Outbuildings.Count} outbuildings · {overlaps} overlaps{overlapInfo}",
        });

        return checks;
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
