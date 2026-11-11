using System;
using System.Collections.Generic;
using System.Linq;

namespace SpawnGuide
{

/// <summary>
/// A whole small town, seeded and reproducible. Calibrated against Lyon County, IA
/// references (George, Craig, Struble, Boyden, Hull):
///   - commercial strip on the highway along the SOUTH edge (main street = the highway)
///   - cardinal grid inside; blocks ~200–330 ft; lots ~50–80 ft frontage
///   - civic anchors on the grid: school, church(es), parks, grain elevator
///   - heavy tree canopy; abrupt end into fields on all sides
/// Hamlet preset implemented first; Boyden/Hull are parameter bumps to come.
/// Town SIZE is deliberately decoupled from spawn FREQUENCY — the town field
/// (how often hamlets/towns appear per 10/30/50/100 miles) is NOT here yet.
/// Lots face the E–W streets only in this version (N–S street faces get no lots yet).
/// Alleys are decided per block ROW (all columns in a row share the decision) so the
/// street spacing stays uniform within the row.
/// </summary>
public static class SmallTownGenerator
{
    const float HighwayWidth = 66f;   // section-line highway ROW (HWY 18 pattern)
    const float StreetRow = 36f;      // residential grid street ROW
    const float AlleyRow = 16f;
    const float BlockLotDepth = 122f; // back-to-back lot rows inside a block
    const float SideLotDepth = 80f;   // depth of the E/W-facing lots in a 4-sided block
    const float FourSideChance = 0.72f; // most blocks get houses on all four sides
    const float BandDepth = 175f;     // commercial band between highway and first grid street

    sealed class TownSize
    {
        public int Columns;       // block columns (N–S street count = Columns + 1)
        public int EWStreets;     // E–W grid streets (block rows = EWStreets - 1)
        public int LotsMin, LotsMax;
        public int CommercialPads;
        public string Label = "";
    }

    static readonly Dictionary<string, TownSize> Sizes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Hamlet: George/Craig/Struble class (~0.4 mi). Boyden: medium (~0.7 mi).
        // Hull: large (~1.1 mi — a true 2-mile Hull-class would make unusable SVGs).
        ["hamlet"] = new TownSize { Columns = 5, EWStreets = 4, LotsMin = 3, LotsMax = 4, CommercialPads = 3, Label = "HAMLET" },
        ["boyden"] = new TownSize { Columns = 9, EWStreets = 6, LotsMin = 3, LotsMax = 4, CommercialPads = 5, Label = "MEDIUM" },
        ["hull"] = new TownSize { Columns = 15, EWStreets = 7, LotsMin = 3, LotsMax = 4, CommercialPads = 7, Label = "LARGE" },
    };

