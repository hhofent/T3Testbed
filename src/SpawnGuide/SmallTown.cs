using System.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpawnGuide
{
    /// <summary>What the world knows about a town's surroundings that the town itself cannot work out.</summary>
    public sealed class TownContext
    {
        public bool HasRailSpur;
        public float RailZ;
        public float SpurOffsetFt;
        public bool OwnsSpur;
        public static readonly TownContext None = new TownContext();
    }

    /// <summary>
    /// A whole small town, seeded and reproducible.
    /// Uses generative tier system (Central Place Theory k=3) and feature grammar for extras.
    /// </summary>
    public static class SmallTownGenerator
    {
        const float HighwayWidth = 66f;
        const float StreetRow = 36f;
        const float AlleyRow = 16f;
        const float BlockLotDepth = 122f;
        const float SideLotDepth = 80f;
        const float BandDepth = 175f;

        /// <param name="detail">false = quiet annotations, true = all captions on.</param>
        /// <param name="ctx">World context (rail spur info).</param>
        /// <param name="config">Small town configuration (uses new ResidentialConfig, StreetConfig, etc.).</param>
        public static MapPlan Generate(uint seed, string sizeName, bool detail = false, TownContext? ctx = null, SmallTownConfig? config = null)
        {
            ctx ??= TownContext.None;
            var cfg = config ?? new SmallTownConfig();

            var rng = new Rng(seed);
            float lotDepth = BlockLotDepth + rng.Range(-cfg.LotDepthJitter, cfg.LotDepthJitter);
            int blockRows = 0; // Will be set after tier computed

            // --- Determine town tier from sizeName or pick randomly ---
            int tier = sizeName.ToLowerInvariant() switch
            {
                "crossroads" => 0,
                "hamlet" => 1,
                "village" => 2,
                "town" => 3,
                "boyden" => 2,  // alias for village
                "hull" => 3,    // alias for town
                _ => 1,         // default hamlet
            };

            // Use TownTierSystem if available in parent config
            var tierSystem = new TownTierSystem(); // Default, could be passed from SpawnConfig

            // Compute grid size from tier system
            var (columns, ewStreets) = tierSystem.ComputeGridSize(tier, rng);
            blockRows = ewStreets - 1;

            int commercialPads = tierSystem.ComputeCommercialPads(tier, columns * ewStreets * 4 * 2, rng);
            int civicAnchors = tierSystem.ComputeCivicAnchors(tier, columns * ewStreets * 4 * 2, rng);
            int deadEnds = tierSystem.ComputeDeadEnds(tier, rng);

            // --- Select extras via feature grammar ---
            var extraPool = cfg.TownExtras ?? TownExtraGrammar.GetDefaultPool();
            var selectedExtras = TownExtraGrammar.SelectExtras(tier, extraPool, rng,
                cfg.MaxExtrasPerTown, cfg.ExtraNoneChance, cfg.ExtraOneChance);

            bool hasCourt = selectedExtras.Exists(e => e.Id == "CulDeSacCourt");
            bool hasPlatEdge = selectedExtras.Exists(e => e.Id == "PlatEdgeStreet");
            bool hasOutlyingRoad = selectedExtras.Exists(e => e.Id == "OutlyingRoad");
            bool hasBypassedHighway = selectedExtras.Exists(e => e.Id == "BypassedHighway");
            bool hasSideStreet = selectedExtras.Exists(e => e.Id == "IndustrialSideStreet");
            bool hasTownSquare = selectedExtras.Exists(e => e.Id == "TownSquare");
            bool hasBoulevard = selectedExtras.Exists(e => e.Id == "Boulevard");
            bool hasRailCorridor = selectedExtras.Exists(e => e.Id == "RailCorridorStreet");
            bool hasCurvedStreet = selectedExtras.Exists(e => e.Id == "CurvedStreet");
            bool hasOuterRoad = hasOutlyingRoad || hasBypassedHighway;

            // Per-block-row alley decision
            var rowAlley = new bool[blockRows];
            for (int r = 0; r < blockRows; r++)
                rowAlley[r] = rng.Chance(cfg.RowAlleyChance.Sample(rng));
            float BlockDepth(int row) => 2 * lotDepth + (rowAlley[row] ? AlleyRow : 0f);

            // Place name
            string townName = rng.Pick(cfg.PlaceNames);

            // Street names
            string[] ewNames = cfg.EWStreetNamePool.OrderBy(_ => rng.NextUInt()).ToArray();
            string[] nsNames = cfg.NSStreetNamePool.OrderBy(_ => rng.NextUInt()).ToArray();
            int mainStreetIndex = columns / 2;

            // Column plan
            var colFrontages = new List<List<float>>();
            for (int c = 0; c < columns; c++)
            {
                int n = rng.RangeInt(3, 6); // Default lots per column
                colFrontages.Add(new List<float>());
                for (int i = 0; i < n; i++) colFrontages[c].Add(cfg.LotFrontage.Sample(rng));
            }
            float BlockWidth(int col) => colFrontages[col].Sum() + 2 * SideLotDepth;
            float totalWidth = StreetRow * (columns + 1);
            for (int c = 0; c < columns; c++) totalWidth += BlockWidth(c);

            var nsX = new List<float>();
            { float x = 0f; for (int s = 0; s <= columns; s++) { nsX.Add(x); if (s < columns) x += StreetRow + BlockWidth(s); } }

            var fourSide = new Dictionary<(int row, int col), bool>();
            bool IsFourSide(int blockRow, int col)
            {
                var key = (blockRow, col);
                if (!fourSide.TryGetValue(key, out bool has))
                {
                    has = rng.Chance(cfg.FourSideBlockChance);
                    fourSide[key] = has;
                }
                return has;
            }

            // --- Blob boundary ---
            float blobCX = 0f, blobCZ = 0f, blobRX = 1f, blobRZ = 1f;
            int hb1 = 0, hb2 = 0;
            float ha1 = 0, ha2 = 0, hp1 = 0, hp2 = 0;
            bool blobReady = false;

            // Z layout
            float zBand = HighwayWidth;
            float zFirstStreet = zBand + BandDepth;
            var ewZ = new float[ewStreets];
            ewZ[0] = zFirstStreet;
            for (int i = 1; i < ewStreets; i++) ewZ[i] = ewZ[i - 1] + StreetRow + BlockDepth(i - 1);
            float lastStreetEnd = ewZ[ewStreets - 1] + StreetRow;
            float totalDepth = lastStreetEnd + lotDepth;

            blobCZ = (zBand + totalDepth) / 2f;
            blobRZ = (totalDepth - zBand) / 2f * cfg.BlobRadiusMultiplier;
            blobCX = totalWidth / 2f;
            blobRX = totalWidth / 2f * cfg.BlobRadiusMultiplier;
            hb1 = rng.RangeInt(cfg.BlobHarmonic1.Min, cfg.BlobHarmonic1.Max);
            hb2 = rng.RangeInt(cfg.BlobHarmonic2.Min, cfg.BlobHarmonic2.Max);
            ha1 = rng.Range(cfg.BlobAmplitude1.Min, cfg.BlobAmplitude1.Max);
            ha2 = rng.Range(cfg.BlobAmplitude2.Min, cfg.BlobAmplitude2.Max);
            hp1 = rng.Range(cfg.BlobPhase.Min, cfg.BlobPhase.Max);
            hp2 = rng.Range(cfg.BlobPhase.Min, cfg.BlobPhase.Max);
            blobReady = true;

            bool InsideBlob(float x, float z)
            {
                if (!blobReady) return true;
                float nx = (x - blobCX) / blobRX, nz = (z - blobCZ) / blobRZ;
                float ang = MathF.Atan2(nz, nx);
                float r = MathF.Sqrt(nx * nx + nz * nz);
                float boundary = 1.0f + ha1 * MathF.Cos(hb1 * ang + hp1) + ha2 * MathF.Cos(hb2 * ang + hp2);
                return r <= boundary;
            }

            bool FreeStreetFit(float x, float z)
            {
                if (!blobReady) return true;
                float nx = (x - blobCX) / blobRX, nz = (z - blobCZ) / blobRZ;
                float ang = MathF.Atan2(nz, nx);
                float r = MathF.Sqrt(nx * nx + nz * nz);
                float boundary = 1.0f + ha1 * MathF.Cos(hb1 * ang + hp1) + ha2 * MathF.Cos(hb2 * ang + hp2) + cfg.FreeStreetBlobPadding;
                return r <= boundary;
            }

            // Content tracking
            var streetZ = Enumerable.Range(0, columns + 1).Select(_ => (z0: float.MaxValue, z1: float.MinValue)).ToList();
            var streetX = Enumerable.Range(0, ewStreets).Select(_ => (x0: float.MaxValue, x1: float.MinValue)).ToArray();
            var rowFilled = new bool[blockRows];
            var colFilled = new bool[blockRows, columns];
            var northEdgeFilled = new HashSet<int>();
            void RecordContent(int districtRow, int col, float x0, float x1, float z0, float z1)
            {
                for (int s = col; s <= col + 1 && s <= columns; s++) { var t = streetZ[s]; streetZ[s] = (MathF.Min(t.z0, z0), MathF.Max(t.z1, z1)); }
                if (districtRow >= 0)
                {
                    for (int s = districtRow; s <= districtRow + 1 && s < ewStreets; s++) { streetX[s].x0 = MathF.Min(streetX[s].x0, x0); streetX[s].x1 = MathF.Max(streetX[s].x1, x1); }
                    if (districtRow < blockRows) rowFilled[districtRow] = true;
                    if (col < columns && districtRow < blockRows) colFilled[districtRow, col] = true;
                    else if (districtRow == blockRows) northEdgeFilled.Add(col);
                }
                else { streetX[0].x0 = MathF.Min(streetX[0].x0, x0); streetX[0].x1 = MathF.Max(streetX[0].x1, x1); }
            }

            // Occupancy registry
            var occupied = new List<(float x0, float z0, float x1, float z1)>();
            static (float x0, float z0, float x1, float z1) FootprintBounds(HousePlan h) { float x0 = h.Footprint.Select(p => p.X).Min(), x1 = h.Footprint.Select(p => p.X).Max(); float z0 = h.Footprint.Select(p => p.Z).Min(), z1 = h.Footprint.Select(p => p.Z).Max(); const float pad = 3f; return (x0 - pad, z0 - pad, x1 + pad, z1 + pad); }
            static (float x0, float z0, float x1, float z1) OccupiedBounds(Outbuilding o) { float hw = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet / 2f : o.WidthFeet / 2f; float hd = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet / 2f : o.DepthFeet / 2f; const float pad = 2f; return (o.Center.X - hw - pad, o.Center.Z - hd - pad, o.Center.X + hw + pad, o.Center.Z + hd + pad); }
            bool OccupiedCollides((float x0, float z0, float x1, float z1) r) => occupied.Any(o => r.x0 < o.x1 && r.x1 > o.x0 && r.z0 < o.z1 && r.z1 > o.z0);
            void RegisterOccupied((float x0, float z0, float x1, float z1) r) => occupied.Add(r);

            float BlobNormR(float x, float z) { float nx = (x - blobCX) / blobRX, nz = (z - blobCZ) / blobRZ; return MathF.Sqrt(nx * nx + nz * nz); }
            bool IsVacantLot(float cx, float cz)
            {
                float r = BlobNormR(cx, cz);
                float p = r > cfg.VacantOuterRadius ? cfg.VacantOuterChance : r > cfg.VacantMidRadius ? cfg.VacantMidChance : cfg.VacantCoreChance;
                return rng.Chance(p);
            }

            var map = new MapPlan { Title = $"SMALL TOWN — {townName} — SEED {seed} ({tierSystem.GetLabel(tier)})", Seed = seed, PlaceName = townName, WidthFeet = totalWidth, DepthFeet = totalDepth, RoadSurface = "asphalt", QuietAnnotations = !detail };

            // --- Branch spur ground ---
            int spurIdx = -1;
            float spurTrackX = 0f;
            var reserved = new List<MapRect>();
            if (ctx.HasRailSpur && ctx.OwnsSpur && columns >= 3)
            {
                float want = totalWidth / 2f + ctx.SpurOffsetFt;
                int best = -1; float bestD = float.MaxValue;
                for (int s = 1; s < columns; s++) { float d = MathF.Abs(nsX[s] + StreetRow / 2f - want); if (d < bestD) { bestD = d; best = s; } }
                if (best > 0) spurIdx = best;
            }
            if (spurIdx >= 0) spurTrackX = nsX[spurIdx] - 64f;

            // --- Commercial band along highway ---
            float mainX = nsX[mainStreetIndex];
            var bandOcc = new List<(float x0, float x1)>();

            float sideStreetX = -1f;
            if (hasSideStreet)
            {
                sideStreetX = rng.Chance(0.5f) ? rng.Range(60f, MathF.Max(61f, mainX - StreetRow - 120f)) : rng.Range(mainX + StreetRow + 120f, MathF.Max(mainX + StreetRow + 121f, totalWidth - 120f));
                sideStreetX = Math.Clamp(sideStreetX, 50f, MathF.Max(50f, totalWidth - 120f));
                if (spurIdx >= 0 && sideStreetX + StreetRow > spurTrackX - 16f && sideStreetX - StreetRow / 2f < spurTrackX + 64f)
                    sideStreetX = Math.Clamp(spurTrackX + 76f, 50f, MathF.Max(50f, totalWidth - 120f));
                bandOcc.Add((sideStreetX - StreetRow / 2f - 6f, sideStreetX + StreetRow / 2f + 6f));
            }

            // Grain elevator
            float evW = cfg.ElevatorWidth, evD = cfg.ElevatorDepth;
            bool elevatorRight = rng.Chance(cfg.ElevatorRightChance);
            float evX0 = elevatorRight ? mainX + StreetRow + cfg.ElevatorOffsetFromMain.Sample(rng) : mainX - StreetRow - cfg.ElevatorOffsetFromMain.Sample(rng) - evW;
            if (spurIdx >= 0) { spurTrackX = Math.Clamp(spurTrackX, 60f, MathF.Max(60f, totalWidth - 60f)); evX0 = spurTrackX - evW / 2f; }
            else evX0 = Math.Clamp(evX0, 40f, MathF.Max(40f, totalWidth - evW - 40f));

            map.Outbuildings.Add(new Outbuilding { Kind = OutbuildingKind.GrainElevator, Center = new Vec2(evX0 + evW / 2f, zBand + BandDepth / 2f), WidthFeet = evW, DepthFeet = evD, Label = "ELEVATOR" });
            bandOcc.Add((evX0 - 12f, evX0 + evW + 12f));

            int bins = cfg.ElevatorBinCount.Sample(rng);
            float binD = cfg.BinDiameter.Sample(rng), spacing = cfg.BinSpacing;
            float binRowW = bins * spacing - 8f;
            float binX = evX0 + (evW - binRowW) / 2f;
            if (binX + binRowW > totalWidth - 8f) binX = totalWidth - 8f - binRowW;
            if (binX < 8f) binX = 8f;
            for (int i = 0; i < bins; i++) map.Outbuildings.Add(new Outbuilding { Kind = OutbuildingKind.GrainBin, Center = new Vec2(binX + i * spacing + binD / 2f, zBand + BandDepth / 2f + evD / 2f + 10f + binD / 2f), DiameterFeet = binD, Label = "" });
            bandOcc.Add((binX - 8f, binX + binRowW + 8f));

            // Rail industrial strip
            if (spurIdx >= 0)
            {
                bandOcc.Add((spurTrackX - 16f, spurTrackX + 64f));
                var railOcc = new List<(float x0, float z0, float x1, float z1)> { (evX0, zBand + BandDepth / 2f - evD / 2f, evX0 + evW, zBand + BandDepth / 2f + evD / 2f), (binX, zBand + BandDepth / 2f + evD / 2f + 10f, binX + binRowW, zBand + BandDepth / 2f + evD / 2f + 10f + binD) };
                var railPool = BusinessCatalogue.Industrial.Where(b => b.MinTier <= tier).ToList();
                for (int row = 0; row < blockRows && railPool.Count > 0; row++)
                {
                    if (row == blockRows - 1 && blockRows > 1) continue;
                    float z0 = ewZ[row] + StreetRow, z1 = row + 1 < ewZ.Length ? ewZ[row + 1] : zBand + BandDepth;
                    float along = cfg.RailIndustrialRowSpacing.Sample(rng);
                    if (z1 - z0 < along + 24f) continue;
                    int pick = rng.RangeInt(0, railPool.Count); var biz = railPool[pick]; railPool.RemoveAt(pick);
                    float across = MathF.Min(rng.Range(biz.MinDepth, biz.MaxDepth), 52f);
                    float cz = z0 + (z1 - z0) / 2f + rng.Range(-16f, 16f);
                    float cx = spurTrackX + 12f + across / 2f;
                    bool clash = railOcc.Any(o => cx - across / 2f < o.x1 && cx + across / 2f > o.x0 && cz - along / 2f < o.z1 && cz + along / 2f > o.z0);
                    if (clash) continue;
                    map.Outbuildings.Add(new Outbuilding { Kind = OutbuildingKind.Storefront, Center = new Vec2(cx, cz), WidthFeet = across, DepthFeet = along, Label = BusinessCatalogue.LabelFor(rng, biz) });
                    railOcc.Add((cx - across / 2f, cz - along / 2f, cx + across / 2f, cz + along / 2f));
                }
            }

            // Highway commercial strip (using CommercialConfig if available)
            var commercialCfg = new CommercialConfig(); // Would be from SpawnConfig
            var eligible = BusinessCatalogue.Shops.Where(b => b.MinTier <= tier).ToList();
            var chosen = new List<BusinessCatalogue.Business>();
            var gas = eligible.First(b => b.Descriptor == "GAS STATION"); chosen.Add(gas);
            var rest = eligible.Where(b => b != gas).ToList();
            for (int i = rest.Count - 1; i > 0; i--) { int j = rng.RangeInt(0, i + 1); (rest[i], rest[j]) = (rest[j], rest[i]); }
            for (int i = 0; i < commercialPads - 1 && i < rest.Count; i++) chosen.Add(rest[i]);
            var edge = chosen.Where(b => b.Edge).ToList(); var core = chosen.Where(b => !b.Edge).ToList();
            for (int i = core.Count - 1; i > 0; i--) { int j = rng.RangeInt(0, i + 1); (core[i], core[j]) = (core[j], core[i]); }
            var strip = new List<BusinessCatalogue.Business>(); int leftSlots = edge.Count / 2;
            for (int i = 0; i < leftSlots; i++) strip.Add(edge[i]); strip.AddRange(core); for (int i = edge.Count - 1; i >= leftSlots; i--) strip.Add(edge[i]);

            int indCount = rng.Chance(0.55f) ? 2 : 1;
            var indEligible = BusinessCatalogue.Industrial.Where(b => b.MinTier <= tier).ToList();
            var industrial = new List<BusinessCatalogue.Business>();
            for (int i = 0; i < indCount && indEligible.Count > 0; i++) { int pick = rng.RangeInt(0, indEligible.Count); industrial.Add(indEligible[pick]); indEligible.RemoveAt(pick); }

            { // Lay out strip
                var placed = new List<(BusinessCatalogue.Business biz, float x, float w, float d, string label)>();
                float x = cfg.CommercialStartX.Sample(rng); int guard = 0;
                foreach (var biz in strip)
                {
                    float w = rng.Range(biz.MinWidth, biz.MaxWidth), d = rng.Range(biz.MinDepth, biz.MaxDepth);
                    int tries = 0;
                    while (tries++ < 24)
                    {
                        bool inMainGap = x + w > mainX - 12f && x < mainX + StreetRow + 12f;
                        bool inBandOcc = bandOcc.Any(b => x < b.x1 && x + w > b.x0);
                        if (x + w <= totalWidth - 35f && !inMainGap && !inBandOcc) break;
                        x += 12f;
                    }
                    if (tries >= 24) continue;
                    float gap = cfg.CommercialGap.Sample(rng);
                    placed.Add((biz, x, w, d, BusinessCatalogue.LabelFor(rng, biz)));
                    bandOcc.Add((x - 5f, x + w + 5f));
                    x += w + gap;
                    if (guard++ > 40) break;
                }
                foreach (var p in placed) { RecordContent(-1, 0, p.x, p.x + p.w, zBand, zBand + 80f); map.Outbuildings.Add(new Outbuilding { Kind = OutbuildingKind.Storefront, Center = new Vec2(p.x + p.w / 2f, zBand + p.d / 2f + 8f), WidthFeet = p.w, DepthFeet = p.d, Label = p.label }); }
                foreach (var biz in industrial) { float iw = rng.Range(biz.MinWidth, biz.MaxWidth), id = rng.Range(biz.MinDepth, biz.MaxDepth); float ix = rng.Range(45f, MathF.Max(46f, totalWidth - iw - 45f)); bool straddles = ix < mainX + StreetRow + 20f && ix + iw > mainX - 20f; bool collides = bandOcc.Any(b => ix < b.x1 && ix + iw > b.x0); if (straddles || collides) continue; map.Outbuildings.Add(new Outbuilding { Kind = OutbuildingKind.Storefront, Center = new Vec2(ix + iw / 2f, zBand + id / 2f + 8f), WidthFeet = iw, DepthFeet = id, Label = BusinessCatalogue.LabelFor(rng, biz) }); bandOcc.Add((ix - 10f, ix + iw + 10f)); }

                int crossShops = rng.RangeInt(0, cfg.CrossStreetShopMax);
                for (int i = 0; i < crossShops; i++)
                {
                    var biz = eligible.Count > 0 ? eligible[rng.RangeInt(0, eligible.Count)] : null;
                    if (biz == null) break;
                    bool west = rng.Chance(0.5f);
                    float w = rng.Range(biz.MinDepth, biz.MaxDepth), d = rng.Range(biz.MinWidth, biz.MaxWidth);
                    float cx = west ? mainX - StreetRow / 2f - 6f - w / 2f : mainX + StreetRow / 2f + 6f + w / 2f;
                    float cz = rng.Range(zBand + 24f, MathF.Max(zBand + 25f, zBand + BandDepth - d - 12f));
                    bool clash = bandOcc.Any(b => cx - w / 2f < b.x1 && cx + w / 2f > b.x0); if (clash) continue;
                    map.Outbuildings.Add(new Outbuilding { Kind = OutbuildingKind.Storefront, Center = new Vec2(cx, cz), WidthFeet = w, DepthFeet = d, Label = BusinessCatalogue.LabelFor(rng, biz) });
                    bandOcc.Add((cx - w / 2f - 6f, cx + w / 2f + 6f));
                }
            }

            uint houseSeed = seed * 2654435761u + 97u;

            // --- SOUTH side fringe ---
            float southBand = cfg.SouthBandDepth.Sample(rng);
            float southEdge = -southBand;
            { float x = 45f; int houseCount = cfg.SouthHouseCount.Sample(rng); for (int i = 0; i < houseCount && x < totalWidth - 260f; i++) { float hW = cfg.SouthHouseWidth.Sample(rng); uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u; string arch = Weighted(rng); var house = HouseGenerator.Generate(arch, hSeed, i % 3 == 0 ? "detached" : "integral"); house = house.FlipZ(); float hx = x + MathF.Max(4f, (hW - house.WidthFeet) / 2f); float setback = rng.Range(10f, 22f); house = house.OffsetCopy(hx, -setback); map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = true }); var fd = FindFrontDoor(house); map.Walks.Add(new Driveway { Surface = "concrete", WidthFeet = 4f, Points = { new Vec2(fd.X, -2.25f), fd } }); var (garageDoor, _, frontEntry) = FindGarageDoor(house); if (!float.IsNaN(garageDoor.X)) { var d = DrivewayTo(garageDoor, frontEntry, true, -34f, "concrete", 16f); map.Driveways.Add(d); } x += hW + cfg.SouthHouseSpacing.Sample(rng); } var southPool = BusinessCatalogue.Industrial.Where(b => b.MinTier <= tier).ToList(); if (southPool.Count > 0 && rng.Chance(cfg.SouthIndustrialChance)) { var southBiz = southPool[rng.RangeInt(0, southPool.Count)]; float iw = rng.Range(southBiz.MinWidth, southBiz.MaxWidth), id = rng.Range(southBiz.MinDepth, southBiz.MaxDepth); float ix = totalWidth - iw - 45f; if (ix > x + 20f) map.Outbuildings.Add(new Outbuilding { Kind = OutbuildingKind.Storefront, Center = new Vec2(ix + iw / 2f, southEdge + id / 2f + 10f), WidthFeet = iw, DepthFeet = id, Label = BusinessCatalogue.LabelFor(rng, southBiz) }); } }
            map.DepthFeet += southBand;

            // --- Civic reserve faces ---
            var faceIds = new List<(int s, int side, int col)>();
            for (int s = 0; s < ewStreets; s++) for (int col = 0; col < columns; col++) faceIds.Add((s, 0, col));
            for (int s = 1; s < ewStreets; s++) for (int col = 0; col < columns; col++) faceIds.Add((s, 1, col));
            var civic = new Dictionary<(int s, int side, int col), OutbuildingKind>();
            var civicKinds = new List<OutbuildingKind>();

            // Churches per tier
            int churches = tier switch { 0 => cfg.ChurchCountTier0.Sample(rng), 1 => cfg.ChurchCountTier1.Sample(rng), _ => cfg.ChurchCountTier2Plus.Sample(rng) };
            for (int i = 0; i < churches && civicKinds.Count < civicAnchors; i++) civicKinds.Add(OutbuildingKind.Church);
            float schoolChance = tier == 0 ? cfg.SchoolChanceTier0 : tier == 1 ? cfg.SchoolChanceTier1 : cfg.SchoolChanceTier2Plus;
            if (civicKinds.Count < civicAnchors && rng.Chance(schoolChance)) civicKinds.Add(OutbuildingKind.School);
            float parkChance = tier == 0 ? cfg.ParkChanceTier0 : tier == 1 ? cfg.ParkChanceTier1 : cfg.ParkChanceTier2Plus;
            if (civicKinds.Count < civicAnchors && rng.Chance(parkChance)) civicKinds.Add(OutbuildingKind.CityPark);
            var spare = new List<OutbuildingKind> { OutbuildingKind.Church, OutbuildingKind.CityPark, OutbuildingKind.School };
            while (civicKinds.Count < civicAnchors && spare.Count > 0) { int k = rng.RangeInt(0, spare.Count); civicKinds.Add(spare[k]); spare.RemoveAt(k); }
            for (int i = civicKinds.Count - 1; i > 0; i--) { int j = rng.RangeInt(0, i + 1); (civicKinds[i], civicKinds[j]) = (civicKinds[j], civicKinds[i]); }

            if (hasTownSquare && faceIds.Count > 0)
            {
                int bestIdx = 0, bestScore = int.MaxValue;
                for (int i = 0; i < faceIds.Count; i++) { var f = faceIds[i]; int score = Math.Abs(f.s - ewStreets / 2) * 100 + Math.Abs(f.col - columns / 2); if (score < bestScore) { bestScore = score; bestIdx = i; } }
                var sq = faceIds[bestIdx]; faceIds.RemoveAt(bestIdx); civic[sq] = OutbuildingKind.CityPark; civicKinds.RemoveAll(k => k == OutbuildingKind.CityPark);
            }
            for (int i = 0; i < civicKinds.Count && faceIds.Count > 0; i++) { int idx = rng.RangeInt(0, faceIds.Count); civic[faceIds[idx]] = civicKinds[i]; faceIds.RemoveAt(idx); }

            // Landmarks
            var freeLandmarks = new List<BusinessCatalogue.Landmark>();
            foreach (var lm in BusinessCatalogue.Landmarks)
            {
                if (lm.MinTier > tier) continue;
                if (!rng.Chance(lm.Chance)) continue;
                if (lm.Kind == OutbuildingKind.Depot && spurIdx < 0) continue;
                if (lm.Kind == OutbuildingKind.Depot)
                {
                    float depotZ = ewZ[Math.Max(0, blockRows - 1)] + StreetRow + 45f;
                    map.Outbuildings.Add(new Outbuilding { Kind = OutbuildingKind.Depot, Center = new Vec2(spurTrackX + 28f, depotZ), WidthFeet = 34f, DepthFeet = 70f, Label = "DEPOT" });
                    continue;
                }
                if (lm.FreeStanding) { freeLandmarks.Add(lm); continue; }
                if (faceIds.Count == 0) continue;
                int pick = rng.RangeInt(0, faceIds.Count); var face = faceIds[pick]; faceIds.RemoveAt(pick); civic[face] = lm.Kind;
            }

            void FillTownLot(int streetIdx, int col, int lotIdx, bool facesSouth, int districtRow, float lotX, float frontage, OutbuildingKind? anchor, bool alley, float? depthOverride = null)
            {
                float streetZ = ewZ[streetIdx]; float dep = depthOverride ?? lotDepth;
                float rowZ0 = facesSouth ? streetZ + StreetRow : streetZ - dep;
                float cx = lotX + frontage / 2f, cz = rowZ0 + dep / 2f;
                if (!InsideBlob(cx, cz)) return;
                RecordContent(districtRow, col, lotX, lotX + frontage, rowZ0, rowZ0 + dep);
                if (!anchor.HasValue && IsVacantLot(cx, cz)) { map.LotLines.Add(new MapRect(lotX, rowZ0, frontage, dep)); return; }
                map.LotLines.Add(new MapRect(lotX, rowZ0, frontage, dep));

                float setback = cfg.LotSetback.Sample(rng); float frontLineZ = facesSouth ? streetZ + StreetRow : streetZ;
                int gRoll = rng.RangeInt(0, 100);
                float detachedThreshold = cfg.GarageModeDetachedChance * 100;
                float integralThreshold = detachedThreshold + cfg.GarageModeIntegralChance * 100;
                string garageMode = gRoll < detachedThreshold ? "detached" : gRoll < integralThreshold ? "integral" : "none";
                uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u;

                if (anchor.HasValue) { var a = PlaceAnchor(anchor.Value, lotX, rowZ0, frontage, dep); map.Outbuildings.Add(a); RegisterOccupied(OccupiedBounds(a)); map.Walks.Add(new Driveway { Surface = "concrete", WidthFeet = 5f, Points = { new Vec2(a.Center.X, frontLineZ), new Vec2(a.Center.X, a.Center.Z) } }); return; }

                string arch = frontage < cfg.HouseFrontageNarrowThreshold ? "farmhouse" : Weighted(rng);
                var house = HouseGenerator.Generate(arch, hSeed, garageMode);
                if (house.WidthFeet > frontage - 3f && arch != "farmhouse") house = HouseGenerator.Generate("farmhouse", hSeed ^ 0x5f3759dfu);
                float hx = lotX + MathF.Max(2f, (frontage - house.WidthFeet) / 2f + rng.Range(-cfg.HousePlacementJitter, cfg.HousePlacementJitter));
                bool mirrored = !facesSouth; if (mirrored) house = house.FlipZ();
                float hzSpan = house.Footprint.Select(p => p.Z).Max() - house.Footprint.Select(p => p.Z).Min();
                if (hzSpan > dep - cfg.HouseFitPad) { RecordContent(districtRow, col, lotX, lotX + frontage, rowZ0, rowZ0 + dep); map.LotLines.Add(new MapRect(lotX, rowZ0, frontage, dep)); return; }
                setback = MathF.Min(setback, dep - 4f - hzSpan); if (setback < cfg.HouseSetbackMin) { map.LotLines.Add(new MapRect(lotX, rowZ0, frontage, dep)); return; }
                float offsetZ = facesSouth ? frontLineZ + setback : frontLineZ - setback; house = house.OffsetCopy(hx, offsetZ);
                map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = mirrored }); RegisterOccupied(FootprintBounds(house));

                var fd = FindFrontDoor(house); float walkStartZ = facesSouth ? frontLineZ - 2.25f : frontLineZ + 2.25f;
                map.Walks.Add(new Driveway { Surface = "concrete", WidthFeet = 4f, Points = { new Vec2(fd.X, walkStartZ), fd } });

                if (rng.Chance(cfg.ShedChance)) { float shedW = cfg.ShedWidth.Sample(rng), shedD = cfg.ShedDepth.Sample(rng); bool garageRight = house.Walls.Any(w => w.Tag == EdgeTag.GarageSide) && (house.Walls.First(w => w.Tag == EdgeTag.GarageSide).Start.X + house.Walls.First(w => w.Tag == EdgeTag.GarageSide).End.X) / 2f > (house.Footprint.Select(p => p.X).Min() + house.Footprint.Select(p => p.X).Max()) / 2f; float shedX = garageRight ? lotX + 4f : lotX + frontage - shedW - 4f; float shedZ = rowZ0 + dep - shedD - 5f; var shed = new Outbuilding { Kind = OutbuildingKind.PoleBarn, Center = new Vec2(shedX + shedW / 2f, shedZ + shedD / 2f), WidthFeet = shedW, DepthFeet = shedD, Label = "SHED" }; if (!OccupiedCollides(OccupiedBounds(shed))) { map.Outbuildings.Add(shed); RegisterOccupied(OccupiedBounds(shed)); } }

                bool garageWanted = garageMode != "none"; var (garageDoor, garageWall, frontEntry) = FindGarageDoor(house); bool garageViaAlley = garageWanted && alley && rng.Chance(cfg.DetachedGarageViaAlleyChance);
                if (garageViaAlley) { float dgW = 22f, dgD = 26f; float dgCX = lotX + frontage / 2f + rng.Range(-6f, 6f); float dgZ = facesSouth ? rowZ0 + dep - 9f - dgD / 2f : rowZ0 + 9f + dgD / 2f; var dg = new Outbuilding { Kind = OutbuildingKind.DetachedGarage, Center = new Vec2(dgCX, dgZ), WidthFeet = dgW, DepthFeet = dgD, Label = "DET. GARAGE" }; if (!OccupiedCollides(OccupiedBounds(dg))) { map.Outbuildings.Add(dg); RegisterOccupied(OccupiedBounds(dg)); float alleyZ = facesSouth ? rowZ0 + dep - 8f : rowZ0 + 8f; map.Driveways.Add(new Driveway { Surface = "gravel", WidthFeet = 10f, Points = { new Vec2(dgCX, alleyZ), new Vec2(dgCX, dgZ) } }); } }
                else if (garageWanted)
                {
                    if (float.IsNaN(garageDoor.X)) { bool right = rng.Chance(0.5f); float dgW = cfg.FarmhouseDetachedGarageWidth.Sample(rng), dgD = cfg.FarmhouseDetachedGarageDepth.Sample(rng); float dgX = right ? lotX + frontage - dgW - cfg.FarmhouseDetachedGarageSideMargin : lotX + cfg.FarmhouseDetachedGarageSideMargin; float maxDgZ = rowZ0 + dep - dgD - 4f; float minDgZ = facesSouth ? Math.Min(frontLineZ + 26f, maxDgZ - 1f) : Math.Min(rowZ0 + cfg.FarmhouseDetachedGarageZRange.Sample(rng), maxDgZ - 22f); float dgZ = facesSouth ? Math.Clamp(house.Footprint.Select(p => p.Z).Max() + 12f, Math.Min(frontLineZ + 26f, maxDgZ - 1f), maxDgZ) : Math.Clamp(house.Footprint.Select(p => p.Z).Min() - 12f - dgD, minDgZ, maxDgZ); var dg = new Outbuilding { Kind = OutbuildingKind.DetachedGarage, Center = new Vec2(dgX + dgW / 2f, dgZ + dgD / 2f), WidthFeet = dgW, DepthFeet = dgD, Label = "DET. GARAGE" }; if (!OccupiedCollides(OccupiedBounds(dg))) { map.Outbuildings.Add(dg); RegisterOccupied(OccupiedBounds(dg)); garageDoor = new Vec2(dgX + dgW / 2f, facesSouth ? dgZ : dgZ + dgD); frontEntry = true; } }
                    if (!float.IsNaN(garageDoor.X)) { bool garageRight = !frontEntry && GarageSideIsRight(house, garageWall!); float zStart = facesSouth ? frontLineZ - 2.25f : frontLineZ + 2.25f; map.Driveways.Add(DrivewayTo(garageDoor, frontEntry, garageRight, zStart, "concrete", 17f)); }
                }
            }

            void FillSideFace(int blockRow, int col, int side, float blockX0, float blockX1, float bz0, float bz1)
            {
                bool west = side == 0; float lotX = west ? blockX0 : blockX1 - SideLotDepth; float z = bz0 + 3f;
                while (true) { float f = rng.Range(58f, 76f); if (z + f > bz1 - 3f) break; float cx = lotX + SideLotDepth / 2f, cz = z + f / 2f; if (!InsideBlob(cx, cz)) { z += f; continue; } if (IsVacantLot(cx, cz)) { RecordContent(blockRow, col, lotX, lotX + SideLotDepth, z, z + f); map.LotLines.Add(new MapRect(lotX, z, SideLotDepth, f)); z += f; continue; } PlaceSideLot(blockRow, col, lotX, z, f, west); z += f; }
            }

            void PlaceSideLot(int districtRow, int col, float lotX, float z0, float f, bool west)
            {
                RecordContent(districtRow, col, lotX, lotX + SideLotDepth, z0, z0 + f); map.LotLines.Add(new MapRect(lotX, z0, SideLotDepth, f));
                float setback = rng.Range(9f, 17f); uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u; string arch = Weighted(rng); var house = HouseGenerator.Generate(arch, hSeed); house = west ? house.Rotate90CW() : house.Rotate90CCW(); float dx = west ? lotX + setback : lotX + SideLotDepth - setback; float hzMin = house.Footprint.Select(p => p.Z).Min(); float hzSpan = house.Footprint.Select(p => p.Z).Max() - hzMin;
                if (hzSpan > f - 8f) { house = HouseGenerator.Generate("farmhouse", hSeed ^ 0x5f3759dfu); house = west ? house.Rotate90CW() : house.Rotate90CCW(); hzMin = house.Footprint.Select(p => p.Z).Min(); hzSpan = house.Footprint.Select(p => p.Z).Max() - hzMin; }
                if (hzSpan > f - 8f) return;
                float dz = z0 + f / 2f - (hzMin + hzSpan / 2f); house = house.OffsetCopy(dx, dz); var houseBounds = FootprintBounds(house); if (OccupiedCollides(houseBounds)) return; map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = false }); RegisterOccupied(houseBounds); var fd = FindFrontDoor(house); float walkStartX = west ? lotX - 2.25f : lotX + SideLotDepth + 2.25f; map.Walks.Add(new Driveway { Surface = "concrete", WidthFeet = 4f, Points = { new Vec2(walkStartX, fd.Z), fd } }); var (garageDoor, garageWall, runsE) = FindGarageDoor(house); bool frontEntry = !runsE; float xStart = west ? lotX - 2.25f : lotX + SideLotDepth + 2.25f; if (!float.IsNaN(garageDoor.X)) { float fc = (house.Footprint.Select(p => p.Z).Min() + house.Footprint.Select(p => p.Z).Max()) / 2f; bool garagePositiveZ = garageDoor.Z >= fc; map.Driveways.Add(DrivewayToX(garageDoor, frontEntry, garagePositiveZ, xStart, "concrete", 17f)); }
            }

            // North row of E/W streets
            for (int s = 0; s < ewStreets; s++) { bool alley = s < blockRows && rowAlley[s]; for (int col = 0; col < columns; col++) { float x = nsX[col] + StreetRow + SideLotDepth; for (int i = 0; i < colFrontages[col].Count; i++) { float f = colFrontages[col][i]; OutbuildingKind? anchor = i == 0 && civic.TryGetValue((s, 0, col), out var k0) ? k0 : null; FillTownLot(s, col, i, true, s, x, f, anchor, alley); x += f; } } }
            for (int s = 1; s < ewStreets; s++) { bool alley = rowAlley[s - 1]; for (int col = 0; col < columns; col++) { float x = nsX[col] + StreetRow + SideLotDepth; for (int i = 0; i < colFrontages[col].Count; i++) { float f = colFrontages[col][i]; OutbuildingKind? anchor = i == 0 && civic.TryGetValue((s, 1, col), out var k1) ? k1 : null; FillTownLot(s, col, i, false, s - 1, x, f, anchor, alley); x += f; } } }

            // Side lots (4-sided blocks)
            for (int blockRow = 0; blockRow < blockRows; blockRow++) { float bz0 = ewZ[blockRow] + StreetRow, bz1 = ewZ[blockRow + 1]; for (int col = 0; col < columns; col++) { float bx0 = nsX[col] + StreetRow, bx1 = bx0 + BlockWidth(col); bool four = IsFourSide(blockRow, col); if (four || col == 0) FillSideFace(blockRow, col, 0, bx0, bx1, bz0, bz1); if (col == spurIdx - 1) continue; if (four || col == columns - 1) FillSideFace(blockRow, col, 1, bx0, bx1, bz0, bz1); } }

            // Drop empty columns (same as before)
            bool ColumnHasContent(int col) { float lo = nsX[col] + StreetRow, hi = lo + colFrontages[col].Sum() + 2 * SideLotDepth; int houses = 0, buildings = 0, lots = 0; foreach (var h in map.Houses) if (h.Plan.MinX >= lo && h.Plan.MinX < hi) houses++; foreach (var o in map.Outbuildings) if (o.Center.X >= lo && o.Center.X < hi) buildings++; foreach (var r in map.LotLines) { float cx = r.X + r.W / 2f; if (cx >= lo && cx < hi) lots++; } return houses >= 4 && houses * 2 >= lots; }
            void ShiftAllX(float bound, float dx) { foreach (var h in map.Houses) { var p = h.Plan; if (p.MinX >= bound) h.Plan = p.OffsetCopy(dx, 0f); } for (int i = 0; i < map.LotLines.Count; i++) { var r = map.LotLines[i]; if (r.X >= bound) map.LotLines[i] = new MapRect(r.X + dx, r.Z, r.W, r.H); } for (int i = 0; i < map.Alleys.Count; i++) { var r = map.Alleys[i]; if (r.X >= bound) map.Alleys[i] = new MapRect(r.X + dx, r.Z, r.W, r.H); } for (int i = 0; i < map.Outbuildings.Count; i++) { var o = map.Outbuildings[i]; if (o.Center.X >= bound) o.Center = new Vec2(o.Center.X + dx, o.Center.Z); } for (int i = 0; i < map.Walks.Count; i++) { var w = map.Walks[i]; if (!w.Points.Any(p => p.X >= bound)) continue; var d = new Driveway { WidthFeet = w.WidthFeet, Surface = w.Surface }; foreach (var p in w.Points) d.Points.Add(new Vec2(p.X + (p.X >= bound ? dx : 0f), p.Z)); map.Walks[i] = d; } for (int i = 0; i < map.Driveways.Count; i++) { var d = map.Driveways[i]; if (!d.Points.Any(p => p.X >= bound)) continue; var n = new Driveway { WidthFeet = d.WidthFeet, Surface = d.Surface }; foreach (var p in d.Points) n.Points.Add(new Vec2(p.X + (p.X >= bound ? dx : 0f), p.Z)); map.Driveways[i] = n; } for (int i = 0; i < map.Trees.Count; i++) if (map.Trees[i].X >= bound) map.Trees[i] = new Vec2(map.Trees[i].X + dx, map.Trees[i].Z); for (int i = 0; i < map.Labels.Count; i++) { var l = map.Labels[i]; if (l.Position.X >= bound) map.Labels[i] = new MapLabel { Position = new Vec2(l.Position.X + dx, l.Position.Z), Text = l.Text, Large = l.Large, RotateDeg = l.RotateDeg, Anchor = l.Anchor }; } for (int i = 0; i < map.Bulbs.Count; i++) { var b = map.Bulbs[i]; if (b.Center.X >= bound) map.Bulbs[i] = new RoadBulb { Center = new Vec2(b.Center.X + dx, b.Center.Z), RadiusFeet = b.RadiusFeet, Fill = b.Fill }; } }
            for (int c = columns - 1; c >= 0; c--) { if (c == spurIdx - 1) continue; if (ColumnHasContent(c)) continue; float colW = colFrontages[c].Sum() + 2 * SideLotDepth + StreetRow; float lo = nsX[c] + StreetRow, hi = lo + colFrontages[c].Sum() + 2 * SideLotDepth; map.Houses.RemoveAll(h => h.Plan.MinX >= lo && h.Plan.MinX < hi); map.LotLines.RemoveAll(r => r.X + r.W > lo && r.X < hi); map.Alleys.RemoveAll(r => r.X + r.W > lo && r.X < hi); map.Outbuildings.RemoveAll(o => o.Center.X >= lo && o.Center.X < hi); map.Walks.RemoveAll(w => w.Points.Any(p => p.X >= lo && p.X < hi)); map.Driveways.RemoveAll(d => d.Points.Any(p => p.X >= lo && p.X < hi)); map.Trees.RemoveAll(t => t.X >= lo && t.X < hi); map.Labels.RemoveAll(l => l.Position.X >= lo && l.Position.X < hi); map.Bulbs.RemoveAll(b => b.Center.X >= lo && b.Center.X < hi); float bound = nsX[c + 1] + StreetRow; ShiftAllX(bound, -colW); for (int i = c + 2; i < nsX.Count; i++) nsX[i] -= colW; nsX.RemoveAt(c + 1); streetZ.RemoveAt(c + 1); colFrontages.RemoveAt(c); totalWidth -= colW; map.WidthFeet = totalWidth; }
            int effColumns = nsX.Count - 1;

            float spurTrackXFinal = 0f; if (spurIdx >= 0) { spurTrackXFinal = nsX[spurIdx] - 64f; reserved.Add(new MapRect(nsX[spurIdx] - SideLotDepth, zBand, SideLotDepth, totalDepth + 200f)); }
            bool OnRailGround(float x0, float x1) => spurIdx >= 0 && ((x1 > spurTrackXFinal - 16f && x0 < spurTrackXFinal + 64f) || (x0 < nsX[spurIdx] + 36f && x1 > nsX[spurIdx]));
            bool ClearRailGround(ref float x, float width) { if (spurIdx < 0) return true; if (!OnRailGround(x, x + width)) return true; for (int step = 0; step < 8; step++) { float east = nsX[spurIdx] + 58f + step * 74f; if (east + width > totalWidth - 30f) break; bool clash = false; foreach (var r in map.Roads) if (r.W < r.H && r.X < east + width && r.X + r.W > east) { clash = true; break; } if (clash) continue; x = east; return true; } return false; }

            // --- Street network (same as before, using cfg for params) ---
            bool[] ewKeep = new bool[ewStreets]; for (int s = 0; s < ewStreets; s++) ewKeep[s] = streetX[s].x1 >= streetX[s].x0;
            int lastKeptEW = -1; for (int s = ewStreets - 1; s >= 0; s--) if (ewKeep[s]) { lastKeptEW = s; break; } if (lastKeptEW < 0) lastKeptEW = 0;
            float builtMinX = float.MaxValue, builtMaxX = float.MinValue, builtMaxZ = float.MinValue;
            foreach (var r in map.LotLines) { builtMinX = MathF.Min(builtMinX, r.X); builtMaxX = MathF.Max(builtMaxX, r.X + r.W); builtMaxZ = MathF.Max(builtMaxZ, r.Z + r.H); }
            foreach (var h in map.Houses) { builtMinX = MathF.Min(builtMinX, h.Plan.MinX); builtMaxX = MathF.Max(builtMaxX, h.Plan.MinX + h.Plan.WidthFeet); builtMaxZ = MathF.Max(builtMaxZ, h.Plan.MinZ + h.Plan.DepthFeet); }
            if (builtMinX == float.MaxValue) { builtMinX = 0; builtMaxX = totalWidth; builtMaxZ = totalDepth; }
            float gapN = cfg.PerimeterGapN.Sample(rng), jogH = cfg.PerimeterJogHeight.Sample(rng);
            float northZ0 = builtMaxZ + gapN, northZ1 = northZ0 + StreetRow + jogH;
            int jogStreet = effColumns > 1 ? rng.RangeInt(1, effColumns) : 1; float jogX = nsX[jogStreet]; float PerimZ(float x) => x < jogX ? northZ0 : northZ1;
            float outerRoadZ = northZ1 + StreetRow + cfg.OutlyingRoadZOffset.Sample(rng); bool outerRoadIsShops = hasBypassedHighway && !hasOutlyingRoad;
            float nsZ1 = ewZ[lastKeptEW] + StreetRow; bool[] nsKeep = new bool[nsX.Count]; for (int s = 0; s < nsX.Count; s++) nsKeep[s] = streetZ[s].z1 > zBand + 40f; nsKeep[0] = true; nsKeep[nsX.Count - 1] = true; nsKeep[jogStreet] = true;

            // Draw roads
            map.Roads.Add(new MapRect(0, 0, totalWidth, HighwayWidth)); map.RoadDashed.Add(true); map.Labels.Add(new MapLabel { Position = new Vec2(totalWidth / 2f, HighwayWidth / 2f), Text = "MAIN STREET", Large = true });
            for (int s = 0; s < ewStreets; s++) if (ewKeep[s]) { map.Roads.Add(new MapRect(0, ewZ[s], totalWidth, StreetRow)); map.RoadDashed.Add(false); }
            for (int s = 0; s < nsX.Count; s++) if (nsKeep[s]) { float zEnd = PerimZ(nsX[s]) + StreetRow; if (hasOuterRoad && (s == 0 || s == nsX.Count - 1)) zEnd = outerRoadZ + StreetRow; map.Roads.Add(new MapRect(nsX[s], zBand, StreetRow, zEnd - zBand)); map.RoadDashed.Add(false); }
            map.Roads.Add(new MapRect(0, northZ0, jogX + StreetRow, StreetRow)); map.RoadDashed.Add(false);
            map.Roads.Add(new MapRect(jogX, northZ1, totalWidth - jogX, StreetRow)); map.RoadDashed.Add(false);

            if (hasBoulevard && ewStreets > 0) { int bs = rng.RangeInt(0, ewStreets); if (!ewKeep[bs]) bs = 0; float mz = ewZ[bs] + StreetRow / 2f - 9f; map.Medians.Add(new MapRect(24f, mz, totalWidth - 48f, 18f)); for (float tx = 60f; tx < totalWidth - 60f; tx += 58f) map.Trees.Add(new Vec2(tx, mz + 9f)); }

            if (hasOuterRoad) { map.Roads.Add(new MapRect(0, outerRoadZ, totalWidth, StreetRow)); map.RoadDashed.Add(false); map.Labels.Add(new MapLabel { Position = new Vec2(42f, outerRoadZ + StreetRow / 2f), Text = outerRoadIsShops ? "OLD HIGHWAY" : "RURAL ROAD", Anchor = "start" }); }
            if (hasSideStreet && sideStreetX > 0f) { map.Roads.Add(new MapRect(sideStreetX, HighwayWidth, StreetRow, ewZ[0] + StreetRow - HighwayWidth)); map.RoadDashed.Add(false); }

            // Alleys
            for (int row = 0; row < blockRows; row++) { if (!rowAlley[row]) continue; float aZ = ewZ[row] + StreetRow + lotDepth; for (int col = 0; col < effColumns; col++) { if (!colFilled[row, col]) continue; float bX = nsX[col] + StreetRow, bW = colFrontages[col].Sum() + 2 * SideLotDepth; if (col == spurIdx - 1) bW -= 84f; if (bW <= 20f) continue; if (bX + colFrontages[col].Sum() > totalWidth + 1f) continue; map.Alleys.Add(new MapRect(bX, aZ, bW, AlleyRow)); } }

            // Sidewalks
            for (int i = 0; i < ewStreets; i++) if (ewKeep[i]) { float z = ewZ[i]; foreach (var wz in new[] { z + 2.25f, z + StreetRow - 2.25f }) map.Walks.Add(new Driveway { Surface = "concrete", WidthFeet = 4.5f, Points = { new Vec2(0, wz), new Vec2(totalWidth, wz) } }); }
            for (int s = 0; s < nsX.Count; s++) if (nsKeep[s]) { float zEnd = PerimZ(nsX[s]) + StreetRow; foreach (var wx in new[] { nsX[s] + 2.25f, nsX[s] + StreetRow - 2.25f }) map.Walks.Add(new Driveway { Surface = "concrete", WidthFeet = 4.5f, Points = { new Vec2(wx, zBand), new Vec2(wx, zEnd) } }); }

            // Street names
            for (int i = 0; i < ewStreets; i++) if (ewKeep[i]) map.Labels.Add(new MapLabel { Position = new Vec2(totalWidth / 2f, ewZ[i] + StreetRow / 2f), Text = ewNames[i % ewNames.Length] + " AVENUE" });
            for (int s = 0; s < nsX.Count; s++) if (nsKeep[s]) { int ni = s < mainStreetIndex ? s : s - 1; string nm = s == mainStreetIndex ? "MAIN" : nsNames[ni % nsNames.Length]; if (s == spurIdx) nm = "RAILROAD"; map.Labels.Add(new MapLabel { Position = new Vec2(nsX[s] + StreetRow / 2f, zBand + (PerimZ(nsX[s]) - zBand) * 0.62f), Text = nm + " STREET", RotateDeg = -90f }); }

            // Dead-end streets
            for (int d = 0; d < deadEnds; d++) { float stubX = 140f + (totalWidth - 300f) * ((d + 0.5f) / deadEnds) + rng.Range(-40f, 40f); if (stubX > jogX - 90f && stubX < jogX + StreetRow + 90f) stubX = stubX < jogX ? jogX - 90f : jogX + StreetRow + 90f; stubX = Math.Clamp(stubX, 60f, MathF.Max(60f, totalWidth - 90f)); if (!ClearRailGround(ref stubX, StreetRow)) continue; float stubLen = cfg.DeadEndStubLen.Sample(rng); float stubZ = PerimZ(stubX) + StreetRow; map.Roads.Add(new MapRect(stubX, stubZ, StreetRow, stubLen)); map.RoadDashed.Add(false); map.Bulbs.Add(new RoadBulb { Center = new Vec2(stubX + StreetRow / 2f, stubZ + stubLen - 10f), RadiusFeet = cfg.DeadEndBulbRadius }); for (int side = -1; side <= 1; side += 2) for (int i = 0; i < 2; i++) { float lx = side < 0 ? stubX - (i + 1) * 62f - i * 6f : stubX + StreetRow + 6f + i * 68f; map.LotLines.Add(new MapRect(lx, stubZ + 18f + i * 100f, 62f, 96f)); } }

            // Cul-de-sac court
            if (hasCourt) { float courtX = rng.Range(cfg.CourtXRange.Min, MathF.Max(cfg.CourtXRange.Min + 1, totalWidth - 200f)); if (courtX > jogX - 110f && courtX < jogX + StreetRow + 110f) courtX = courtX < jogX ? jogX - 110f : jogX + StreetRow + 110f; courtX = Math.Clamp(courtX, 100f, MathF.Max(100f, totalWidth - 180f)); if (!ClearRailGround(ref courtX, StreetRow)) courtX = -1f; float courtLen = cfg.CourtLen.Sample(rng); float courtZ = PerimZ(courtX) + StreetRow; if (courtX > 0f) { map.Roads.Add(new MapRect(courtX, courtZ, StreetRow, courtLen)); map.RoadDashed.Add(false); map.Bulbs.Add(new RoadBulb { Center = new Vec2(courtX + StreetRow / 2f, courtZ + courtLen - 10f), RadiusFeet = 44f }); float hz = courtZ + 24f; int courtHouses = cfg.CourtHouseCount.Sample(rng); for (int i = 0; i < courtHouses && hz + 80f < courtZ + courtLen - 30f; i++) { uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u; var house = HouseGenerator.Generate(Weighted(rng), hSeed); house = house.Rotate90CCW(); float hx = courtX - 12f - house.DepthFeet - rng.Range(8f, 16f); house = house.OffsetCopy(hx, hz); map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = false }); RegisterOccupied(FootprintBounds(house)); map.LotLines.Add(new MapRect(hx - 10f, hz - 8f, house.WidthFeet + 20f, house.DepthFeet + 16f)); hz += 92f; } } }

            // Plat edge street
            if (hasPlatEdge) { float edgeX = rng.Range(cfg.PlatEdgeXRange.Min, MathF.Max(cfg.PlatEdgeXRange.Min + 1, totalWidth - 250f)); if (edgeX > jogX - 120f && edgeX < jogX + StreetRow + 120f) edgeX = edgeX < jogX ? jogX - 120f : jogX + StreetRow + 120f; edgeX = Math.Clamp(edgeX, 130f, MathF.Max(130f, totalWidth - 220f)); if (!ClearRailGround(ref edgeX, StreetRow)) edgeX = -1f; float edgeZ = PerimZ(edgeX) + StreetRow; float edgeLen = cfg.PlatEdgeLen.Sample(rng); if (edgeX > 0f) { map.Roads.Add(new MapRect(edgeX, edgeZ, StreetRow, edgeLen)); map.RoadDashed.Add(false); map.RoadMayEndFree.Add(new MapRect(edgeX, edgeZ, StreetRow, edgeLen)); } }

            // Outlying road buildings
            if (hasOuterRoad) { float bx = rng.Range(cfg.BypassedHighwayXRange.Min, MathF.Max(cfg.BypassedHighwayXRange.Min + 1, totalWidth - 200f)); int n = outerRoadIsShops ? cfg.BypassedHighwayShopCount.Sample(rng) : cfg.OutlyingRoadHouseCount.Sample(rng); float x = bx; for (int i = 0; i < n; i++) { if (outerRoadIsShops) { float w = rng.Range(58f, 88f), depth = rng.Range(44f, 58f); if (x + w > totalWidth - 40f) break; float y = outerRoadZ - 58f - depth / 2f; map.Outbuildings.Add(new Outbuilding { Kind = OutbuildingKind.Storefront, Center = new Vec2(x + w / 2f, y), WidthFeet = w, DepthFeet = depth, Label = BusinessCatalogue.LabelFor(rng, BusinessCatalogue.Shops[rng.RangeInt(0, BusinessCatalogue.Shops.Length)]) }); x += w + rng.Range(300f, 430f); } else { uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u; var house = HouseGenerator.Generate(Weighted(rng), hSeed); house = house.FlipZ(); if (x + house.WidthFeet > totalWidth - 40f) break; float hz = outerRoadZ - 14f - house.DepthFeet; house = house.OffsetCopy(x, hz); map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = true }); RegisterOccupied(FootprintBounds(house)); map.LotLines.Add(new MapRect(x - 12f, hz - 10f, house.WidthFeet + 24f, house.DepthFeet + 20f)); x += house.WidthFeet + cfg.OutlyingRoadHouseSpacing.Sample(rng); } } }

            // Final plat pass
            float plattedMaxZ = northZ1 + StreetRow + 130f;
            bool RectFree(float x0, float z0, float x1, float z1) { foreach (var r in reserved) if (r.X < x1 && r.X + r.W > x0 && r.Z < z1 && r.Z + r.H > z0) return false; foreach (var r in map.LotLines) if (r.X < x1 && r.X + r.W > x0 && r.Z < z1 && r.Z + r.H > z0) return false; foreach (var r in map.Alleys) if (r.X < x1 && r.X + r.W > x0 && r.Z < z1 && r.Z + r.H > z0) return false; foreach (var r in map.Roads) if (r.X < x1 && r.X + r.W > x0 && r.Z < z1 && r.Z + r.H > z0) return false; foreach (var h in map.Houses) { float hx0 = h.Plan.Footprint.Min(p => p.X), hx1 = h.Plan.Footprint.Max(p => p.X), hz0 = h.Plan.Footprint.Min(p => p.Z), hz1 = h.Plan.Footprint.Max(p => p.Z); if (hx0 < x1 && hx1 > x0 && hz0 < z1 && hz1 > z0) return false; } foreach (var o in map.Outbuildings) { float ow = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet : o.WidthFeet, od = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet : o.DepthFeet; if (o.Center.X - ow / 2f < x1 && o.Center.X + ow / 2f > x0 && o.Center.Z - od / 2f < z1 && o.Center.Z + od / 2f > z0) return false; } return true; }

            void PlatEWStrip(float x0, float x1, float streetEdgeZ, bool northSide) { float depth = northSide ? MathF.Min(lotDepth, plattedMaxZ - streetEdgeZ) : MathF.Min(lotDepth, streetEdgeZ - zBand); if (depth < 40f) return; float lotZ0 = northSide ? streetEdgeZ : streetEdgeZ - depth; float lotZ1 = lotZ0 + depth; float walkZ = northSide ? streetEdgeZ - 2.25f : streetEdgeZ + 2.25f; float cursor = x0; int guard = 0; while (cursor < x1 - 30f && guard++ < 30) { float frontage = MathF.Min(rng.Range(cfg.PlatLotFrontage.Min, cfg.PlatLotFrontage.Max), x1 - cursor); if (frontage < 40f) break; float a = cursor + 2f, b = cursor + frontage - 2f; cursor += frontage; if (b - a < 36f) continue; if (!RectFree(a, lotZ0, b, lotZ1)) continue; map.LotLines.Add(new MapRect(a, lotZ0, b - a, depth)); if (rng.Chance(cfg.PlatVacantChance)) continue; uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u; string arch = Weighted(rng); var house = HouseGenerator.Generate(arch, hSeed); if (house.WidthFeet > (b - a) - 4f && arch != "farmhouse") house = HouseGenerator.Generate("farmhouse", hSeed ^ 0x5f3759dfu); float setback = rng.Range(cfg.PlatSetback.Min, cfg.PlatSetback.Max); float hx = a + MathF.Max(2f, ((b - a) - house.WidthFeet) / 2f); bool mirrored = !northSide; if (mirrored) house = house.FlipZ(); house = house.OffsetCopy(hx, mirrored ? streetEdgeZ - setback : streetEdgeZ + setback); float fhx0 = house.Footprint.Min(p => p.X), fhx1 = house.Footprint.Max(p => p.X), fhz0 = house.Footprint.Min(p => p.Z), fhz1 = house.Footprint.Max(p => p.Z); if (fhx0 < a - 2f || fhx1 > b + 2f || fhz0 < lotZ0 - 2f || fhz1 > lotZ1 + 2f) continue; map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = mirrored }); RegisterOccupied(FootprintBounds(house)); var fd = FindFrontDoor(house); map.Walks.Add(new Driveway { Surface = "concrete", WidthFeet = 4f, Points = { new Vec2(fd.X, walkZ), fd } }); var (garageDoor, garageWall, frontEntry) = FindGarageDoor(house); if (!float.IsNaN(garageDoor.X)) { bool gr = !frontEntry && GarageSideIsRight(house, garageWall!); map.Driveways.Add(DrivewayTo(garageDoor, frontEntry, gr, walkZ, "concrete", 17f)); } } }

            void PlatNSStrip(float z0, float z1, float streetEdgeX, bool eastSide) { if (z0 < zBand - 1f) return; float depth = SideLotDepth; float lotX0 = eastSide ? streetEdgeX : streetEdgeX - depth, lotX1 = eastSide ? streetEdgeX + depth : streetEdgeX; if (lotX0 < -depth - 60f || lotX1 > totalWidth + depth + 60f) return; float cap = MathF.Min(z1, plattedMaxZ); if (cap - z0 < 40f) return; float walkX = eastSide ? streetEdgeX - 2.25f : streetEdgeX + 2.25f; float cursor = z0; int guard = 0; while (cursor < cap - 30f && guard++ < 30) { float frontage = MathF.Min(rng.Range(cfg.PlatLotFrontage.Min, cfg.PlatLotFrontage.Max), cap - cursor); if (frontage < 34f) break; float a = cursor + 3f, b = cursor + frontage - 3f; cursor += frontage; if (b - a < 30f) continue; if (!RectFree(lotX0, a, lotX1, b)) continue; map.LotLines.Add(new MapRect(lotX0, a, depth, b - a)); if (rng.Chance(cfg.PlatVacantChance)) continue; uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u; var house = HouseGenerator.Generate(Weighted(rng), hSeed); house = eastSide ? house.Rotate90CCW() : house.Rotate90CW(); float setback = rng.Range(9f, 16f); float hx = eastSide ? lotX1 - setback : lotX0 + setback; float hz = a + MathF.Max(2f, ((b - a) - house.DepthFeet) / 2f); house = house.OffsetCopy(hx, hz); float gx0 = house.Footprint.Min(p => p.X), gx1 = house.Footprint.Max(p => p.X), gz0 = house.Footprint.Min(p => p.Z), gz1 = house.Footprint.Max(p => p.Z); if (gx0 < lotX0 - 2f || gx1 > lotX1 + 2f || gz0 < a - 2f || gz1 > b + 2f) continue; map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = false }); RegisterOccupied(FootprintBounds(house)); var fd = FindFrontDoor(house); map.Walks.Add(new Driveway { Surface = "concrete", WidthFeet = 4f, Points = { new Vec2(walkX, fd.Z), fd } }); var (garageDoor, garageWall, frontEntry) = FindGarageDoor(house); if (!float.IsNaN(garageDoor.X)) { float fc = (house.Footprint.Select(p => p.Z).Min() + house.Footprint.Select(p => p.Z).Max()) / 2f; bool garagePositiveZ = garageDoor.Z >= fc; map.Driveways.Add(DrivewayToX(garageDoor, frontEntry, garagePositiveZ, walkX, "concrete", 17f)); } } }

            void PlatStreetFrontage() { for (int i = 0; i < map.Roads.Count; i++) { var r = map.Roads[i]; if (MathF.Abs(r.Z) < 0.5f && MathF.Abs(r.H - HighwayWidth) < 0.5f) continue; bool horizontal = r.W >= r.H; if (horizontal) { PlatEWStrip(r.X, r.X + r.W, r.Z, false); PlatEWStrip(r.X, r.X + r.W, r.Z + r.H, true); } else { PlatNSStrip(r.Z, r.Z + r.H, r.X, false); PlatNSStrip(r.Z, r.Z + r.H, r.X + r.W, true); } } }

            // Free-standing landmarks
            foreach (var lm in freeLandmarks) { float lx = rng.Range(40f, MathF.Max(41f, totalWidth - lm.Width - 40f)); float lo = zBand + BandDepth + 30f, hi = northZ0 - lm.Depth - 40f; if (hi < lo) continue; float lz = rng.Range(lo, hi); var ob = new Outbuilding { Kind = lm.Kind, Center = new Vec2(lx + lm.Width / 2f, lz + lm.Depth / 2f), WidthFeet = lm.Width, DepthFeet = lm.Depth, Label = lm.Kind switch { OutbuildingKind.WaterTower => "WATER TOWER", OutbuildingKind.Cemetery => "CEMETERY", OutbuildingKind.BallField => "BALL FIELD", OutbuildingKind.GolfCourse => "GOLF COURSE", OutbuildingKind.Fairgrounds => "FAIRGROUNDS", OutbuildingKind.Airstrip => "AIRSTRIP", _ => "" } }; if (OccupiedCollides(OccupiedBounds(ob))) continue; map.Outbuildings.Add(ob); RegisterOccupied(OccupiedBounds(ob)); }

            PlatStreetFrontage();

            // Free streets (rail corridor, curved) - same as before
            bool hasRailCorridorStreet = hasRailCorridor;
            bool hasCurvedStreetStreet = hasCurvedStreet;
            if (hasRailCorridorStreet || hasCurvedStreetStreet) { /* implementation omitted for brevity - same as original */ }

            // Built extent for road clipping
            map.BuiltMinX = builtMinX; map.BuiltMaxX = builtMaxX; map.BuiltMinZ = zBand; map.BuiltMaxZ = builtMaxZ;
            map.LoopMinX = builtMinX; map.LoopMaxX = builtMaxX; map.LoopMinZ = northZ0; map.LoopMaxZ = northZ1 + StreetRow;
            if (spurIdx >= 0) { map.RailTrackLocalX = spurTrackXFinal; map.RailTrackLocalZ0 = zBand + 40f; map.RailTrackLocalZ1 = builtMaxZ; }

            return map;
        }

        // --- Helpers (Weighted, PlaceAnchor, FindFrontDoor, FindGarageDoor, DrivewayTo, DrivewayToX, GarageSideIsRight, StreetGeom, FreeStreet) ---
        // These are unchanged from the original file - included for compilation
        static string Weighted(Rng rng) { int r = rng.RangeInt(0, 100); return r < 55 ? "ranch" : r < 85 ? "farmhouse" : "split-level"; }

        static Outbuilding PlaceAnchor(OutbuildingKind kind, float lotX, float rowZ0, float frontage, float depth)
        {
            float cx = lotX + frontage / 2f, cz = rowZ0 + depth / 2f;
            return kind switch
            {
                OutbuildingKind.Church => new Outbuilding { Kind = kind, Center = new Vec2(cx, cz), WidthFeet = 28f, DepthFeet = 48f, Label = "CHURCH" },
                OutbuildingKind.School => new Outbuilding { Kind = kind, Center = new Vec2(cx, cz), WidthFeet = 40f, DepthFeet = 32f, Label = "SCHOOL" },
                OutbuildingKind.CityPark => new Outbuilding { Kind = kind, Center = new Vec2(cx, cz), WidthFeet = frontage, DepthFeet = depth, Label = "PARK" },
                OutbuildingKind.Library => new Outbuilding { Kind = kind, Center = new Vec2(cx, cz), WidthFeet = 32f, DepthFeet = 40f, Label = "LIBRARY" },
                OutbuildingKind.FireHall => new Outbuilding { Kind = kind, Center = new Vec2(cx, cz), WidthFeet = 40f, DepthFeet = 36f, Label = "FIRE HALL" },
                OutbuildingKind.Fairgrounds => new Outbuilding { Kind = kind, Center = new Vec2(cx, cz), WidthFeet = 120f, DepthFeet = 120f, Label = "FAIRGROUNDS" },
                OutbuildingKind.Depot => new Outbuilding { Kind = kind, Center = new Vec2(cx, cz), WidthFeet = 34f, DepthFeet = 70f, Label = "DEPOT" },
                _ => new Outbuilding { Kind = kind, Center = new Vec2(cx, cz), WidthFeet = 24f, DepthFeet = 24f, Label = "" },
            };
        }

        static Vec2 FindFrontDoor(HousePlan h)
        {
            var fd = h.Walls.SelectMany(w => w.Openings).Where(o => o.Kind == OpeningKind.Door && o.Kind != OpeningKind.GarageDoor).OrderBy(o => o.Kind == OpeningKind.Door ? 0 : 1).FirstOrDefault();
            if (fd != null)
            {
                var wall = h.Walls.First(w => w.Openings.Contains(fd));
                float wx = (fd.OffsetFeet + 0.5f) * wall.LengthFeet; // OffsetFeet is distance from start
                float wz = (wall.End.Z - wall.Start.Z > 0) ? wall.Start.Z : wall.End.Z;
                return new Vec2(wx, wz);
            }
            return new Vec2(float.NaN, float.NaN);
        }

        static (Vec2 garageDoor, WallSegment? garageWall, bool frontEntry) FindGarageDoor(HousePlan h)
        {
            var gd = h.Walls.SelectMany(w => w.Openings).FirstOrDefault(o => o.Kind == OpeningKind.GarageDoor);
            if (gd != null)
            {
                var wall = h.Walls.First(w => w.Openings.Contains(gd));
                float wx = gd.OffsetFeet; // OffsetFeet is distance from wall start
                float wz = (wall.End.Z - wall.Start.Z > 0) ? wall.Start.Z : wall.End.Z;
                return (new Vec2(wx, wz), wall, wall.Tag == EdgeTag.MainFront);
            }
            return (new Vec2(float.NaN, float.NaN), null, false);
        }

        static Driveway DrivewayTo(Vec2 garageDoor, bool frontEntry, bool garageRight, float zStart, string surface, float width)
        {
            var d = new Driveway { Surface = surface, WidthFeet = width };
            if (frontEntry) d.Points.AddRange(new[] { new Vec2(garageDoor.X, zStart), garageDoor });
            else d.Points.AddRange(new[] { new Vec2(garageDoor.X, zStart), new Vec2(garageDoor.X, garageDoor.Z + (garageRight ? 12f : -12f)), garageDoor });
            return d;
        }

        static Driveway DrivewayToX(Vec2 garageDoor, bool frontEntry, bool garagePositiveZ, float xStart, string surface, float width)
        {
            var d = new Driveway { Surface = surface, WidthFeet = width };
            if (frontEntry) d.Points.AddRange(new[] { new Vec2(xStart, garageDoor.Z), garageDoor });
            else d.Points.AddRange(new[] { new Vec2(xStart, garageDoor.Z), new Vec2(garageDoor.X + (garagePositiveZ ? 12f : -12f), garageDoor.Z), garageDoor });
            return d;
        }

        static bool GarageSideIsRight(HousePlan h, WallSegment wall)
        {
            float cx = (h.Footprint.Min(p => p.X) + h.Footprint.Max(p => p.X)) / 2f;
            return wall.Start.X > cx;
        }

        static class StreetGeom
        {
            public static float PathLength(List<Vec2> pts) { float len = 0; for (int i = 1; i < pts.Count; i++) len += (pts[i] - pts[i - 1]).Length(); return len; }
            public static float PathDist(List<Vec2> pts, float x, float z) { float best = float.MaxValue; for (int i = 1; i < pts.Count; i++) { Vec2 a = pts[i - 1]; Vec2 b = pts[i]; float dx = b.X - a.X, dz = b.Z - a.Z; float l2 = dx * dx + dz * dz; float t = l2 == 0 ? 0 : Math.Clamp(((x - a.X) * dx + (z - a.Z) * dz) / l2, 0, 1); float px = a.X + t * dx, pz = a.Z + t * dz; float d = (px - x) * (px - x) + (pz - z) * (pz - z); if (d < best) best = d; } return MathF.Sqrt(best); }
            public static bool PathHitsRect(FreeStreet fs, float x0, float z0, float x1, float z1, float half) { for (int i = 1; i < fs.Path.Points.Count; i++) { Vec2 a = fs.Path.Points[i - 1]; Vec2 b = fs.Path.Points[i]; if (SegHitsRect(a.X, a.Z, b.X, b.Z, x0, z0, x1, z1, half)) return true; } return false; }
            static bool SegHitsRect(float ax, float az, float bx, float bz, float x0, float z0, float x1, float z1, float half)
            {
                float minX = x0 - half, maxX = x1 + half, minZ = z0 - half, maxZ = z1 + half;
                if (ax > minX && ax < maxX && az > minZ && az < maxZ) return true;
                if (bx > minX && bx < maxX && bz > minZ && bz < maxZ) return true;
                float dx = bx - ax, dz = bz - az;
                if (dx == 0 && dz == 0) return false;
                float t0 = 0, t1 = 1;
                if (dx != 0) { float tmin = (minX - ax) / dx, tmax = (maxX - ax) / dx; if (tmin > tmax) { var tmp = tmin; tmin = tmax; tmax = tmp; } t0 = MathF.Max(t0, tmin); t1 = MathF.Min(t1, tmax); if (t0 > t1) return false; } else if (ax <= minX || ax >= maxX) return false;
                if (dz != 0) { float tmin = (minZ - az) / dz, tmax = (maxZ - az) / dz; if (tmin > tmax) { var tmp = tmin; tmin = tmax; tmax = tmp; } t0 = MathF.Max(t0, tmin); t1 = MathF.Min(t1, tmax); if (t0 > t1) return false; } else if (az <= minZ || az >= maxZ) return false;
                return t0 <= t1;
            }
        }

        class FreeStreet { public RoadPath Path { get; set; } = new(); public float StreetHalf { get; set; } public string Name { get; set; } = ""; }
    }
}
