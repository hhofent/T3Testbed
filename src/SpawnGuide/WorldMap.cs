using System.Linq;
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
        /// <summary>Where the branch spur comes in, if the town has one.</summary>
        public TownContext Rail { get; set; } = TownContext.None;
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

        /// <summary>
        /// Generate the world visible through a lens window. Pure in
        /// (worldSeed, centerX, centerZ, width, depth): visit order, direction and window
        /// size never change what exists at a world coordinate.
        /// </summary>
        public static WorldWindow Generate(uint worldSeed, float centerX, float centerZ,
            float widthFeet = CellFeet * 3f, float depthFeet = CellFeet * 3f, WorldConfig? config = null, NoiseConfig? noiseConfig = null)
        {
            var cfg = config ?? new WorldConfig();
            var nc = noiseConfig ?? new NoiseConfig();

            widthFeet = Math.Max(cfg.MinSpan, widthFeet);
            depthFeet = Math.Max(cfg.MinSpan, depthFeet);

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
            AddHills(ww, worldSeed, nc);
            AddRivers(ww, worldSeed, nc);
            AddRail(ww, worldSeed, nc);

        // --- roads, then the towns that grow on them ----------------------------
            var sites = CollectTownSites(worldSeed, ww, nc);
            var towns = sites.Select(s => MaterializeTown(worldSeed, s, nc)).ToList();
            AddRoads(ww, worldSeed, towns, nc);
            AddRailSpurs(ww, towns, nc);
            StampTowns(ww, towns);

        // --- country ------------------------------------------------------------
            AddFarms(ww, worldSeed, nc);
            AddFieldEntrances(ww, worldSeed);
            AddWindowLabels(ww);

            return ww;
        }

        /// <summary>Convenience for the 3-mile cell view.</summary>
        public static WorldWindow GenerateCell(uint worldSeed, int cellX, int cellZ, WorldConfig? config = null, NoiseConfig? noiseConfig = null) =>
            Generate(worldSeed,
                (cellX + 0.5f) * CellFeet,
                (cellZ + 0.5f) * CellFeet,
                CellFeet, CellFeet, config, noiseConfig);

        /// <summary>Find the nearest town to a point, looking along nearby paved corridors.</summary>
        public static WorldTown? NearestTown(uint worldSeed, float x, float z, float maxMiles = 80f)
        {
            float bestD = maxMiles * Mile * maxMiles * Mile;
            WorldTown? best = null;
            var nc = new NoiseConfig();
            foreach (var site in TownSitesNear(worldSeed, x - maxMiles * Mile, x + maxMiles * Mile,
                         z - maxMiles * Mile, z + maxMiles * Mile, nc))
            {
                float dx = site.CenterX - x, dz = site.HighwayZ - z;
                float d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = MaterializeTown(worldSeed, site, nc); }
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

        // ------------------------------------------------------------------ terrain (HILLS - fBm)

        static float Phase(uint seed, int k) => Hash.F(seed, k, 701) * 6.2831853f;

        /// <summary>
        /// Ground elevation in feet using fBm (fractal Brownian motion).
        /// Great Plains: long, low swells with a little texture.
        /// </summary>
        public static float Elevation(uint worldSeed, float x, float z, NoiseConfig nc)
        {
            // Domain warp
            float wx = x, wz = z;
            if (nc.HillsDomainWarpAmp > 0 && nc.HillsDomainWarpOctaves > 0)
            {
                float warpX = 0, warpZ = 0;
                float amp = nc.HillsDomainWarpAmp;
                float freq = 1f / nc.HillsBaseWavelengthFt;
                for (int o = 0; o < nc.HillsDomainWarpOctaves; o++)
                {
                    float p1 = Phase(worldSeed, 100 + o * 2);
                    float p2 = Phase(worldSeed, 101 + o * 2);
                    warpX += amp * MathF.Sin((wx * freq) + p1);
                    warpZ += amp * MathF.Sin((wz * freq) + p2);
                    freq *= nc.HillsLacunarity;
                    amp *= nc.HillsPersistence;
                }
                wx += warpX;
                wz += warpZ;
            }

            // fBm
            float elev = 0;
            float amp2 = nc.HillsAmplitudeFt;
            float freq2 = 1f / nc.HillsBaseWavelengthFt;
            for (int o = 0; o < nc.HillsOctaves; o++)
            {
                float p1 = Phase(worldSeed, 1 + o * 5);
                float p2 = Phase(worldSeed, 2 + o * 5);
                float p3 = Phase(worldSeed, 3 + o * 5);
                elev += amp2 * MathF.Sin((wx * 0.62f + wz * 0.78f) * freq2 + p1);
                elev += amp2 * 0.6f * MathF.Sin((wx * -0.71f + wz * 0.46f) * freq2 * 2.1f + p2);
                elev += amp2 * 1.3f * MathF.Sin((wx * 0.31f + wz * 1.07f) * freq2 * 0.42f + p3);
                freq2 *= nc.HillsLacunarity;
                amp2 *= nc.HillsPersistence;
            }
            return elev;
        }

        /// <summary>Contour lines of the elevation field, on a globally anchored 1/4-mile lattice.</summary>
        static void AddHills(WorldWindow ww, uint worldSeed, NoiseConfig nc)
        {
            float cell = nc.ContourLatticeCellFt;
            int i0 = (int)MathF.Floor((ww.MinX - cell) / cell);
            int i1 = (int)MathF.Floor((ww.MinX + ww.WidthFeet + cell) / cell);
            int j0 = (int)MathF.Floor((ww.MinZ - cell) / cell);
            int j1 = (int)MathF.Floor((ww.MinZ + ww.DepthFeet + cell) / cell);
            int nx = i1 - i0 + 1, nz = j1 - j0 + 1;
            if (nx < 2 || nz < 2) return;

            var e = new float[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                    e[j * nx + i] = Elevation(worldSeed, (i0 + i) * cell, (j0 + j) * cell, nc);

            float lo = e.Min(), hi = e.Max();
            int first = (int)MathF.Ceiling(lo / nc.ContourIntervalFt) * (int)nc.ContourIntervalFt;
            var pts = new List<Vec2>(4);
            for (int level = first; level <= hi; level += (int)nc.ContourIntervalFt)
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

        // ------------------------------------------------------------------ rivers (RIVERS - Perlin meander + Strahler tributaries)

        static float TrunkBaseX(uint worldSeed, int t, NoiseConfig nc)
        {
            float j = Hash.F(worldSeed, t, 811) - 0.5f;
            return t * nc.TrunkRiverSpacingMi.Sample(new Rng((uint)(worldSeed ^ (uint)t * 1001))) * Mile + j * 12f * Mile;
        }

        /// <summary>Centreline of a trunk river at a given northing. Always flowing south.</summary>
        public static float TrunkX(uint worldSeed, int t, float z, NoiseConfig nc)
        {
            float p1 = Hash.F(worldSeed, t, 821) * 6.2831853f;
            float p2 = Hash.F(worldSeed, t, 822) * 6.2831853f;
            float p3 = Hash.F(worldSeed, t, 823) * 6.2831853f;
            float x = TrunkBaseX(worldSeed, t, nc);
            x += nc.RiverMeanderBaseAmpFt * MathF.Sin(z / nc.RiverMeanderBaseWavelengthFt + p1);
            x += nc.RiverMeanderBaseAmpFt * 0.47f * MathF.Sin(z / (nc.RiverMeanderBaseWavelengthFt / 2.88f) + p2);
            x += nc.RiverMeanderBaseAmpFt * 0.17f * MathF.Sin(z / (nc.RiverMeanderBaseWavelengthFt / 7.6f) + p3);
            return x;
        }

        static float TrunkWidth(uint worldSeed, int t, float z)
        {
            float p1 = Hash.F(worldSeed, t, 824) * 6.2831853f;
            float p2 = Hash.F(worldSeed, t, 825) * 6.2831853f;
            return 58f + 20f * MathF.Sin(z / 17000f + p1) + 10f * MathF.Sin(z / 6000f + p2);
        }

        static int NearestTrunk(uint worldSeed, float x, NoiseConfig nc) => (int)MathF.Round(x / (nc.TrunkRiverSpacingMi.Sample(new Rng(worldSeed)) * Mile));

        static float TrunkDistance(uint worldSeed, float x, float z, NoiseConfig nc)
        {
            float best = float.MaxValue;
            int t0 = NearestTrunk(worldSeed, x, nc);
            for (int t = t0 - 1; t <= t0 + 1; t++)
                best = MathF.Min(best, MathF.Abs(x - TrunkX(worldSeed, t, z, nc)));
            return best;
        }

        /// <summary>Tributary shape: head (north), merge point (south, on the trunk), side.</summary>
        static void TributaryShape(uint worldSeed, int t, int k, NoiseConfig nc,
            out float zHead, out float zMerge, out float xHead, out float side)
        {
            var rng = new Rng(Hash.U32(worldSeed, t, k, 830));
            side = ((k & 1) == 0) ? -1f : 1f;
            zHead = k * nc.TributarySpacingMi.Sample(rng) * Mile + (rng.NextFloat() - 0.5f) * 2.4f * Mile;
            float len = nc.TributaryLengthBaseMi.Sample(rng) * Mile;
            zMerge = zHead - len;
            float gap = nc.TributaryGapFromTrunkMi.Sample(rng) * Mile;
            xHead = TrunkX(worldSeed, t, zHead, nc) + side * gap;
        }

        static Vec2 TributaryPoint(uint worldSeed, int t, int k, float u, NoiseConfig nc)
        {
            TributaryShape(worldSeed, t, k, nc, out float zHead, out float zMerge, out float xHead, out float side);
            float x1 = TrunkX(worldSeed, t, zMerge, nc);
            float x = xHead + (x1 - xHead) * u;
            float z = zHead + (zMerge - zHead) * u;
            float bow = MathF.Sin(u * MathF.PI) * nc.TributaryBowAmpFt * -side;
            return new Vec2(x + bow, z);
        }

        static void AddRivers(WorldWindow ww, uint worldSeed, NoiseConfig nc)
        {
            float minX = ww.MinX - 4000f, maxX = ww.MinX + ww.WidthFeet + 4000f;
            float minZ = ww.MinZ - 4000f, maxZ = ww.MinZ + ww.DepthFeet + 4000f;

            float trunkSpacing = nc.TrunkRiverSpacingMi.Sample(new Rng(worldSeed)) * Mile;
            int t0 = (int)MathF.Floor((minX - 20f * Mile) / trunkSpacing);
            int t1 = (int)MathF.Floor((maxX + 20f * Mile) / trunkSpacing);
            for (int t = t0; t <= t1; t++)
            {
                var run = new List<Vec2>();
                var runs = new List<List<Vec2>> { run };
                int m0 = (int)MathF.Floor(minZ / 240f), m1 = (int)MathF.Floor(maxZ / 240f);
                for (int m = m0; m <= m1; m++)
                {
                    float z = m * 240f;
                    float x = TrunkX(worldSeed, t, z, nc);
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
                // Bank trees
                for (int m = m0; m <= m1; m++)
                {
                    float z = m * 240f;
                    float x = TrunkX(worldSeed, t, z, nc);
                    if (x < minX || x > maxX) continue;
                    float w = TrunkWidth(worldSeed, t, z);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        ulong h = Hash.U64(worldSeed, t, m, side, 840);
                        if (h % 100 >= nc.RiverBankTreeChance * 100) continue;
                        float off = w / 2f + 14f + (float)((h >> 8) % 90);
                        float px = x + side * off + ((h >> 24) % 40) - 20f;
                        float pz = z + ((h >> 40) % 60) - 30f;
                        AddBankTree(ww, worldSeed, px, pz, nc);
                    }
                }
            }

            // --- tributaries ---
            float tribSpacing = nc.TributarySpacingMi.Sample(new Rng(worldSeed ^ 0x1234)) * Mile;
            for (int t = t0; t <= t1; t++)
            {
                int k0 = (int)MathF.Floor((minZ - 12f * Mile) / tribSpacing);
                int k1 = (int)MathF.Floor((maxZ + 4f * Mile) / tribSpacing);
                for (int k = k0; k <= k1; k++)
                {
                    TributaryShape(worldSeed, t, k, nc, out float zHead, out float zMerge, out _, out _);
                    if (zMerge > maxZ || zHead < minZ) continue;
                    var pts = new List<Vec2>();
                    for (int i = 0; i <= 20; i++)
                    {
                        float u = i / 20f;
                        var p = TributaryPoint(worldSeed, t, k, u, nc);
                        if (p.X < minX - 2000f || p.X > maxX + 2000f) continue;
                        pts.Add(p);
                    }
                    if (pts.Count < 2) continue;
                    var water = new WaterCourse { WidthFeet = nc.TributaryWidthFt };
                    foreach (var p in pts) water.Points.Add(new Vec2(p.X - ww.MinX, p.Z - ww.MinZ));
                    ww.Map.Waters.Add(water);

                    for (int i = 0; i <= 20; i += 2)
                    {
                        float u = i / 20f;
                        var p = TributaryPoint(worldSeed, t, k, u, nc);
                        float w = 7f + 19f * u;
                        for (int s2 = -1; s2 <= 1; s2 += 2)
                        {
                            ulong h = Hash.U64(worldSeed, t, k, i * 2 + (s2 > 0 ? 1 : 0), 841);
                            if (h % 100 >= nc.TributaryBankTreeChance * 100) continue;
                            float off = w / 2f + 12f + (float)((h >> 8) % 60);
                            AddBankTree(ww, worldSeed, p.X + s2 * off + ((h >> 24) % 30) - 15f, p.Z + ((h >> 40) % 40) - 20f, nc);
                        }
                    }
                }
            }
        }

        static void AddBankTree(WorldWindow ww, uint worldSeed, float x, float z, NoiseConfig nc)
        {
            if (x < ww.MinX || x > ww.MinX + ww.WidthFeet) return;
            if (z < ww.MinZ || z > ww.MinZ + ww.DepthFeet) return;
            if (NearRoad(worldSeed, x, z, 70f, nc)) return;
            if (NearRail(worldSeed, x, z, 50f, nc)) return;
            foreach (var t in ww.Towns)
                if (x > t.MinX - 120f && x < t.MaxX + 120f && z > t.MinZ - 120f && z < t.MaxZ + 120f) return;
            ww.Map.Trees.Add(new Vec2(x - ww.MinX, z - ww.MinZ));
        }

        // ------------------------------------------------------------------ rail (RAIL - fBm swell + spurs)

        /// <summary>How far the east–west rail of line m sits off its own section road.</summary>
        static float RailEWOffset(uint worldSeed, int m, NoiseConfig nc)
        {
            return nc.RailOffsetBaseFt.Sample(new Rng(Hash.U32(worldSeed, m, 851)));
        }

        /// <summary>How far the north–south rail of line n sits off its own section road.</summary>
        static float RailNSOffset(uint worldSeed, int n, NoiseConfig nc)
        {
            return nc.RailOffsetBaseFt.Sample(new Rng(Hash.U32(worldSeed, n, 861)));
        }

        /// <summary>The section line the east–west rail of line m belongs to.</summary>
        static float RailEWBase(uint worldSeed, int m, NoiseConfig nc) => m * nc.RailSpacingMi.Sample(new Rng(worldSeed)) * Mile + nc.RailPhaseSections * SectionFeet;

        /// <summary>The section line the north–south rail of line n belongs to.</summary>
        static float RailNSBase(uint worldSeed, int n, NoiseConfig nc) => n * nc.RailSpacingMi.Sample(new Rng(worldSeed ^ 0x5678)) * Mile + nc.RailPhaseSections * SectionFeet;

        /// <summary>The one-sided swell of the east–west rail of line m at x (fBm).</summary>
        static float RailEWSwell(uint worldSeed, int m, float x, NoiseConfig nc)
        {
            float amp = nc.RailSwellBaseAmpFt + nc.RailSwellAmpJitterFt * Hash.F(worldSeed, m, 852);
            float len = nc.RailSwellBaseWavelengthFt + nc.RailSwellWavelengthJitterFt * Hash.F(worldSeed, m, 853);
            float phase = Hash.F(worldSeed, m, 854) * 6.2831853f;
            return amp * (0.5f + 0.5f * MathF.Sin(x / len + phase));   // never negative
        }

        /// <summary>The one-sided swell of the north–south rail of line n at z (fBm).</summary>
        static float RailNSSwell(uint worldSeed, int n, float z, NoiseConfig nc)
        {
            float amp = nc.RailSwellBaseAmpFt + nc.RailSwellAmpJitterFt * Hash.F(worldSeed, n, 862);
            float len = nc.RailSwellBaseWavelengthFt + nc.RailSwellWavelengthJitterFt * Hash.F(worldSeed, n, 863);
            float phase = Hash.F(worldSeed, n, 864) * 6.2831853f;
            return amp * (0.5f + 0.5f * MathF.Sin(z / len + phase));   // never negative
        }

        static float RailEWZ(uint worldSeed, int m, float x, NoiseConfig nc) =>
            RailEWBase(worldSeed, m, nc) + RailEWOffset(worldSeed, m, nc) + RailEWSwell(worldSeed, m, x, nc);

        static float RailNSX(uint worldSeed, int n, float z, NoiseConfig nc) =>
            RailNSBase(worldSeed, n, nc) + RailNSOffset(worldSeed, n, nc) + RailNSSwell(worldSeed, n, z, nc);

        /// <summary>Distance from a point to the nearest rail line (approximate, curve ignored).</summary>
        static float NearestRailDistance(uint worldSeed, float x, float z, NoiseConfig nc)
        {
            float best = float.MaxValue;
            float railSpacing = nc.RailSpacingMi.Sample(new Rng(worldSeed)) * Mile;
            int m = (int)MathF.Round(z / railSpacing);
            for (int k = m - 1; k <= m + 1; k++)
                best = MathF.Min(best, MathF.Abs(z - (RailEWBase(worldSeed, k, nc) + RailEWOffset(worldSeed, k, nc))));
            int n = (int)MathF.Round(x / railSpacing);
            for (int k = n - 1; k <= n + 1; k++)
                best = MathF.Min(best, MathF.Abs(x - (RailNSBase(worldSeed, k, nc) + RailNSOffset(worldSeed, k, nc))));
            return best;
        }

        static bool NearRail(uint worldSeed, float x, float z, float pad, NoiseConfig nc)
        {
            float railSpacing = nc.RailSpacingMi.Sample(new Rng(worldSeed)) * Mile;
            int m = (int)MathF.Round(z / railSpacing);
            for (int k = m - 1; k <= m + 1; k++)
                if (MathF.Abs(z - (RailEWBase(worldSeed, k, nc) + RailEWOffset(worldSeed, k, nc))) < pad) return true;
            int n = (int)MathF.Round(x / railSpacing);
            for (int k = n - 1; k <= n + 1; k++)
                if (MathF.Abs(x - (RailNSBase(worldSeed, k, nc) + RailNSOffset(worldSeed, k, nc))) < pad) return true;
            return false;
        }

        static void AddRail(WorldWindow ww, uint worldSeed, NoiseConfig nc)
        {
            float minX = ww.MinX - 2000f, maxX = ww.MinX + ww.WidthFeet + 2000f;
            float minZ = ww.MinZ - 2000f, maxZ = ww.MinZ + ww.DepthFeet + 2000f;

            float railSpacing = nc.RailSpacingMi.Sample(new Rng(worldSeed)) * Mile;
            int m0 = (int)MathF.Floor((minZ - 12f * Mile) / railSpacing);
            int m1 = (int)MathF.Floor((maxZ + 12f * Mile) / railSpacing);
            for (int m = m0; m <= m1; m++)
            {
                var line = new RailLine();
                int i0 = (int)MathF.Floor(minX / 600f), i1 = (int)MathF.Floor(maxX / 600f);
                for (int i = i0; i <= i1; i++)
                {
                    float x = i * 600f;
                    line.Points.Add(new Vec2(x - ww.MinX, RailEWZ(worldSeed, m, x, nc) - ww.MinZ));
                }
                if (line.Points.Count >= 2) ww.Map.Rails.Add(line);
            }

            int n0 = (int)MathF.Floor((minX - 12f * Mile) / railSpacing);
            int n1 = (int)MathF.Floor((maxX + 12f * Mile) / railSpacing);
            for (int n = n0; n <= n1; n++)
            {
                var line = new RailLine();
                int j0 = (int)MathF.Floor(minZ / 600f), j1 = (int)MathF.Floor(maxZ / 600f);
                for (int j = j0; j <= j1; j++)
                {
                    float z = j * 600f;
                    line.Points.Add(new Vec2(RailNSX(worldSeed, n, z, nc) - ww.MinX, z - ww.MinZ));
                }
                if (line.Points.Count >= 2) ww.Map.Rails.Add(line);
            }
        }

        /// <summary>
        /// The branch spurs: a short north–south line off the rail, running to the town
        /// that it serves. It leaves the rail at a right angle and crosses the roads it
        /// meets square, so it obeys the same crossing rule as the main lines.
        /// </summary>
        static void AddRailSpurs(WorldWindow ww, List<WorldTown> towns, NoiseConfig nc)
        {
            foreach (var t in towns)
            {
                if (!t.Rail.HasRailSpur) continue;
                WorldTownPart? owner = null;
                foreach (var p in t.Parts)
                    if (p.Plan.RailTrackLocalX > 0f) { owner = p; break; }
                if (owner == null) continue;
                float spurX = owner.OriginX + owner.Plan.RailTrackLocalX;
                float nearZ = owner.Mirrored
                    ? owner.OriginZ + (66f - owner.Plan.RailTrackLocalZ0)
                    : owner.OriginZ + owner.Plan.RailTrackLocalZ0;
                float z0 = MathF.Min(t.Rail.RailZ, nearZ);
                float z1 = MathF.Max(t.Rail.RailZ, nearZ);
                if (z1 - z0 < 100f) continue;
                var line = new RailLine();
                int j0 = (int)MathF.Floor(z0 / 600f), j1 = (int)MathF.Floor(z1 / 600f);
                for (int j = j0; j <= j1; j++)
                {
                    float z = j * 600f;
                    line.Points.Add(new Vec2(spurX - ww.MinX, z - ww.MinZ));
                }
                if (line.Points.Count >= 2) ww.Map.Rails.Add(line);
            }
        }

        // ------------------------------------------------------------------ roads (ROADS - grid with exceptions)

        /// <summary>True when the given mile line is missing from the network (rare exception).</summary>
        static bool RoadLineAbsent(uint worldSeed, bool ew, int line, NoiseConfig nc)
        {
            ulong h = Hash.U64(worldSeed, line, ew ? 901 : 902);
            return h % 1000 < nc.RoadLineAbsentChance * 1000;
        }

        /// <summary>True when the given gravel mile line has been paved (rare exception).</summary>
        static bool RoadLinePaved(uint worldSeed, bool ew, int line, NoiseConfig nc)
        {
            ulong h = Hash.U64(worldSeed, line, ew ? 903 : 904);
            return h % 1000 < nc.RoadLinePavedChance * 1000;
        }

        /// <summary>The paved road corridors. They run on the section grid, every 10 miles.</summary>
        static float CorridorZ(uint worldSeed, int corridor, NoiseConfig nc) => corridor * nc.PavedSpacingMi * Mile;
        static float CorridorX(uint worldSeed, int column, NoiseConfig nc) => column * nc.PavedSpacingMi * Mile;

        static bool NearRoad(uint worldSeed, float x, float z, float pad, NoiseConfig nc)
        {
            int m = (int)MathF.Round(z / SectionFeet);
            for (int k = m - 1; k <= m + 1; k++)
            {
                if (RoadLineAbsent(worldSeed, true, k, nc)) continue;
                float half = RoadLinePaved(worldSeed, true, k, nc) ? 33f : 20f;
                if (MathF.Abs(z - k * SectionFeet) < half + pad) return true;
            }
            int n = (int)MathF.Round(x / SectionFeet);
            for (int k = n - 1; k <= n + 1; k++)
            {
                if (RoadLineAbsent(worldSeed, false, k, nc)) continue;
                float half = RoadLinePaved(worldSeed, false, k, nc) ? 33f : 20f;
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

        static void AddRoads(WorldWindow ww, uint worldSeed, List<WorldTown> towns, NoiseConfig nc)
        {
            var map = ww.Map;
            float minX = ww.MinX, maxX = ww.MinX + ww.WidthFeet;
            float minZ = ww.MinZ, maxZ = ww.MinZ + ww.DepthFeet;

            var clips = new List<(float x0, float z0, float x1, float z1, float highwayZ)>();
            foreach (var t in towns)
                foreach (var p in t.Parts)
                    foreach (var r in p.RoadClip)
                        clips.Add((r.x0, r.z0, r.x1, r.z1, t.HighwayZ));

            // --- paved corridors ---
            float pavedSpacing = nc.PavedSpacingMi * Mile;
            int c0 = (int)MathF.Floor(minZ / pavedSpacing);
            int c1 = (int)MathF.Floor(maxZ / pavedSpacing);
            for (int c = c0; c <= c1; c++)
            {
                float z = CorridorZ(worldSeed, c, nc);
                var spans = new List<(float a, float b)> { (minX, maxX) };
                foreach (var b in clips)
                {
                    if (MathF.Abs(b.highwayZ - z) < 200f) continue;
                    if (b.z1 >= z - 33f && b.z0 <= z + 33f)
                        spans = Subtract(spans, b.x0, b.x1);
                }
                foreach (var s in spans)
                {
                    if (s.b - s.a < 1f) continue;
                    map.Roads.Add(new MapRect(s.a - ww.MinX, z - 33f - ww.MinZ, s.b - s.a, 66f));
                    map.RoadDashed.Add(true);
                    map.RoadPaved.Add(true);
                }
            }

            int d0 = (int)MathF.Floor(minX / pavedSpacing);
            int d1 = (int)MathF.Floor(maxX / pavedSpacing);
            for (int c = d0; c <= d1; c++)
            {
                float x = CorridorX(worldSeed, c, nc);
                var spans = new List<(float a, float b)> { (minZ, maxZ) };
                foreach (var b in clips)
                {
                    if (b.x1 >= x - 33f && b.x0 <= x + 33f)
                        spans = Subtract(spans, b.z0, b.z1);
                }
                foreach (var s in spans)
                {
                    if (s.b - s.a < 1f) continue;
                    map.Roads.Add(new MapRect(x - 33f - ww.MinX, s.a - ww.MinZ, 66f, s.b - s.a));
                    map.RoadDashed.Add(false);
                    map.RoadPaved.Add(true);
                }
            }

            // --- gravel section roads ---
            int ix0 = (int)MathF.Ceiling(minX / SectionFeet);
            int ix1 = (int)MathF.Floor(maxX / SectionFeet);
            for (int ix = ix0; ix <= ix1; ix++)
            {
                if (RoadLineAbsent(worldSeed, false, ix, nc)) continue;
                float x = ix * SectionFeet;
                bool paved = RoadLinePaved(worldSeed, false, ix, nc);
                float half = paved ? 33f : 20f;
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
                if (RoadLineAbsent(worldSeed, true, iz, nc)) continue;
                float z = iz * SectionFeet;
                bool paved = RoadLinePaved(worldSeed, true, iz, nc);
                float half = paved ? 33f : 20f;
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

        // ------------------------------------------------------------------ town field (TOWNS - rail proximity + Dirichlet layout/class)

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

        static TownSite RawSite(uint worldSeed, int corridor, int slot, NoiseConfig nc)
        {
            ulong h = Hash.U64(worldSeed, corridor, slot, 11);
            var rng = new Rng((uint)h);
            float highwayZ = CorridorZ(worldSeed, corridor, nc);
            float slotSpacing = nc.PavedSpacingMi * Mile;
            float rawX = (slot + 0.5f) * slotSpacing + rng.Range(-nc.TownSlotJitterFraction, nc.TownSlotJitterFraction) * slotSpacing;
            float centerX = (MathF.Floor(rawX / SectionFeet) + 0.5f) * SectionFeet;

            bool nearRail = NearestRailDistance(worldSeed, centerX, highwayZ, nc) < nc.TownRailProximityMi * Mile;
            TownClass cls = TownClass.None;
            WorldTownLayout layout = WorldTownLayout.North;
            if (nearRail)
            {
                var layoutWeights = nc.TownLayoutWeights.SampleDirichlet(rng);
                var classWeights = nc.TownClassWeights.SampleDirichlet(rng);
                float layoutRoll = rng.NextFloat();
                float classRoll = rng.NextFloat();
                float cum = 0;
                for (int i = 0; i < layoutWeights.Length; i++)
                {
                    cum += layoutWeights[i];
                    if (layoutRoll <= cum) { layout = (WorldTownLayout)i; break; }
                }
                cum = 0;
                for (int i = 0; i < classWeights.Length; i++)
                {
                    cum += classWeights[i];
                    if (classRoll <= cum) { cls = (TownClass)(i + 1); break; }
                }
            }

            if (cls != TownClass.None && TrunkDistance(worldSeed, centerX, highwayZ, nc) < nc.RiverVetoDistanceFt)
                cls = TownClass.None;

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

        static TownSite? VisibleSite(uint worldSeed, int corridor, int slot, NoiseConfig nc)
        {
            var s = RawSite(worldSeed, corridor, slot, nc);
            if (s.Class == TownClass.None) return null;
            int neighborRadius = Math.Max(1, (int)MathF.Ceiling(nc.TownMinSpacingMi / nc.PavedSpacingMi));
            for (int dk = -neighborRadius; dk <= neighborRadius; dk++)
            {
                for (int di = -neighborRadius; di <= neighborRadius; di++)
                {
                    if (dk == 0 && di == 0) continue;
                    var n = RawSite(worldSeed, corridor + dk, slot + di, nc);
                    if (n.Class == TownClass.None) continue;
                    float dx = n.CenterX - s.CenterX, dz = n.HighwayZ - s.HighwayZ;
                    float minSpacing = nc.TownMinSpacingMi * Mile;
                    if (dx * dx + dz * dz < minSpacing * minSpacing && Beats(n, s)) return null;
                }
            }
            return s;
        }

        static List<TownSite> TownSitesNear(uint worldSeed, float minX, float maxX, float minZ, float maxZ, NoiseConfig nc)
        {
            var result = new List<TownSite>();
            float pavedSpacing = nc.PavedSpacingMi * Mile;
            int k0 = (int)MathF.Floor((minZ - 16000f) / pavedSpacing);
            int k1 = (int)MathF.Floor((maxZ + 16000f) / pavedSpacing);
            int i0 = (int)MathF.Floor((minX - 16000f) / pavedSpacing);
            int i1 = (int)MathF.Floor((maxX + 16000f) / pavedSpacing);
            for (int k = k0; k <= k1; k++)
                for (int i = i0; i <= i1; i++)
                {
                    var site = VisibleSite(worldSeed, k, i, nc);
                    if (site == null) continue;
                    if (site.CenterX < minX - 20000f || site.CenterX > maxX + 20000f) continue;
                    result.Add(site);
                }
            return result;
        }

        static List<TownSite> CollectTownSites(uint worldSeed, WorldWindow ww, NoiseConfig nc)
        {
            var sites = TownSitesNear(worldSeed, ww.MinX - 20000f, ww.MinX + ww.WidthFeet + 20000f,
                ww.MinZ - 16000f, ww.MinZ + ww.DepthFeet + 16000f, nc);
            return sites.Where(s => s.CenterX > ww.MinX - 22000f && s.CenterX < ww.MinX + ww.WidthFeet + 22000f
                && s.HighwayZ > ww.MinZ - 8000f && s.HighwayZ < ww.MinZ + ww.DepthFeet + 8000f).ToList();
        }

        /// <summary>Debug: how the towns' corridors sit against the rail lattice.</summary>
        public static void ProbeRail(uint worldSeed)
        {
            var nc = new NoiseConfig();
            var counts = new Dictionary<string, int>();
            foreach (var site in TownSitesNear(worldSeed, 0f, 330f * Mile, 0f, 40f * Mile, nc))
            {
                var ctx = TownRailContext(worldSeed, site, nc);
                int m = (int)MathF.Round(site.HighwayZ / (nc.RailSpacingMi.Sample(new Rng(worldSeed)) * Mile));
                float best = float.MaxValue;
                for (int k = m - 1; k <= m + 1; k++)
                    best = MathF.Min(best, MathF.Abs(site.HighwayZ - RailEWZ(worldSeed, k, site.CenterX, nc)));
                string key = $"corridor z={site.HighwayZ / Mile:0} mi  best={best / Mile:0.00} mi  spur={ctx.HasRailSpur}";
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
            foreach (var kv in counts.OrderBy(k => k.Key)) Console.WriteLine($"{kv.Value,3} x  {kv.Key}");

            foreach (var site in TownSitesNear(worldSeed, 0f, 330f * Mile, 0f, 40f * Mile, nc))
            {
                var ctx = TownRailContext(worldSeed, site, nc);
                if (!ctx.HasRailSpur) continue;
                var t = MaterializeTown(worldSeed, site, nc);
                foreach (var part in t.Parts)
                {
                    if (part.Plan.RailTrackLocalX <= 0f) continue;
                    float tx = part.Plan.RailTrackLocalX;
                    var near = part.Plan.Roads
                        .Where(r => r.W < r.H && MathF.Abs(r.X - tx) < 200f)
                        .Select(r => (r.X, r.W))
                        .OrderBy(r => MathF.Abs(r.X - tx))
                        .ToList();
                    string streets = string.Join(", ", near.Take(3).Select(r => $"{r.X:0}+{r.W:0}"));
                    var ev = part.Plan.Outbuildings.FirstOrDefault(o => o.Kind == OutbuildingKind.GrainElevator);
                    int shops = part.Plan.Outbuildings.Count(o => o.Kind == OutbuildingKind.Storefront
                        && MathF.Abs(o.Center.X - tx) < 120f);
                    Console.WriteLine($"town {t.Name,-16} track x={tx:0} world={part.OriginX + tx:0}  elevator x={(ev == null ? -1f : ev.Center.X):0}  rail-side works={shops}  streets {streets}");
                }
            }
        }

        /// <summary>
        /// Where the branch spur comes in, if the town has one.
        /// Iowa's branch lines ran from a main line to towns that were not on it.
        /// The spur leaves the rail at a right angle and runs to the town, where the grain
        /// elevator stands at its end.
        /// </summary>
        static TownContext TownRailContext(uint worldSeed, TownSite site, NoiseConfig nc)
        {
            float railSpacing = nc.RailSpacingMi.Sample(new Rng(worldSeed)) * Mile;
            int m = (int)MathF.Round(site.HighwayZ / railSpacing);
            float bestZ = 0f, bestD = float.MaxValue;
            for (int k = m - 1; k <= m + 1; k++)
            {
                float rz = RailEWZ(worldSeed, k, site.CenterX, nc);
                float d = MathF.Abs(site.HighwayZ - rz);
                if (d < bestD) { bestD = d; bestZ = rz; }
            }
            if (bestD > nc.RailSpurMaxDistanceMi * Mile) return TownContext.None;
            float off = nc.RailSpurOffsetBaseFt.Sample(new Rng(Hash.U32(worldSeed, site.Corridor, site.Slot, 71)));
            if (Hash.F(worldSeed, site.Corridor, site.Slot, 72) < nc.RailSpurSideChance) off = -off;
            return new TownContext { HasRailSpur = true, RailZ = bestZ, SpurOffsetFt = off };
        }

        static WorldTown MaterializeTown(uint worldSeed, TownSite site, NoiseConfig nc)
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
            town.Rail = TownRailContext(worldSeed, site, nc);

            void AddPart(uint seed, TownClass cls, bool mirrored)
            {
                var ctx = town.Rail;
                if (ctx.HasRailSpur)
                {
                    bool railIsNorth = town.Rail.RailZ > site.HighwayZ;
                    bool ownsSpur = site.Layout == WorldTownLayout.Both
                        ? railIsNorth == !mirrored
                        : true;
                    ctx = new TownContext
                    {
                        HasRailSpur = true,
                        RailZ = town.Rail.RailZ,
                        SpurOffsetFt = town.Rail.SpurOffsetFt,
                        OwnsSpur = ownsSpur,
                    };
                }
                var plan = SmallTownGenerator.Generate(seed, SizeName(cls), detail: false, ctx: ctx, config: null);
                float ox = site.CenterX - plan.WidthFeet / 2f;
                float oz = site.HighwayZ - 66f / 2f;
                var part = new WorldTownPart { Plan = plan, Mirrored = mirrored, OriginX = ox, OriginZ = oz };

                float bMinX = ox + plan.BuiltMinX, bMaxX = ox + plan.BuiltMaxX;
                float bMinZ, bMaxZ;
                if (mirrored)
                {
                    bMinZ = oz + 66f - plan.BuiltMaxZ;
                    bMaxZ = oz + 66f - plan.BuiltMinZ;
                }
                else
                {
                    bMinZ = oz + plan.BuiltMinZ;
                    bMaxZ = oz + plan.BuiltMaxZ;
                }
                part.BlockMinX = bMinX; part.BlockMaxX = bMaxX;
                part.BlockMinZ = bMinZ; part.BlockMaxZ = bMaxZ;

                if (mirrored)
                {
                    float z0 = oz + 66f - plan.LoopMaxZ;
                    float z1 = oz + 66f - plan.LoopMinZ;
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

            float WZ(float localZ) => mir ? oz + (66f - localZ) : oz + localZ;
            bool Keep(float localZ) => localZ >= 66f - 1f;

            for (int i = 0; i < town.Roads.Count; i++)
            {
                var r = town.Roads[i];
                if (IsTownHighway(r)) continue;
                if (!Keep(r.Z)) continue;
                float z0 = WZ(r.Z), z1 = WZ(r.Z + r.H);
                var placed = new MapRect(r.X + ox, MathF.Min(z0, z1), r.W, MathF.Abs(z1 - z0));
                map.Roads.Add(placed);
                map.RoadDashed.Add(i < town.RoadDashed.Count && town.RoadDashed[i]);
                map.RoadPaved.Add(true);
                if (town.RoadMayEndFree.Any(f =>
                    MathF.Abs(f.X - r.X) < 0.5f && MathF.Abs(f.Z - r.Z) < 0.5f &&
                    MathF.Abs(f.W - r.W) < 0.5f && MathF.Abs(f.H - r.H) < 0.5f))
                    map.RoadMayEndFree.Add(placed);
            }
            foreach (var p in town.RoadPaths)
            {
                var path = new RoadPath { WidthFeet = p.WidthFeet, Paved = p.Paved };
                foreach (var q in p.Points)
                    path.Points.Add(new Vec2(q.X + ox, WZ(q.Z)));
                if (path.Points.Count >= 2) map.RoadPaths.Add(path);
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
            foreach (var m in town.Medians)
            {
                if (!Keep(m.Z)) continue;
                float z0 = WZ(m.Z), z1 = WZ(m.Z + m.H);
                map.Medians.Add(new MapRect(m.X + ox, MathF.Min(z0, z1), m.W, MathF.Abs(z1 - z0)));
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
                var plan = mir ? h.Plan.FlipZ().OffsetCopy(0, 66f) : h.Plan;
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
                    RotateDeg = mir ? -o.RotateDeg : o.RotateDeg,
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
            MathF.Abs(r.Z) < 0.5f && MathF.Abs(r.H - 66f) < 0.5f;

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

        // ------------------------------------------------------------------ farmsteads (FARMS - per-section with jitter)

        static int FarmCountFor(uint worldSeed, int sx, int sz, NoiseConfig nc)
        {
            var rng = new Rng(Hash.U32(worldSeed, sx, sz, 31));
            float r = rng.NextFloat();
            // FarmDensity is the mean farmsteads per 640-acre section. Draw a
            // deterministic Poisson count; avoid the old thresholds, which sent
            // almost every section to the maximum count at low density settings.
            float lambda = MathF.Max(0f, nc.FarmDensity);
            float probability = MathF.Exp(-lambda);
            float cumulative = probability;
            int count = 0;
            while (r >= cumulative && count < nc.FarmsPerSectionMax)
            {
                count++;
                probability *= lambda / count;
                cumulative += probability;
            }
            return count;
        }

        static void AddFarms(WorldWindow ww, uint worldSeed, NoiseConfig nc)
        {
            int sx0 = (int)MathF.Floor(ww.MinX / SectionFeet) - 1;
            int sx1 = (int)MathF.Floor((ww.MinX + ww.WidthFeet) / SectionFeet) + 1;
            int sz0 = (int)MathF.Floor(ww.MinZ / SectionFeet) - 1;
            int sz1 = (int)MathF.Floor((ww.MinZ + ww.DepthFeet) / SectionFeet) + 1;

            for (int sx = sx0; sx <= sx1; sx++)
            {
                for (int sz = sz0; sz <= sz1; sz++)
                {
                    int count = Math.Min(FarmCountFor(worldSeed, sx, sz, nc), nc.FarmsPerSectionMax);
                    var bboxes = new List<(float x0, float z0, float x1, float z1)>();
                    for (int idx = 0; idx < count; idx++)
                    {
                        uint farmSeed = Hash.U32(worldSeed, sx, sz, 32, idx);
                        var rng = new Rng(farmSeed);
                        var plan = MapGenerator.RuralLot(farmSeed);
                        int side = rng.RangeInt(0, 4);
                        float lotW = plan.WidthFeet, lotD = plan.DepthFeet;
                        float span = SectionFeet - 2f * nc.FarmMarginFt;

                        float ox, oz;
                        float perpJitter = nc.FarmSectionJitterFt.Sample(rng);
                        float alongJitter = nc.FarmAlongJitterFt.Sample(rng);

                        if (side == 0 || side == 1)
                        {
                            float along = sx * SectionFeet + nc.FarmMarginFt + alongJitter + rng.Range(0f, MathF.Max(0f, span - lotW));
                            ox = along;
                            oz = side == 0 ? sz * SectionFeet + 20f + perpJitter : (sz + 1) * SectionFeet - 20f - perpJitter;
                        }
                        else
                        {
                            float along = sz * SectionFeet + nc.FarmMarginFt + alongJitter + rng.Range(0f, MathF.Max(0f, span - lotW));
                            ox = side == 2 ? sx * SectionFeet + 20f + perpJitter : (sx + 1) * SectionFeet - 20f - perpJitter;
                            oz = side == 2 ? along + lotW : along;
                        }

                        var b = RuralBounds(plan, side, ox, oz);
                        if (b.maxX - b.minX <= 1f || b.maxZ - b.minZ <= 1f) continue;
                        if (IntersectsAnyTown(worldSeed, b, 240f, nc)) continue;
                        if (NearWater(worldSeed, b, nc)) continue;
                        if (NearRail(worldSeed, (b.minX + b.maxX) / 2f, (b.minZ + b.maxZ) / 2f, 220f, nc)) continue;
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

        static bool IntersectsAnyTown(uint worldSeed, (float minX, float minZ, float maxX, float maxZ) b, float pad, NoiseConfig nc)
        {
            foreach (var site in TownSitesNear(worldSeed,
                         b.minX - pad, b.maxX + pad, b.minZ - pad, b.maxZ + pad, nc))
            {
                float hw = TownHalfWidth(site.Class);
                float north = TownNorthExtent(site.Class);
                float x0 = site.CenterX - hw, x1 = site.CenterX + hw;
                float z0 = site.HighwayZ - 300f, z1 = site.HighwayZ + north;
                if (b.minX < x1 + pad && b.maxX > x0 - pad && b.minZ < z1 + pad && b.maxZ > z0 - pad)
                    return true;
            }
            return false;
        }

        const float TownSouthExtent = 300f;

        static float TownHalfWidth(TownClass c) => c switch
        {
            TownClass.Town => 1450f,
            TownClass.Village => 1050f,
            TownClass.Hamlet => 620f,
            TownClass.Crossroads => 330f,
            _ => 330f,
        };

        static float TownNorthExtent(TownClass c) => c switch
        {
            TownClass.Town => 1900f,
            TownClass.Village => 1400f,
            TownClass.Hamlet => 950f,
            TownClass.Crossroads => 650f,
            _ => 650f,
        };

        static bool NearWater(uint worldSeed, (float minX, float minZ, float maxX, float maxZ) b, NoiseConfig nc)
        {
            float cx = (b.minX + b.maxX) / 2f, cz = (b.minZ + b.maxZ) / 2f;
            int t = NearestTrunk(worldSeed, cx, nc);
            for (int k = t - 1; k <= t + 1; k++)
            {
                for (float z = b.minZ - 400f; z <= b.maxZ + 400f; z += 220f)
                {
                    float rx = TrunkX(worldSeed, k, z, nc);
                    if (rx > b.minX - 260f && rx < b.maxX + 260f) return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ field entrances

        static void AddFieldEntrances(WorldWindow ww, uint worldSeed)
        {
            float minX = ww.MinX - 2000f, maxX = ww.MinX + ww.WidthFeet + 2000f;
            float minZ = ww.MinZ - 2000f, maxZ = ww.MinZ + ww.DepthFeet + 2000f;
            int sx0 = (int)MathF.Floor(minX / SectionFeet) - 1;
            int sx1 = (int)MathF.Floor(maxX / SectionFeet) + 1;
            int sz0 = (int)MathF.Floor(minZ / SectionFeet) - 1;
            int sz1 = (int)MathF.Floor(maxZ / SectionFeet) + 1;
            float half = SectionFeet / 2f;

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
                            int count = 1 + (int)(h % 2);
                            for (int k = 0; k < count; k++)
                            {
                                ulong h2 = Hash.U64(worldSeed, sx, sz, qi, 62 + k);
                                float t = (float)((h2 >> 8) % 1000000UL) / 1000000f;
                                float len = 95f + (float)((h2 >> 24) % 75);
                                bool onNS = (h2 & 1UL) == 0UL;
                                float ax, az, bx, bz;
                                float roadHalf;
                                const float setback = 110f;
                                if (onNS)
                                {
                                    int line = qx == 0 ? sx : sx + 1;
                                    if (RoadLineAbsent(worldSeed, false, line, new NoiseConfig())) { continue; }
                                    float X = line * SectionFeet;
                                    float qz0 = sz * SectionFeet + qz * half;
                                    float zz = Math.Clamp(qz0 + t * half, qz0 + setback, qz0 + half - setback);
                                    float dir = qx == 0 ? 1f : -1f;
                                    roadHalf = RoadLinePaved(worldSeed, false, line, new NoiseConfig()) ? 33f : 20f;
                                    ax = X + dir * (roadHalf - 4f); az = zz;
                                    bx = X + dir * (roadHalf + len); bz = zz;
                                }
                                else
                                {
                                    int line = qz == 0 ? sz : sz + 1;
                                    if (RoadLineAbsent(worldSeed, true, line, new NoiseConfig())) { continue; }
                                    float Z = line * SectionFeet;
                                    float qx0 = sx * SectionFeet + qx * half;
                                    float xx = Math.Clamp(qx0 + t * half, qx0 + setback, qx0 + half - setback);
                                    float dir = qz == 0 ? 1f : -1f;
                                    roadHalf = RoadLinePaved(worldSeed, true, line, new NoiseConfig()) ? 33f : 20f;
                                    ax = xx; az = Z + dir * (roadHalf - 4f);
                                    bx = xx; bz = Z + dir * (roadHalf + len);
                                }
                                if (EntranceBlocked(ww, ax, az, bx, bz)) { continue; }
                                ww.Map.Driveways.Add(new Driveway
                                {
                                    Surface = "dirt",
                                    WidthFeet = 26f,
                                    Points = { new Vec2(ax - ww.MinX, az - ww.MinZ), new Vec2(bx - ww.MinX, bz - ww.MinZ) },
                                });
                            }
                        }
                    }
                }
            }
        }

        static bool EntranceBlocked(WorldWindow ww, float ax, float az, float bx, float bz)
        {
            foreach (var f in ww.Farms)
                if (SegmentNearRect(ax, az, bx, bz, f.MinX - 90f, f.MinZ - 90f, f.MaxX + 90f, f.MaxZ + 90f)) return true;
            foreach (var t in ww.Towns)
                if (SegmentNearRect(ax, az, bx, bz, t.MinX - 60f, t.MinZ - 60f, t.MaxX + 60f, t.MaxZ + 60f)) return true;
            return false;
        }

        static bool SegmentNearRect(float ax, float az, float bx, float bz, float x0, float z0, float x1, float z1)
        {
            if (ax > x0 && ax < x1 && az > z0 && az < z1) return true;
            if (bx > x0 && bx < x1 && bz > z0 && bz < z1) return true;
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
                map.Houses.Add(new PositionedHouse { Plan = hp.OffsetCopy(ox, oz), MirroredZ = side == 1 });
            }
            foreach (var o in plan.Outbuildings)
            {
                var c = FarmPoint(o.Center, side, ox, oz);
                bool swap = side >= 2;
                map.Outbuildings.Add(new Outbuilding
                {
                    Kind = o.Kind, Center = c,
                    WidthFeet = swap ? o.DepthFeet : o.WidthFeet,
                    DepthFeet = swap ? o.WidthFeet : o.DepthFeet,
                    DiameterFeet = o.DiameterFeet, Label = o.Label,
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
            float pavedSpacing = 10f * Mile; // TODO: use nc
            int c0 = (int)MathF.Floor((ww.MinZ - 2000f) / pavedSpacing);
            int c1 = (int)MathF.Floor((ww.MinZ + ww.DepthFeet + 2000f) / pavedSpacing);
            for (int c = c0; c <= c1; c++)
            {
                float z = c * pavedSpacing;
                map.Labels.Add(new MapLabel
                {
                    Position = new Vec2(42f, z - ww.MinZ),
                    Text = HighwayName(c),
                    Large = true,
                    Anchor = "start",
                });
            }
        }

        static string[] HighwayNames = { "HWY 12", "HWY 7", "HWY 31", "HWY 46", "HWY 22", "HWY 38", "HWY 5", "HWY 17" };
        static string HighwayName(int corridor) => HighwayNames[(int)((uint)(corridor * 37 + 11) % (uint)HighwayNames.Length)];

        // =====================================================================
        // VERIFICATION (unchanged)
        // =====================================================================

        public sealed class Check { public string Name { get; set; } = ""; public bool Pass { get; set; } public string Detail { get; set; } = ""; }

        public static List<Check> Verify(uint worldSeed)
        {
            var nc = new NoiseConfig();
            var checks = new List<Check>();

            var a = Generate(worldSeed, 0f, 0f, CellFeet * 3f, CellFeet * 3f, null, nc);
            var b = Generate(worldSeed, 0f, 0f, CellFeet * 3f, CellFeet * 3f, null, nc);
            string sa = SvgMapRenderer.Render(a.Map, bare: true, targetWidth: 700f);
            string sb = SvgMapRenderer.Render(b.Map, bare: true, targetWidth: 700f);
            checks.Add(new Check { Name = "same window twice is byte-identical", Pass = sa == sb, Detail = $"{sa.Length} bytes" });

            var farA = Generate(worldSeed, 0f, 100f * Mile, CellFeet * 3f, CellFeet * 3f, null, nc);
            var farB = Generate(worldSeed, 0f, 100f * Mile, CellFeet * 3f, CellFeet * 3f, null, nc);
            checks.Add(new Check { Name = "100 miles out is still reproducible", Pass = SvgMapRenderer.Render(farA.Map, true, 500f) == SvgMapRenderer.Render(farB.Map, true, 500f), Detail = $"center 0, {100f:0} mi · {farA.Towns.Count} towns · {farA.FarmsInWindow} farms" });

            var left = Generate(worldSeed, 0f, 0f, 6f * Mile, 6f * Mile, null, nc);
            var right = Generate(worldSeed, 2f * Mile, 0f, 6f * Mile, 6f * Mile, null, nc);
            float ox0 = -1f * Mile, ox1 = 3f * Mile, oz0 = -3f * Mile, oz1 = 3f * Mile;
            var ea = CanonicalElements(left, ox0, oz0, ox1, oz1);
            var eb = CanonicalElements(right, ox0, oz0, ox1, oz1);
            const float SamePlace = 0.5f;
            bool overlapSame = ea.Count == eb.Count;
            float worstDrift = 0f;
            if (overlapSame)
            {
                for (int i = 0; i < ea.Count; i++)
                {
                    var pa = ParseElement(ea[i]);
                    var pb = ParseElement(eb[i]);
                    if (pa.kind != pb.kind) { overlapSame = false; break; }
                    float d = MathF.Max(MathF.Abs(pa.x - pb.x), MathF.Abs(pa.z - pb.z));
                    worstDrift = MathF.Max(worstDrift, d);
                    if (d > SamePlace) { overlapSame = false; break; }
                }
            }
            checks.Add(new Check { Name = "overlapping windows agree on every building/tree", Pass = overlapSame, Detail = $"{ea.Count} shared elements" + (overlapSame ? $", max drift {worstDrift:0.00} ft" : $" vs {eb.Count}") });

            var cell = GenerateCell(worldSeed, 0, 0, null, nc);
            var super = Generate(worldSeed, CellFeet * 1.5f, CellFeet * 1.5f, CellFeet * 3f, CellFeet * 3f, null, nc);
            var ca = CanonicalElements(cell, cell.MinX, cell.MinZ, cell.MinX + CellFeet, cell.MinZ + CellFeet);
            var cb = CanonicalElements(super, cell.MinX, cell.MinZ, cell.MinX + CellFeet, cell.MinZ + CellFeet);
            checks.Add(new Check { Name = "3-mile cell equals the same land in the 9-mile view", Pass = ca.SequenceEqual(cb), Detail = $"{ca.Count} elements in the cell" });

            var ladder = new List<(float x, float z, TownClass cls)>();
            for (int c = 0; c <= 3; c++)
                for (int i = -2; i <= 30; i++)
                {
                    var s = VisibleSite(worldSeed, c, i, nc);
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
            checks.Add(new Check { Name = "town ladder: 20-mile minimum spacing, big town rare", Pass = ladder.Count < 2 || minGap >= nc.TownMinSpacingMi * Mile - 1f, Detail = $"{ladder.Count} towns / 330 mi · min gap {(minGap / Mile):0.##} mi · {crossroads} crossroads, {hamlets} hamlet, {villages} village, {townsBig} town" });

            int nearRail = 0;
            foreach (var t in ladder)
                if (NearestRailDistance(worldSeed, t.x, t.z, nc) < 16f * Mile) nearRail++;
            checks.Add(new Check { Name = "towns tend to be near rail lines", Pass = ladder.Count == 0 || nearRail * 3 >= ladder.Count, Detail = $"{nearRail} of {ladder.Count} within 16 mi of rail" });

            var census = Generate(worldSeed, 0f, 0f, 12f * Mile, 12f * Mile, null, nc);
            if (Environment.GetEnvironmentVariable("SPAWNGUIDE_PROBE") == "1")
                foreach (var t in census.Towns) Console.Error.WriteLine($"PROBE census town {t.Name} cx={t.CenterX:0} hwZ={t.HighwayZ:0} minX={t.MinX:0} maxX={t.MaxX:0} minZ={t.MinZ:0} maxZ={t.MaxZ:0} layout={t.Layout} houses={t.Houses}");
            int overlaps = CountBuildingOverlaps(census.Map, out string overlapInfo);
            checks.Add(new Check { Name = "no building overlaps another (towns, farms, outbuildings)", Pass = overlaps == 0, Detail = $"{census.Map.Houses.Count} houses · {census.Map.Outbuildings.Count} outbuildings · {overlaps} overlaps{overlapInfo}" });

            checks.Add(new Check { Name = "rivers run south and tributaries merge into them", Pass = TrunkDistance(worldSeed, 0f, 0f, nc) < float.MaxValue, Detail = $"trunk rivers every {nc.TrunkRiverSpacingMi.Sample(new Rng(worldSeed)):0} mi, tributary heads every {nc.TributarySpacingMi.Sample(new Rng(worldSeed ^ 0x1234)):0} mi" });

            checks.Add(new Check { Name = "rail crosses roads square, never almost parallel", Pass = RailCrossingAngles(worldSeed, out string worst, nc), Detail = worst });

            int withSpur = 0, withElevator = 0, townsNear = 0;
            foreach (var site in TownSitesNear(worldSeed, 0f, 330f * Mile, 0f, 40f * Mile, nc))
            {
                townsNear++;
                var ctx = TownRailContext(worldSeed, site, nc);
                if (!ctx.HasRailSpur) continue;
                withSpur++;
                var t = MaterializeTown(worldSeed, site, nc);
                foreach (var p in t.Parts)
                    if (p.Plan.Outbuildings.Any(o => o.Kind == OutbuildingKind.GrainElevator)) { withElevator++; break; }
            }
            checks.Add(new Check { Name = "rail towns get a branch spur and an elevator on it", Pass = withSpur > 0 && withElevator == withSpur, Detail = $"{withSpur} of {townsNear} towns on a branch spur, {withElevator} with the elevator on it" });

            int spurHits = 0, spurChecked = 0;
            foreach (var site in TownSitesNear(worldSeed, 0f, 330f * Mile, 0f, 40f * Mile, nc))
            {
                var ctx = TownRailContext(worldSeed, site, nc);
                if (!ctx.HasRailSpur) continue;
                var t = MaterializeTown(worldSeed, site, nc);
                foreach (var part in t.Parts)
                {
                    if (part.Plan.RailTrackLocalX <= 0f) continue;
                    spurChecked++;
                    float sx = part.Plan.RailTrackLocalX;
                    float z0 = part.Plan.RailTrackLocalZ0, z1 = part.Plan.RailTrackLocalZ1;
                    foreach (var h in part.Plan.Houses)
                    {
                        float hx0 = h.Plan.Footprint.Min(p => p.X), hx1 = h.Plan.Footprint.Max(p => p.X);
                        float hz = h.Plan.Footprint.Select(p => p.Z).Average();
                        if (hx0 - 6f < sx && hx1 + 6f > sx && hz > z0 - 30f && hz < z1 + 30f) spurHits++;
                    }
                }
            }
            checks.Add(new Check { Name = "the branch spur runs clear of the houses", Pass = spurHits == 0, Detail = $"{spurChecked} spur corridors, {spurHits} house fronts in the way" });

            int onPaved = 0, townsChecked = 0, dangling = 0;
            var danglingDetail = new List<string>();
            foreach (var site in TownSitesNear(worldSeed, 0f, 330f * Mile, 0f, 40f * Mile, nc))
            {
                townsChecked++;
                float pavedSpacing = nc.PavedSpacingMi * Mile;
                bool paved = MathF.Abs(site.HighwayZ / pavedSpacing - MathF.Round(site.HighwayZ / pavedSpacing)) < 1e-3f;
                if (paved) onPaved++;
                var town = MaterializeTown(worldSeed, site, nc);
                dangling += CountDanglingRoads(town, danglingDetail);
            }
            checks.Add(new Check { Name = "every town sits on a paved road", Pass = townsChecked == 0 || onPaved == townsChecked, Detail = $"{onPaved} of {townsChecked} towns on the paved grid" });
            checks.Add(new Check { Name = "no town street ends without an exit", Pass = dangling == 0, Detail = $"{dangling} dangling street ends" + (danglingDetail.Count > 0 ? " · " + string.Join("; ", danglingDetail.Take(4)) : "") });

            return checks;
        }

        static bool RailCrossingAngles(uint worldSeed, out string detail, NoiseConfig nc)
        {
            detail = "";
            float worst = 0f;
            int crossings = 0;
            float railSpacing = nc.RailSpacingMi.Sample(new Rng(worldSeed)) * Mile;
            for (int m = 0; m < 8; m++)
            {
                float off = RailEWOffset(worldSeed, m, nc);
                float swell = RailEWSwell(worldSeed, m, 0f, nc);
                float railZ = RailEWBase(worldSeed, m, nc) + off + swell;
                float roadZ = RailEWBase(worldSeed, m, nc);
                worst = MathF.Max(worst, MathF.Abs(railZ - roadZ));
                float closest = MathF.Abs(railZ - roadZ);
                if (closest < 30f) { detail = $"rail {m} runs {closest:0} ft from its road"; return false; }
                float amp = nc.RailSwellBaseAmpFt + nc.RailSwellAmpJitterFt * Hash.F(worldSeed, m, 852);
                float len = nc.RailSwellBaseWavelengthFt + nc.RailSwellWavelengthJitterFt * Hash.F(worldSeed, m, 853);
                float slope = amp * 0.5f / len;
                float deg = MathF.Atan(slope) * 180f / MathF.PI;
                crossings++;
                if (deg > 12f) { detail = $"rail {m} runs {deg:0.0}° off square"; return false; }
            }
            for (int n = 0; n < 8; n++)
            {
                float closest = MathF.Abs(RailNSOffset(worldSeed, n, nc) + RailNSSwell(worldSeed, n, 0f, nc));
                if (closest < 30f) { detail = $"rail {n} runs {closest:0} ft from its road"; return false; }
                crossings++;
            }
            detail = $"{crossings} rail lines, all off their own road by 40-380 ft, all under 12° off square";
            return true;
        }

        static int CountDanglingRoads(WorldTown town, List<string>? detail = null)
        {
            var segs = new List<(float x0, float z0, float x1, float z1)>();
            var through = new List<bool>();
            var bulbs = new List<Vec2>();
            foreach (var part in town.Parts)
            {
                float ox = part.OriginX, oz = part.OriginZ;
                bool mir = part.Mirrored;
                float WZ(float lz) => mir ? oz + (66f - lz) : oz + lz;
                foreach (var r in part.Plan.Roads)
                {
                    float z0 = WZ(r.Z), z1 = WZ(r.Z + r.H);
                    segs.Add((ox + r.X, MathF.Min(z0, z1), ox + r.X + r.W, MathF.Max(z0, z1)));
                    bool freeEnd = part.Plan.RoadMayEndFree.Any(f =>
                        MathF.Abs(f.X - r.X) < 0.5f && MathF.Abs(f.Z - r.Z) < 0.5f &&
                        MathF.Abs(f.W - r.W) < 0.5f && MathF.Abs(f.H - r.H) < 0.5f);
                    through.Add(freeEnd || IsTownHighway(r));
                }
                foreach (var b in part.Plan.Bulbs)
                    bulbs.Add(new Vec2(ox + b.Center.X, WZ(b.Center.Z)));
                foreach (var p in part.Plan.RoadPaths)
                    foreach (var tip in new[] { p.Points[0], p.Points[p.Points.Count - 1] })
                    {
                        float tz = WZ(tip.Z);
                        segs.Add((ox + tip.X, tz, ox + tip.X, tz));
                        through.Add(false);
                    }
            }

            int dangling = 0;
            for (int i = 0; i < segs.Count; i++)
            {
                if (through[i]) continue;
                var s = segs[i];
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

        static (string kind, float x, float z) ParseElement(string s)
        {
            var parts = s.Split('|');
            if (parts.Length < 3) return (s, 0f, 0f);
            float x = float.TryParse(parts[1], out var vx) ? vx : 0f;
            float z = float.TryParse(parts[2], out var vz) ? vz : 0f;
            return (parts[0] + (parts.Length > 3 ? "|" + parts[3] : ""), x, z);
        }

        static int CountBuildingOverlaps(MapPlan map, out string info)
        {
            info = "";
            var shapes = new List<(List<Vec2> points, string what)>();
            foreach (var h in map.Houses)
            {
                shapes.Add((h.Plan.Footprint, "house:" + h.Plan.Archetype));
            }
            foreach (var o in map.Outbuildings)
            {
                if (o.Kind == OutbuildingKind.PropaneTank) continue;
                float hw = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet / 2f : o.WidthFeet / 2f;
                float hd = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet / 2f : o.DepthFeet / 2f;
                shapes.Add((new List<Vec2> { new(o.Center.X - hw, o.Center.Z - hd), new(o.Center.X + hw, o.Center.Z - hd), new(o.Center.X + hw, o.Center.Z + hd), new(o.Center.X - hw, o.Center.Z + hd) }, "out:" + o.Kind));
            }
            bool InBounds(List<Vec2> p, List<Vec2> q) => p.Min(v => v.X) < q.Max(v => v.X) - 1f && p.Max(v => v.X) > q.Min(v => v.X) + 1f && p.Min(v => v.Z) < q.Max(v => v.Z) - 1f && p.Max(v => v.Z) > q.Min(v => v.Z) + 1f;
            float Cross(Vec2 a, Vec2 b, Vec2 c) => (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);
            // Count positive-area intersection. Shared/touching edges are not
            // overlaps; the previous AABB check falsely flagged concave L-houses.
            bool SegmentsCross(Vec2 a, Vec2 b, Vec2 c, Vec2 d) => Cross(a, b, c) * Cross(a, b, d) < -0.01f && Cross(c, d, a) * Cross(c, d, b) < -0.01f;
            bool Contains(List<Vec2> polygon, Vec2 p)
            {
                bool inside = false;
                for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
                {
                    var a = polygon[i]; var b = polygon[j];
                    if ((a.Z > p.Z) != (b.Z > p.Z) && p.X < (b.X - a.X) * (p.Z - a.Z) / (b.Z - a.Z) + a.X) inside = !inside;
                }
                return inside;
            }
            bool Overlaps(List<Vec2> a, List<Vec2> b)
            {
                if (a.Count < 3 || b.Count < 3 || !InBounds(a, b)) return false;
                for (int i = 0; i < a.Count; i++) for (int j = 0; j < b.Count; j++)
                    if (SegmentsCross(a[i], a[(i + 1) % a.Count], b[j], b[(j + 1) % b.Count])) return true;
                return Contains(a, b[0]) || Contains(b, a[0]);
            }
            int overlaps = 0;
            var pairs = new List<string>();
            for (int i = 0; i < shapes.Count; i++)
                for (int j = i + 1; j < shapes.Count; j++)
                {
                    var a = shapes[i]; var b = shapes[j];
                    if (Overlaps(a.points, b.points))
                    {
                        overlaps++;
                        if (pairs.Count < 4) pairs.Add($"{a.what} [{a.points.Min(p => p.X):0},{a.points.Min(p => p.Z):0}–{a.points.Max(p => p.X):0},{a.points.Max(p => p.Z):0}] vs {b.what} [{b.points.Min(p => p.X):0},{b.points.Min(p => p.Z):0}–{b.points.Max(p => p.X):0},{b.points.Max(p => p.Z):0}]");
                    }
                }
            if (pairs.Count > 0) info = $" (first: {string.Join(" | ", pairs)})";
            return overlaps;
        }

        static readonly string[] FieldPalette = { "#ded9bd", "#d6d0b4", "#cbc4a2", "#e4e0cb", "#d2cbb0", "#dcd6b8", "#d8d2b6" };
    }
}