    /// <param name="detail">false → quiet annotations (natural-size embed). true → all captions on (zoom viewer's detail layer).</param>
    public static MapPlan Generate(uint seed, string sizeName, bool detail = false)
    {
        if (!Sizes.TryGetValue(sizeName, out var size))
            throw new ArgumentException($"unknown town size '{sizeName}' (have: {string.Join(", ", Sizes.Keys)})");

        var rng = new Rng(seed);
        float lotDepth = BlockLotDepth + rng.Range(-6f, 6f);
        int blockRows = size.EWStreets - 1;

        // Per-block-row alley decision (row-level so street spacing is uniform per row).
        var rowAlley = new bool[blockRows];
        for (int r = 0; r < blockRows; r++) rowAlley[r] = rng.Chance(0.55f);
        float BlockDepth(int row) => 2 * lotDepth + (rowAlley[row] ? AlleyRow : 0f);

        string townName = rng.Pick(new[] { "CEDAR BLUFF", "TWIN GROVE", "MARBLE JUNCTION", "PRAIRIE CENTER", "HALSTEAD", "NEW HOPE" });
        string[] ewNames = { "IOWA", "MICHIGAN", "MINNESOTA", "DAKOTA", "CALUMET", "OHIO" };
        string[] nsNames = { "DIVISION", "LINCOLN", "WASHINGTON", "HARRIS", "WILLIAMS", "CHESTNUT", "BURROUGHS", "ADA", "PARK", "ELEANOR", "VINE", "CHELSEA", "STONE", "HUGH", "LOCUST", "DELTA" };
        int mainStreetIndex = size.Columns / 2; // N–S street index; MAIN sits mid-town

        // --- column plan: shared lot frontages per column so facing rows align.
        var colFrontages = new List<float>[size.Columns];
        for (int c = 0; c < size.Columns; c++)
        {
            int n = rng.RangeInt(size.LotsMin, size.LotsMax + 1);
            colFrontages[c] = new List<float>();
            for (int i = 0; i < n; i++) colFrontages[c].Add(rng.Range(55f, 80f));
        }
        float BlockWidth(int col) => colFrontages[col].Sum() + 2 * SideLotDepth;
        float totalWidth = StreetRow * (size.Columns + 1);
        for (int c = 0; c < size.Columns; c++) totalWidth += BlockWidth(c);

        var nsX = new float[size.Columns + 1];
        {
            float x = 0f;
            for (int s = 0; s <= size.Columns; s++)
            {
                nsX[s] = x;
                if (s < size.Columns) x += StreetRow + BlockWidth(s);
            }
        }

        // Per (blockRow, col): 4-sided (side lots on N–S streets) or 2-sided (E–W faces only).
        var fourSide = new Dictionary<(int row, int col), bool>();
        bool IsFourSide(int blockRow, int col)
        {
            var key = (blockRow, col);
            if (!fourSide.TryGetValue(key, out bool has))
            {
                has = rng.Chance(FourSideChance);
                fourSide[key] = has;
            }
            return has;
        }


        // --- seeded blob boundary: the town is an irregular silhouette, never a rectangle.
        float blobCX = 0f, blobCZ = 0f, blobRX = 1f, blobRZ = 1f;
        int hb1 = 0, hb2 = 0;
        float ha1 = 0, ha2 = 0, hp1 = 0, hp2 = 0;
        bool blobReady = false;

        // --- Z layout: highway, commercial band, then the cumulative E–W grid.
        float zBand = HighwayWidth;
        float zFirstStreet = zBand + BandDepth;
        var ewZ = new float[size.EWStreets];
        ewZ[0] = zFirstStreet;
        for (int i = 1; i < size.EWStreets; i++)
            ewZ[i] = ewZ[i - 1] + StreetRow + BlockDepth(i - 1);
        float lastStreetEnd = ewZ[size.EWStreets - 1] + StreetRow;
        float totalDepth = lastStreetEnd + lotDepth;   // final north-edge lot row is the town edge

        blobCX = totalWidth / 2f;
        blobCZ = totalDepth / 2f;
        blobRX = totalWidth / 2f;
        blobRZ = totalDepth / 2f;
        hb1 = rng.RangeInt(2, 5);
        hb2 = rng.RangeInt(5, 9);
        ha1 = rng.Range(0.10f, 0.20f);
        ha2 = rng.Range(0.04f, 0.10f);
        hp1 = rng.Range(0f, MathF.PI * 2f);
        hp2 = rng.Range(0f, MathF.PI * 2f);
        blobReady = true;

        bool InsideBlob(float x, float z)
        {
            if (!blobReady) return true;
            float nx = (x - blobCX) / blobRX, nz = (z - blobCZ) / blobRZ;
            float ang = MathF.Atan2(nz, nx);
            float r = MathF.Sqrt(nx * nx + nz * nz);
            float boundary = 0.86f + ha1 * MathF.Cos(hb1 * ang + hp1) + ha2 * MathF.Cos(hb2 * ang + hp2);
            return r <= boundary;
        }

        // --- content tracking: streets end at the town's houses, never at the bounding square.
        var streetZ = Enumerable.Range(0, size.Columns + 1).Select(_ => (z0: float.MaxValue, z1: float.MinValue)).ToArray();
        var streetX = Enumerable.Range(0, size.EWStreets).Select(_ => (x0: float.MaxValue, x1: float.MinValue)).ToArray();
        var rowFilled = new bool[blockRows];
        var colFilled = new bool[blockRows, size.Columns];
        void RecordContent(int districtRow, int col, float x0, float x1, float z0, float z1)
        {
            for (int s = col; s <= col + 1 && s <= size.Columns; s++)
            {
                streetZ[s].z0 = MathF.Min(streetZ[s].z0, z0);
                streetZ[s].z1 = MathF.Max(streetZ[s].z1, z1);
            }
            if (districtRow >= 0)
            {
                for (int s = districtRow; s <= districtRow + 1 && s < size.EWStreets; s++)
                {
                    streetX[s].x0 = MathF.Min(streetX[s].x0, x0);
                    streetX[s].x1 = MathF.Max(streetX[s].x1, x1);
                }
                if (districtRow < blockRows) rowFilled[districtRow] = true;
                if (col < size.Columns && districtRow < blockRows) colFilled[districtRow, col] = true;
            }
            else
            {
                // commercial band contributes to the first E–W street only
                streetX[0].x0 = MathF.Min(streetX[0].x0, x0);
                streetX[0].x1 = MathF.Max(streetX[0].x1, x1);
            }
        }

        // --- occupancy registry so no two buildings land on each other.
        var occupied = new List<(float x0, float z0, float x1, float z1)>();
        static (float x0, float z0, float x1, float z1) FootprintBounds(HousePlan h)
        {
            float x0 = h.Footprint.Select(p => p.X).Min(), x1 = h.Footprint.Select(p => p.X).Max();
            float z0 = h.Footprint.Select(p => p.Z).Min(), z1 = h.Footprint.Select(p => p.Z).Max();
            const float pad = 3f;
            return (x0 - pad, z0 - pad, x1 + pad, z1 + pad);
        }
        static (float x0, float z0, float x1, float z1) OccupiedBounds(Outbuilding o)
        {
            float hw = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet / 2f : o.WidthFeet / 2f;
            float hd = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet / 2f : o.DepthFeet / 2f;
            const float pad = 2f;
            return (o.Center.X - hw - pad, o.Center.Z - hd - pad, o.Center.X + hw + pad, o.Center.Z + hd + pad);
        }
        bool OccupiedCollides((float x0, float z0, float x1, float z1) r)
            => occupied.Any(o => r.x0 < o.x1 && r.x1 > o.x0 && r.z0 < o.z1 && r.z1 > o.z0);
        void RegisterOccupied((float x0, float z0, float x1, float z1) r) => occupied.Add(r);

        float BlobNormR(float x, float z)
        {
            float nx = (x - blobCX) / blobRX, nz = (z - blobCZ) / blobRZ;
            return MathF.Sqrt(nx * nx + nz * nz);
        }
        // Out-of-town-edge lots go vacant more often than interior ones.
        bool IsVacantLot(float cx, float cz)
        {
            float r = BlobNormR(cx, cz);
            float p = r > 0.66f ? 0.30f : r > 0.48f ? 0.12f : 0.05f;
            return rng.Chance(p);
        }

        var map = new MapPlan
        {
            Title = $"SMALL TOWN — {townName} — SEED {seed} ({size.Label})",
            Seed = seed,
            WidthFeet = totalWidth,
            DepthFeet = totalDepth,
            RoadSurface = "asphalt",
            QuietAnnotations = !detail,
        };

        // --- roads: highway (dashed), E–W grid streets, N–S streets.
        map.Roads.Add(new MapRect(0, 0, totalWidth, HighwayWidth));
        map.RoadDashed.Add(true);
        map.Labels.Add(new MapLabel { Position = new Vec2(totalWidth / 2f, HighwayWidth / 2f), Text = "HIGHWAY 18", Large = true });
        foreach (var z in ewZ)
        {
            map.Roads.Add(new MapRect(0, z, totalWidth, StreetRow));
            map.RoadDashed.Add(false);
        }
        for (int s = 0; s <= size.Columns; s++)
        {
            // N–S streets run from the highway's north edge through the last E–W street.
            map.Roads.Add(new MapRect(nsX[s], zBand, StreetRow, lastStreetEnd - zBand));
            map.RoadDashed.Add(false);
        }

        // --- commercial band along the highway. Main street extends south to it.
        // Elevator first, so pads can be kept clear of it.
        float mainX = nsX[mainStreetIndex];
        var bandOcc = new List<(float x0, float x1)>();
        {
            float evW = 48f, evD = 88f;
            float evX0 = mainX + StreetRow + 55f;
            map.Outbuildings.Add(new Outbuilding
            {
                Kind = OutbuildingKind.GrainElevator,
                Center = new Vec2(evX0 + evW / 2f, zBand + BandDepth / 2f),
                WidthFeet = evW,
                DepthFeet = evD,
                Label = "ELEVATOR",
            });
            if (evX0 + evW + 8f < totalWidth - 8f) bandOcc.Add((evX0 - 14f, evX0 + evW + 80f));
            int bins = rng.RangeInt(4, 7);
            float binD = 22f, spacing = binD + 8f;
            float binRowW = bins * spacing - 8f;
            float binX = evX0 + (evW - binRowW) / 2f;
            if (binX + binRowW > totalWidth - 8f) binX = totalWidth - 8f - binRowW;
            for (int i = 0; i < bins; i++)
                map.Outbuildings.Add(new Outbuilding
                {
                    Kind = OutbuildingKind.GrainBin,
                    Center = new Vec2(binX + i * spacing + binD / 2f, zBand + BandDepth / 2f + evD / 2f + 10f + binD / 2f),
                    DiameterFeet = binD,
                    Label = "",
                });
            bandOcc.Add((evX0 - 14f, totalWidth + 1f));   // keep the pad row clear of elevator + bins
        }
        // 1–2 industrial flat boxes on the highway edge, away from the main junction.
        string[] industrialNames = { "MERCO MFG", "CO-OP PLANT", "SIOUX STEEL", "MERIDIAN MFG" };
        for (int i = 0, placedInd = 0; i < 2 && placedInd < (rng.Chance(0.55f) ? 2 : 1); i++)
        {
            float iw = rng.Range(85f, 115f), id = rng.Range(55f, 70f);
            float ix = i == 0 ? rng.Range(50f, totalWidth * 0.30f) : rng.Range(totalWidth * 0.62f, totalWidth - iw - 50f);
            if (ix < mainX + StreetRow + 20f && ix + iw > mainX - 20f) continue;
            map.Outbuildings.Add(new Outbuilding
            {
                Kind = OutbuildingKind.Storefront,
                Center = new Vec2(ix + iw / 2f, zBand + id / 2f + 8f),
                WidthFeet = iw,
                DepthFeet = id,
                Label = industrialNames[(i + (int)(seed % 4)) % industrialNames.Length],
            });
            bandOcc.Add((ix - 10f, ix + iw + 10f));
            placedInd++;
        }
        var padLabels = new List<string> { "CASEY'S", "FEED & SEED", "CO-OP", "HARDWARE", "BAR & GRILL", "AUTO ELECTRIC" };
        {
            float x = 55f;
            int placed = 0, guard = 0;
            while (placed < size.CommercialPads && guard++ < 60)
            {
                float w = rng.Range(75f, 115f);
                float gap = rng.Range(28f, 52f);
                bool inMainGap = x + w > mainX - 12f && x < mainX + StreetRow + 12f;
                bool inBandOcc = bandOcc.Any(b => x < b.x1 && x + w > b.x0);
                if (x + w <= totalWidth - 40f && !inMainGap && !inBandOcc && InsideBlob(x + w / 2f, zBand + 50f))
                {
                    RecordContent(-1, 0, x, x + w, zBand, zBand + 80f);
                    float d = rng.Range(52f, 72f);
                    map.Outbuildings.Add(new Outbuilding
                    {
                        Kind = OutbuildingKind.Storefront,
                        Center = new Vec2(x + w / 2f, zBand + d / 2f + 8f),
                        WidthFeet = w,
                        DepthFeet = d,
                        Label = padLabels[placed % padLabels.Count],
                    });
                    bandOcc.Add((x - 6f, x + w + 6f));
                    placed++;
                }
                x += w + gap;
            }
        }
        uint houseSeed = seed * 2654435761u + 97u;

        // --- SOUTH side of the highway: sparse fringe — industrial/store(s) + a few houses
        // facing the highway. Most towns live on both sides of the main road (George, Boyden, Hull).
        float southBand = rng.Range(150f, 210f);
        float southEdge = -southBand;
        {
            float x = 45f;
            int houseCount = rng.RangeInt(1, 4);
            for (int i = 0; i < houseCount && x < totalWidth - 260f; i++)
            {
                float hW = 150f + rng.Range(0f, 70f);
                uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u;
                string arch = Weighted(rng);
                var house = HouseGenerator.Generate(arch, hSeed, i % 3 == 0 ? "detached" : "integral");
                house = house.FlipZ();
                float hx = x + MathF.Max(4f, (hW - house.WidthFeet) / 2f);
                float setback = rng.Range(10f, 22f);
                float offsetZ = -setback;   // front (flipped) faces north toward the highway
                house = house.OffsetCopy(hx, offsetZ);
                map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = true });
                var fd = FindFrontDoor(house);
                map.Walks.Add(new Driveway { Surface = "concrete", WidthFeet = 4f, Points = { new Vec2(fd.X, -2.25f), fd } });
                var (garageDoor, _, frontEntry) = FindGarageDoor(house);
                if (!float.IsNaN(garageDoor.X))
                {
                    var d = DrivewayTo(garageDoor, frontEntry, true, -34f, "concrete", 16f);
                    map.Driveways.Add(d);
                }
                x += hW + rng.Range(24f, 60f);
            }
            // one industrial or storefront at the remaining side
            if (rng.Chance(0.6f))
            {
                float iw = rng.Range(70f, 100f), id = rng.Range(46f, 60f);
                float ix = totalWidth - iw - 45f;
                string[] southLabels = { "MERIDIAN MFG", "CO-OP PLANT", "SOAP WORKS", "FEED CO-OP" };
                if (ix > x + 20f)
                {
                    map.Outbuildings.Add(new Outbuilding
                    {
                        Kind = OutbuildingKind.Storefront,
                        Center = new Vec2(ix + iw / 2f, southEdge + id / 2f + 10f),
                        WidthFeet = iw,
                        DepthFeet = id,
                        Label = southLabels[(int)(seed % 4)],
                    });
                }
            }
        }
        map.DepthFeet += southBand;

        // --- civic reserve faces: face (s, side, col) — s = street index, side 0 = north
        // row (faces south), side 1 = south row (faces north, s ≥ 1), col = column index.
        var faceIds = new List<(int s, int side, int col)>();
        for (int s = 0; s < size.EWStreets; s++)
            for (int col = 0; col < size.Columns; col++) faceIds.Add((s, 0, col));
        for (int s = 1; s < size.EWStreets; s++)
            for (int col = 0; col < size.Columns; col++) faceIds.Add((s, 1, col));
        var civic = new Dictionary<(int s, int side, int col), OutbuildingKind>();
        OutbuildingKind[] civicKinds = { OutbuildingKind.CityPark, OutbuildingKind.School, OutbuildingKind.Church, OutbuildingKind.Church, OutbuildingKind.CityPark };
        for (int i = 0; i < civicKinds.Length && faceIds.Count > 0; i++)
        {
            int idx = rng.RangeInt(0, faceIds.Count);
            civic[faceIds[idx]] = civicKinds[i];
            faceIds.RemoveAt(idx);
        }

        void FillTownLot(int streetIdx, int col, int lotIdx, bool facesSouth, int districtRow, float lotX, float frontage, OutbuildingKind? anchor, bool alley)
        {
            float streetZ = ewZ[streetIdx];
            float rowZ0 = facesSouth ? streetZ + StreetRow : streetZ - lotDepth;
            float cx = lotX + frontage / 2f, cz = rowZ0 + lotDepth / 2f;
            if (!InsideBlob(cx, cz)) return; // ragged town edge
            RecordContent(districtRow, col, lotX, lotX + frontage, rowZ0, rowZ0 + lotDepth);
            // Seeded vacancy, weighted toward the ragged outskirts.
            if (!anchor.HasValue && IsVacantLot(cx, cz))
            {
                map.LotLines.Add(new MapRect(lotX, rowZ0, frontage, lotDepth));
                ScatterTrees(map, rng, rng.RangeInt(1, 4), lotX + 6f, lotX + frontage - 6f, rowZ0 + 6f, rowZ0 + lotDepth - 6f, null, 6f);
                return;
            }
            map.LotLines.Add(new MapRect(lotX, rowZ0, frontage, lotDepth));

            float setback = rng.Range(8f, 17f);
            float frontLineZ = facesSouth ? streetZ + StreetRow : streetZ;
            int gRoll = rng.RangeInt(0, 100);
            // Most houses: integral garage. A real fraction get a detached pole garage instead.
            string garageMode = gRoll < 45 ? "detached" : gRoll < 92 ? "integral" : "none";
            uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u;

            if (anchor.HasValue)
            {
                var a = PlaceAnchor(anchor.Value, lotX, rowZ0, frontage, lotDepth);
                map.Outbuildings.Add(a);
                RegisterOccupied(OccupiedBounds(a));
                map.Walks.Add(new Driveway
                {
                    Surface = "concrete",
                    WidthFeet = 5f,
                    Points = { new Vec2(a.Center.X, frontLineZ), new Vec2(a.Center.X, a.Center.Z) },
                });
                ScatterTrees(map, rng, rng.RangeInt(3, 6), lotX + 5f, lotX + frontage - 5f, rowZ0 + 5f, rowZ0 + lotDepth - 5f, null, 6f);
                return;
            }

            // Narrow half-lots can only host the narrow archetype.
            string arch = frontage < 46f ? "farmhouse" : Weighted(rng);
            var house = HouseGenerator.Generate(arch, hSeed, garageMode);
            if (house.WidthFeet > frontage - 3f && arch != "farmhouse")
                house = HouseGenerator.Generate("farmhouse", hSeed ^ 0x5f3759dfu);

            float hx = lotX + MathF.Max(2f, (frontage - house.WidthFeet) / 2f + rng.Range(-2f, 2f));
            bool mirrored = !facesSouth;
            if (mirrored) house = house.FlipZ();
            // House must fit between the street edge and the lot's back line (clear of the alley).
            float hzSpan = house.Footprint.Select(p => p.Z).Max() - house.Footprint.Select(p => p.Z).Min();
            if (hzSpan > lotDepth - 8f) { RecordContent(districtRow, col, lotX, lotX + frontage, rowZ0, rowZ0 + lotDepth); map.LotLines.Add(new MapRect(lotX, rowZ0, frontage, lotDepth)); return; }
            setback = MathF.Min(setback, lotDepth - 4f - hzSpan);
            if (setback < 4f) { map.LotLines.Add(new MapRect(lotX, rowZ0, frontage, lotDepth)); return; }
            float offsetZ = facesSouth ? frontLineZ + setback : frontLineZ - setback;
            house = house.OffsetCopy(hx, offsetZ);
            map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = mirrored });
            RegisterOccupied(FootprintBounds(house));

            var fd = FindFrontDoor(house);
            map.Walks.Add(new Driveway
            {
                Surface = "concrete",
                WidthFeet = 4f,
                Points = { new Vec2(fd.X, facesSouth ? frontLineZ + 4.5f : frontLineZ - 4.5f), fd },
            });

            // ~12% of houses get a small pole shed at the back of the lot, away from the garage side.
            if (rng.Chance(0.12f))
            {
                float shedW = rng.Range(8f, 11f), shedD = rng.Range(10f, 13f);
                bool garageRight = house.Walls.Any(w => w.Tag == EdgeTag.GarageSide) &&
                    (house.Walls.First(w => w.Tag == EdgeTag.GarageSide).Start.X + house.Walls.First(w => w.Tag == EdgeTag.GarageSide).End.X) / 2f >
                    (house.Footprint.Select(p => p.X).Min() + house.Footprint.Select(p => p.X).Max()) / 2f;
                float shedX = garageRight ? lotX + 4f : lotX + frontage - shedW - 4f;
                float shedZ = rowZ0 + lotDepth - shedD - 5f;
                var shed = new Outbuilding { Kind = OutbuildingKind.PoleBarn, Center = new Vec2(shedX + shedW / 2f, shedZ + shedD / 2f), WidthFeet = shedW, DepthFeet = shedD, Label = "SHED" };
                if (!OccupiedCollides(OccupiedBounds(shed)))
                {
                    map.Outbuildings.Add(shed);
                    RegisterOccupied(OccupiedBounds(shed));
                }
            }

            bool garageWanted = garageMode != "none";
            var (garageDoor, garageWall, frontEntry) = FindGarageDoor(house);
            bool garageViaAlley = garageWanted && alley && rng.Chance(0.6f);
            if (garageViaAlley)
            {
                float dgW = 22f, dgD = 26f;
                float dgCX = lotX + frontage / 2f + rng.Range(-6f, 6f);
                float dgZ = facesSouth ? rowZ0 + lotDepth - 9f - dgD / 2f : rowZ0 + 9f + dgD / 2f;
                var dg = new Outbuilding
                {
                    Kind = OutbuildingKind.DetachedGarage,
                    Center = new Vec2(dgCX, dgZ),
                    WidthFeet = dgW,
                    DepthFeet = dgD,
                    Label = "DET. GARAGE",
                };
                if (!OccupiedCollides(OccupiedBounds(dg)))
                {
                    map.Outbuildings.Add(dg);
                    RegisterOccupied(OccupiedBounds(dg));
                }
                float alleyZ = facesSouth ? rowZ0 + lotDepth - 8f : rowZ0 + 8f;
                map.Driveways.Add(new Driveway { Surface = "gravel", WidthFeet = 10f, Points = { new Vec2(dgCX, alleyZ), new Vec2(dgCX, dgZ) } });
            }
            else if (garageWanted)
            {
                if (float.IsNaN(garageDoor.X))
                {
                    bool right = rng.Chance(0.5f);
                    float dgW = 20f, dgD = 22f;
                    float dgX = right ? lotX + frontage - dgW - 3f : lotX + 3f;
                    float dgZ = facesSouth
                        ? Math.Clamp(house.Footprint.Select(p => p.Z).Max() + 12f, frontLineZ + 26f, rowZ0 + lotDepth - dgD - 4f)
                        : Math.Clamp(house.Footprint.Select(p => p.Z).Min() - 12f - dgD, rowZ0 + 22f, rowZ0 + lotDepth - dgD - 4f);
                    var dg = new Outbuilding
                    {
                        Kind = OutbuildingKind.DetachedGarage,
                        Center = new Vec2(dgX + dgW / 2f, dgZ + dgD / 2f),
                        WidthFeet = dgW,
                        DepthFeet = dgD,
                        Label = "DET. GARAGE",
                    };
                    if (!OccupiedCollides(OccupiedBounds(dg)))
                    {
                        map.Outbuildings.Add(dg);
                        RegisterOccupied(OccupiedBounds(dg));
                        garageDoor = new Vec2(dgX + dgW / 2f, facesSouth ? dgZ : dgZ + dgD);
                        frontEntry = true;
                    }
                }
                if (!float.IsNaN(garageDoor.X))
                {
                    bool garageRight = !frontEntry && GarageSideIsRight(house, garageWall!);
                    float zStart = facesSouth ? frontLineZ + 2f : frontLineZ - 2f;
                    map.Driveways.Add(DrivewayTo(garageDoor, frontEntry, garageRight, zStart, "concrete", 17f));
                }
            }

            ScatterTrees(map, rng, rng.RangeInt(2, 5), lotX + 4f, lotX + frontage - 4f, rowZ0 + 5f, rowZ0 + lotDepth - 5f, new[] { house }, 5f);
        }

        // Fill one E/W-facing lot on the side of a 4-sided block (x-axis frontage/facing).
        void FillSideFace(int blockRow, int col, int side, float blockX0, float blockX1, float bz0, float bz1)
        {
            bool west = side == 0;
            float lotX = west ? blockX0 : blockX1 - SideLotDepth;
            float z = bz0 + 3f;
            while (true)
            {
                float f = rng.Range(58f, 76f);
                if (z + f > bz1 - 3f) break;
                float cx = lotX + SideLotDepth / 2f, cz = z + f / 2f;
                if (!InsideBlob(cx, cz)) { z += f; continue; } // ragged town edge
                if (IsVacantLot(cx, cz))
                {
                    RecordContent(blockRow, col, lotX, lotX + SideLotDepth, z, z + f);
                    map.LotLines.Add(new MapRect(lotX, z, SideLotDepth, f));
                    ScatterTrees(map, rng, rng.RangeInt(0, 3), lotX + 4f, lotX + SideLotDepth - 4f, z + 4f, z + f - 4f, null, 6f);
                    z += f;
                    continue;
                }
                PlaceSideLot(blockRow, col, lotX, z, f, west);
                z += f;
            }
        }

        void PlaceSideLot(int districtRow, int col, float lotX, float z0, float f, bool west)
        {
            RecordContent(districtRow, col, lotX, lotX + SideLotDepth, z0, z0 + f);
            map.LotLines.Add(new MapRect(lotX, z0, SideLotDepth, f));

            float setback = rng.Range(9f, 17f);
            uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u;
            string arch = Weighted(rng);
            var house = HouseGenerator.Generate(arch, hSeed);
            house = west ? house.Rotate90CW() : house.Rotate90CCW();
            float dx = west ? lotX + setback : lotX + SideLotDepth - setback;
            float hzMin = house.Footprint.Select(p => p.Z).Min();
            float hzSpan = house.Footprint.Select(p => p.Z).Max() - hzMin;
            float dz = z0 + f / 2f - (hzMin + hzSpan / 2f);
            house = house.OffsetCopy(dx, dz);
            map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = false });

            var fd = FindFrontDoor(house);
            float walkStartX = west ? lotX - 2.25f : lotX + SideLotDepth + 2.25f;
            map.Walks.Add(new Driveway
            {
                Surface = "concrete",
                WidthFeet = 4f,
                Points = { new Vec2(walkStartX, fd.Z), fd },
            });

            var (garageDoor, garageWall, runsE) = FindGarageDoor(house);
            bool frontEntry = !runsE; // garage door on the front wall now faces ±x
            float xStart = west ? lotX - 1.5f : lotX + SideLotDepth + 1.5f;
            if (!float.IsNaN(garageDoor.X))
            {
                float fc = (house.Footprint.Select(p => p.Z).Min() + house.Footprint.Select(p => p.Z).Max()) / 2f;
                bool garagePositiveZ = garageDoor.Z >= fc;
                map.Driveways.Add(DrivewayToX(garageDoor, frontEntry, garagePositiveZ, xStart, "concrete", 17f));
            }

            ScatterTrees(map, rng, rng.RangeInt(0, 3), lotX + 4f, lotX + SideLotDepth - 4f, z0 + 4f, z0 + f - 4f, new[] { house }, 4f);
        }

        // North row of EVERY street (faces south); south row of every street except the first.
        for (int s = 0; s < size.EWStreets; s++)
        {
            bool alley = s < blockRows && rowAlley[s];
            for (int col = 0; col < size.Columns; col++)
            {
                float x = nsX[col] + StreetRow + SideLotDepth;
                for (int i = 0; i < colFrontages[col].Count; i++)
                {
                    float f = colFrontages[col][i];
                    OutbuildingKind? anchor = i == 0 && civic.TryGetValue((s, 0, col), out var k0) ? k0 : null;
                    FillTownLot(s, col, i, facesSouth: true, districtRow: s, x, f, anchor, alley);
                    x += f;
                }
            }
        }
        for (int s = 1; s < size.EWStreets; s++)
        {
            bool alley = rowAlley[s - 1];
            for (int col = 0; col < size.Columns; col++)
            {
                float x = nsX[col] + StreetRow + SideLotDepth;
                for (int i = 0; i < colFrontages[col].Count; i++)
                {
                    float f = colFrontages[col][i];
                    OutbuildingKind? anchor = i == 0 && civic.TryGetValue((s, 1, col), out var k1) ? k1 : null;
                    FillTownLot(s, col, i, facesSouth: false, districtRow: s - 1, x, f, anchor, alley);
                    x += f;
                }
            }
        }

        // Side lots facing the N–S streets (4-sided blocks only); otherwise corner-yard trees.
        for (int blockRow = 0; blockRow < blockRows; blockRow++)
        {
            float bz0 = ewZ[blockRow] + StreetRow;
            float bz1 = ewZ[blockRow + 1];
            for (int col = 0; col < size.Columns; col++)
            {
                float bx0 = nsX[col] + StreetRow;
                float bx1 = bx0 + BlockWidth(col);
                if (IsFourSide(blockRow, col))
                {
                    FillSideFace(blockRow, col, 0, bx0, bx1, bz0, bz1);
                    FillSideFace(blockRow, col, 1, bx0, bx1, bz0, bz1);
                }
                else
                {
                    ScatterTrees(map, rng, rng.RangeInt(1, 4), bx0 + 4f, bx0 + SideLotDepth - 4f, bz0 + 6f, bz1 - 6f, null, 6f);
                    ScatterTrees(map, rng, rng.RangeInt(1, 4), bx1 - SideLotDepth + 4f, bx1 - 4f, bz0 + 6f, bz1 - 6f, null, 6f);
                }
            }
        }

        // --- alleys, only through blocks that actually ended up with content.
        for (int row = 0; row < blockRows; row++)
        {
            if (!rowAlley[row]) continue;
            float aZ = ewZ[row] + StreetRow + lotDepth;
            for (int col = 0; col < size.Columns; col++)
            {
                if (!colFilled[row, col]) continue;
                float bX = nsX[col] + StreetRow;
                map.Alleys.Add(new MapRect(bX, aZ, BlockWidth(col), AlleyRow));
            }
        }

        // --- clip streets to the town's content: no street runs past the last house into a square fringe.
        (float x0, float x1)?[] ewClip = new (float x0, float x1)?[size.EWStreets];
        bool[] ewKeep = new bool[size.EWStreets];
        for (int s = 0; s < size.EWStreets; s++)
        {
            ewKeep[s] = streetX[s].x1 >= streetX[s].x0;
            if (ewKeep[s]) ewClip[s] = (streetX[s].x0, streetX[s].x1);
        }
        float[] nsZ1 = new float[size.Columns + 1];
        bool[] nsKeep = new bool[size.Columns + 1];
        for (int s = 0; s <= size.Columns; s++)
        {
            nsZ1[s] = MathF.Min(MathF.Max(streetZ[s].z1, ewZ[0] + StreetRow + lotDepth * 0.5f), lastStreetEnd);
            nsKeep[s] = streetZ[s].z1 > zBand + 40f;
        }
        {
            var newRoads = new List<MapRect> { map.Roads[0] };
            var newDashed = new List<bool> { map.RoadDashed[0] };
            for (int s = 0; s < size.EWStreets; s++)
            {
                if (!ewKeep[s]) continue;
                var c = ewClip[s]!.Value;
                newRoads.Add(new MapRect(c.x0, ewZ[s], c.x1 - c.x0, StreetRow));
                newDashed.Add(false);
            }
            for (int s = 0; s <= size.Columns; s++)
            {
                if (!nsKeep[s]) continue;
                newRoads.Add(new MapRect(nsX[s], zBand, StreetRow, nsZ1[s] - zBand));
                newDashed.Add(false);
            }
            map.Roads.Clear();
            map.Roads.AddRange(newRoads);
            map.RoadDashed.Clear();
            map.RoadDashed.AddRange(newDashed);
        }

        // --- sidewalks, clipped to each street's surviving span; street trees in the same span.
        for (int i = 0; i < size.EWStreets; i++)
        {
            if (!ewKeep[i]) continue;
            var c = ewClip[i]!.Value;
            float z = ewZ[i];
            foreach (var wz in new[] { z + 2.25f, z + StreetRow - 2.25f })
            {
                map.Walks.Add(new Driveway { Surface = "concrete", WidthFeet = 4.5f, Points = { new Vec2(c.x0, wz), new Vec2(c.x1, wz) } });
            }
            for (float x = c.x0 + 16f; x < c.x1 - 16f; x += rng.Range(30f, 46f))
            {
                if (nsX.Any(sx => MathF.Abs(x - (sx + StreetRow / 2f)) < 26f)) continue;
                if (!InsideBlob(x, z + StreetRow / 2f)) continue;
                if (rng.Chance(0.72f)) map.Trees.Add(new Vec2(x, z + 8f));
                if (rng.Chance(0.72f)) map.Trees.Add(new Vec2(x + 13f, z + StreetRow - 8f));
            }
        }
        for (int s = 0; s <= size.Columns; s++)
        {
            if (!nsKeep[s]) continue;
            foreach (var wx in new[] { nsX[s] + 2.25f, nsX[s] + StreetRow - 2.25f })
            {
                map.Walks.Add(new Driveway
                {
                    Surface = "concrete",
                    WidthFeet = 4.5f,
                    Points = { new Vec2(wx, zBand), new Vec2(wx, nsZ1[s]) },
                });
            }
        }

        // Street names, from surviving spans.
        for (int i = 0; i < size.EWStreets; i++)
        {
            if (!ewKeep[i]) continue;
            var c = ewClip[i]!.Value;
            map.Labels.Add(new MapLabel { Position = new Vec2((c.x0 + c.x1) / 2f, ewZ[i] + StreetRow / 2f), Text = ewNames[i % ewNames.Length] + " AVENUE" });
        }
        for (int s = 0; s <= size.Columns; s++)
        {
            if (!nsKeep[s]) continue;
            int ni = s < mainStreetIndex ? s : s - 1;
            string nm = s == mainStreetIndex ? "MAIN" : nsNames[ni % nsNames.Length];
            map.Labels.Add(new MapLabel
            {
                Position = new Vec2(nsX[s] + StreetRow / 2f, zBand + (nsZ1[s] - zBand) * 0.62f),
                Text = nm + " STREET",
                RotateDeg = -90f,
            });
        }

        // --- roads never stop at a flat edge: EVERY surviving street terminus is a turnaround.
        // Two seeded stubs are full asphalt cul-de-sacs (with flanking lots); the rest are
        // subtle gravel turnarounds (rural dead-end aprons, not subdivisions).
        var termini = new List<(Vec2 pos, bool nsStub, int idx)>();
        for (int s = 0; s < size.EWStreets; s++)
        {
            if (!ewKeep[s]) continue;
            var c = ewClip[s]!.Value;
            termini.Add((new Vec2(c.x0, ewZ[s] + StreetRow / 2f), false, s));
            termini.Add((new Vec2(c.x1, ewZ[s] + StreetRow / 2f), false, s));
        }
        for (int s = 0; s <= size.Columns; s++)
        {
            if (!nsKeep[s]) continue;
            termini.Add((new Vec2(nsX[s] + StreetRow / 2f, nsZ1[s]), true, s));
        }
        var bigCandidates = termini.Where(t => t.nsStub).OrderBy(_ => rng.NextUInt()).Take(2).ToList();
        foreach (var t in termini)
        {
            bool big = bigCandidates.Contains(t);
            map.Bulbs.Add(new RoadBulb { Center = t.pos, RadiusFeet = big ? 46f : 26f, Fill = big ? null : "#c8c3b5" });
        }
        foreach (var t in bigCandidates)
        {
            float stubX = t.pos.X - StreetRow / 2f;
            float bulbZ = t.pos.Z - 6f;
            float f = 62f, zl = bulbZ - 16f - f;
            PlaceSideLot(blockRows, 0, stubX - SideLotDepth, zl, f, west: false); // west flank faces EAST onto the stub
            PlaceSideLot(blockRows, 1, stubX + StreetRow, zl, f, west: true);     // east flank faces WEST onto the stub
        }

        map.Labels.Add(new MapLabel
        {
            Position = new Vec2(totalWidth - 8f, 14f),
            Text = $"{totalWidth:0}′ × {totalDepth:0}′",
            Anchor = "end",
        });
        return map;
    }

    static string Weighted(Rng rng)
    {
        float r = rng.NextFloat();
        return r < 0.50f ? "ranch" : r < 0.80f ? "split-level" : "farmhouse";
    }

    static Outbuilding PlaceAnchor(OutbuildingKind kind, float lotX, float rowZ0, float frontage, float lotDepth)
    {
        float cx = lotX + frontage / 2f;
        float midZ = rowZ0 + lotDepth / 2f;
        return kind switch
        {
            OutbuildingKind.CityPark => new Outbuilding { Kind = kind, Center = new Vec2(cx, midZ), WidthFeet = frontage - 10f, DepthFeet = lotDepth - 14f, Label = "CITY PARK" },
            OutbuildingKind.School => new Outbuilding { Kind = kind, Center = new Vec2(cx, midZ), WidthFeet = MathF.Min(frontage - 8f, 84f), DepthFeet = 64f, Label = "SCHOOL" },
            OutbuildingKind.Church => new Outbuilding { Kind = kind, Center = new Vec2(cx, midZ), WidthFeet = 32f, DepthFeet = 54f, Label = "CHURCH" },
            _ => new Outbuilding { Kind = kind, Center = new Vec2(cx, midZ), WidthFeet = frontage - 12f, DepthFeet = lotDepth / 2f, Label = kind.ToString().ToUpperInvariant() },
        };
    }

    static (Vec2 Pos, WallSegment Wall, bool FrontEntry) FindGarageDoor(HousePlan plan)
    {
        foreach (var wall in plan.Walls)
        {
            var dir = (wall.End - wall.Start).Normalized();
            foreach (var op in wall.Openings)
            {
                if (op.Kind != OpeningKind.GarageDoor) continue;
                var pos = wall.Start + dir * op.OffsetFeet;
                bool horizontal = MathF.Abs(dir.X) > MathF.Abs(dir.Z);
                return (pos, wall, horizontal);
            }
        }
        return (new Vec2(float.NaN, float.NaN), null!, false);
    }

    static Vec2 FindFrontDoor(HousePlan plan)
    {
        foreach (var wall in plan.Walls.Where(w => w.Tag == EdgeTag.MainFront))
        {
            var dir = (wall.End - wall.Start).Normalized();
            foreach (var op in wall.Openings)
                if (op.Kind == OpeningKind.Door)
                    return wall.Start + dir * op.OffsetFeet;
        }
        return new Vec2(plan.MinX + plan.WidthFeet / 2f, plan.MinZ);
    }

    static bool GarageSideIsRight(HousePlan plan, WallSegment wall)
    {
        float cx = (plan.Footprint.Select(p => p.X).Min() + plan.Footprint.Select(p => p.X).Max()) / 2f;
        return (wall.Start.X + wall.End.X) / 2f > cx;
    }

    // X-axis variant: the street is to the side (N–S facing lots).
    static Driveway DrivewayToX(Vec2 garageDoor, bool frontEntry, bool garagePositiveZ, float xStart, string surface, float width)
    {
        var d = new Driveway { WidthFeet = width, Surface = surface };
        if (frontEntry)
        {
            d.Points.Add(new Vec2(xStart, garageDoor.Z));
            d.Points.Add(new Vec2(garageDoor.X, garageDoor.Z));
        }
        else
        {
            float laneZ = garageDoor.Z + (garagePositiveZ ? 9f : -9f);
            d.Points.Add(new Vec2(xStart, laneZ));
            d.Points.Add(new Vec2(garageDoor.X, laneZ));
            d.Points.Add(new Vec2(garageDoor.X, garageDoor.Z));
        }
        return d;
    }

    static Driveway DrivewayTo(Vec2 garageDoor, bool frontEntry, bool garageRight, float zStart, string surface, float width)
    {
        var d = new Driveway { WidthFeet = width, Surface = surface };
        if (frontEntry)
        {
            d.Points.Add(new Vec2(garageDoor.X, zStart));
            d.Points.Add(new Vec2(garageDoor.X, garageDoor.Z));
        }
        else
        {
            float laneX = garageDoor.X + (garageRight ? 9f : -9f);
            d.Points.Add(new Vec2(laneX, zStart));
            d.Points.Add(new Vec2(laneX, garageDoor.Z));
            d.Points.Add(new Vec2(garageDoor.X, garageDoor.Z));
        }
        return d;
    }

    static bool PointInHouse(Vec2 p, HousePlan h, float pad)
    {
        float minX = h.Footprint.Select(f => f.X).Min() - pad;
        float maxX = h.Footprint.Select(f => f.X).Max() + pad;
        float minZ = h.Footprint.Select(f => f.Z).Min() - pad;
        float maxZ = h.Footprint.Select(f => f.Z).Max() + pad;
        return p.X >= minX && p.X <= maxX && p.Z >= minZ && p.Z <= maxZ;
    }

    static void ScatterTrees(MapPlan map, Rng rng, int count, float minX, float maxX, float minZ, float maxZ,
        IReadOnlyList<HousePlan>? avoid, float pad)
    {
        int placed = 0, attempts = 0;
        while (placed < count && attempts < 40)
        {
            attempts++;
            var p = new Vec2(rng.Range(minX, maxX), rng.Range(minZ, maxZ));
            if (avoid != null && avoid.Any(h => PointInHouse(p, h, pad))) continue;
            map.Trees.Add(p);
            placed++;
        }
    }
}
}
