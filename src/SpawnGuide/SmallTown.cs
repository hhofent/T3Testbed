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
///   - civic anchors on the grid: school, church(es), park, grain elevator
///   - heavy tree canopy; abrupt end into fields on all sides
/// Hamlet preset is implemented; Boyden/Hull presets are parameter bumps to come.
/// Size is deliberately decoupled from spawn FREQUENCY — the town field (how often
/// hamlets/towns appear per 30/100 miles) is NOT implemented here yet.
/// Lots face the E–W streets only in this version (N–S street faces get no lots yet).
/// </summary>
public static class SmallTownGenerator
{
    const float HighwayWidth = 66f;   // section-line highway ROW (HWY 18 pattern)
    const float StreetRow = 36f;      // residential grid street ROW
    const float AlleyRow = 16f;
    const float BlockLotDepth = 122f; // back-to-back lot rows inside a block
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
        // Hamlet: George/Craig/Struble class. ~0.25 mi wide. Boyden and Hull presets
        // (larger grids, school+park+more commercial) come after this tests clean.
        ["hamlet"] = new TownSize { Columns = 5, EWStreets = 4, LotsMin = 3, LotsMax = 4, CommercialPads = 3, Label = "HAMLET" },
    };

    public static MapPlan Generate(uint seed, string sizeName)
    {
        if (!Sizes.TryGetValue(sizeName, out var size))
            throw new ArgumentException($"unknown town size '{sizeName}' (have: {string.Join(", ", Sizes.Keys)})");

        var rng = new Rng(seed);
        float lotDepth = BlockLotDepth + rng.Range(-6f, 6f);
        float blockDepth = 2 * lotDepth + AlleyRow;   // one alley per block, shared
        float streetSpacing = StreetRow + blockDepth;

        string townName = rng.Pick(new[] { "CEDAR BLUFF", "TWIN GROVE", "MARBLE JUNCTION", "PRAIRIE CENTER", "HALSTEAD", "NEW HOPE" });
        string[] ewNames = { "IOWA", "MICHIGAN", "MINNESOTA", "DAKOTA", "CALUMET", "OHIO" };
        string[] nsNames = { "DIVISION", "LINCOLN", "MAIN", "WASHINGTON", "HARRIS", "WILLIAMS", "CHESTNUT", "BURROUGHS" };
        int mainStreetIndex = size.Columns / 2; // N–S streets index 0..Columns; MAIN sits in the middle

        // --- column plan: shared lot frontages per column, so facing rows align in all block rows.
        var colFrontages = new List<float>[size.Columns];
        var colLotCounts = new int[size.Columns];
        for (int c = 0; c < size.Columns; c++)
        {
            int n = rng.RangeInt(size.LotsMin, size.LotsMax + 1);
            colLotCounts[c] = n;
            colFrontages[c] = new List<float>();
            for (int i = 0; i < n; i++) colFrontages[c].Add(rng.Range(55f, 80f));
        }
        float totalWidth = 0f;
        foreach (var f in colFrontages) totalWidth += f.Sum();
        totalWidth += StreetRow * (size.Columns + 1);

        // --- N–S street x positions.
        var nsX = new float[size.Columns + 1];
        {
            float x = 0f;
            for (int s = 0; s <= size.Columns; s++)
            {
                nsX[s] = x;
                if (s < size.Columns) x += StreetRow + colFrontages[s].Sum();
            }
        }

        // --- Z layout: highway, commercial band, then the E–W grid.
        float zHighway = 0f;
        float zBand = HighwayWidth;
        float zFirstStreet = zBand + BandDepth;
        float[] ewZ = new float[size.EWStreets];
        for (int i = 0; i < size.EWStreets; i++) ewZ[i] = zFirstStreet + i * streetSpacing;
        float lastStreetEnd = ewZ[size.EWStreets - 1] + StreetRow;
        float totalDepth = lastStreetEnd + lotDepth;   // final north-facing lot row is the town edge

        var map = new MapPlan
        {
            Title = $"SMALL TOWN — {townName} — SEED {seed} ({size.Label})",
            Seed = seed,
            WidthFeet = totalWidth,
            DepthFeet = totalDepth,
            RoadSurface = "asphalt",
        };

        // --- roads: highway (dashed), then E–W grid streets (no paint), then N–S mains.
        map.Roads.Add(new MapRect(0, zHighway, totalWidth, HighwayWidth));
        map.RoadDashed.Add(true);
        map.Labels.Add(new MapLabel { Position = new Vec2(totalWidth / 2f, zHighway + HighwayWidth / 2f), Text = "HIGHWAY 18", Large = true });
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

        // Street names.
        for (int i = 0; i < size.EWStreets; i++)
            map.Labels.Add(new MapLabel { Position = new Vec2(totalWidth / 2f, ewZ[i] + StreetRow / 2f), Text = ewNames[i] + " AVENUE" });
        for (int s = 0; s <= size.Columns; s++)
        {
            string nm = s == mainStreetIndex ? "MAIN" : nsNames[s < mainStreetIndex ? s : s - 1];
            map.Labels.Add(new MapLabel
            {
                Position = new Vec2(nsX[s] + StreetRow / 2f, zBand + (lastStreetEnd - zBand) * 0.62f),
                Text = nm + " STREET",
                RotateDeg = -90f,
            });
        }

        // --- commercial band along the highway. Main street extends south through it.
        float mainX = nsX[mainStreetIndex];
        var padLabels = new List<string> { "CASEY'S", "FEED & SEED", "CO-OP", "HARDWARE", "BAR & GRILL", "AUTO ELECTRIC" };
        var padXs = new List<float>();
        {
            // pads on both sides of the main-street junction, avoiding the roadway
            float x = 60f;
            int guard = 0;
            while (padXs.Count < size.CommercialPads && guard++ < 40)
            {
                float w = rng.Range(75f, 130f);
                float gap = rng.Range(28f, 55f);
                if (x < mainX - 10f || x > mainX + StreetRow + 10f)
                    padXs.Add(x);
                x += w + gap;
                if (x + w > totalWidth - 40f) break;
            }
        }
        int pi = 0;
        foreach (float px in padXs)
        {
            float w = rng.Range(75f, 115f);
            float d = rng.Range(52f, 72f);
            map.Outbuildings.Add(new Outbuilding
            {
                Kind = OutbuildingKind.Storefront,
                Center = new Vec2(px + w / 2f, zBand + d / 2f + 8f),
                WidthFeet = w,
                DepthFeet = d,
                Label = padLabels[pi % padLabels.Count],
            });
            pi++;
        }
        // Grain elevator + bin row, east of the main street — the Iowa marker.
        {
            float evW = 48f, evD = 88f;
            float evX = mainX + StreetRow + 55f;
            float evZ = zBand + BandDepth / 2f;
            map.Outbuildings.Add(new Outbuilding
            {
                Kind = OutbuildingKind.GrainElevator,
                Center = new Vec2(evX + evW / 2f, evZ),
                WidthFeet = evW,
                DepthFeet = evD,
                Label = "ELEVATOR",
            });
            int bins = rng.RangeInt(4, 7);
            float binD = 22f, spacing = binD + 8f;
            float binRowW = bins * spacing - 8f;
            float binX = evX + (evW - binRowW) / 2f;
            for (int i = 0; i < bins; i++)
                map.Outbuildings.Add(new Outbuilding
                {
                    Kind = OutbuildingKind.GrainBin,
                    Center = new Vec2(binX + i * spacing + binD / 2f, evZ + evD / 2f + 10f + binD / 2f),
                    DiameterFeet = binD,
                    Label = "",
                });
        }

        // --- decide civic reserve lots (one anchor each): park, school, two churches.
        // Face index: row (0..2*EWStreets-2) × col. Rows: for street pair (i, i+1),
        // face A = north row of street i (faces south), face B = south row of street i+1.
        int faceRows = 2 * (size.EWStreets - 1);
        var faceIds = new List<(int row, int col)>();
        for (int r = 0; r < faceRows; r++)
            for (int c = 0; c < size.Columns; c++)
                faceIds.Add((r, c));
        var civic = new Dictionary<(int, int), OutbuildingKind>();
        var civicPicks = new List<(int, int)>();
        while (civicPicks.Count < 5 && faceIds.Count > 0)
        {
            int idx = rng.RangeInt(0, faceIds.Count);
            civicPicks.Add(faceIds[idx]);
            faceIds.RemoveAt(idx);
        }
        OutbuildingKind[] civicKinds = { OutbuildingKind.CityPark, OutbuildingKind.School, OutbuildingKind.Church, OutbuildingKind.Church, OutbuildingKind.CityPark };
        for (int i = 0; i < civicPicks.Count; i++) civic[civicPicks[i]] = civicKinds[i];

        // --- per-block alleys (seeded ~55%), per blockFace fill.
        var alleyCache = new Dictionary<(int, int), bool>();
        bool BlockAlley(int blockRow, int col)
        {
            var key = (blockRow, col);
            if (!alleyCache.TryGetValue(key, out bool has))
            {
                has = rng.Chance(0.55f);
                alleyCache[key] = has;
                if (has)
                {
                    float aZ = ewZ[blockRow] + StreetRow + lotDepth;
                    float bX = nsX[col] + StreetRow;
                    map.Alleys.Add(new MapRect(bX, aZ, colFrontages[col].Sum(), AlleyRow));
                }
            }
            return has;
        }

        float lotX0(int col) => nsX[col] + StreetRow;

        // Fill one lot facing an E–W street.
        uint houseSeed = seed * 2654435761u + 97u;
        void FillTownLot(int faceRow, int streetIdx, int col, int lotIdx, bool facesSouth, float lotX, float frontage, OutbuildingKind? anchor, bool alley)
        {
            float streetZ = ewZ[streetIdx];
            float rowZ0 = facesSouth ? streetZ + StreetRow : streetZ - lotDepth; // back-to-front handled below
            map.LotLines.Add(new MapRect(lotX, rowZ0, frontage, lotDepth));

            float setback = rng.Range(9f, 20f);
            float frontLineZ = facesSouth ? streetZ + StreetRow : streetZ;
            uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u;

            if (anchor.HasValue)
            {
                var a = PlaceAnchor(anchor.Value, lotX, rowZ0, frontage, lotDepth, frontLineZ, facesSouth);
                map.Outbuildings.Add(a);
                map.Walks.Add(new Driveway { Surface = "concrete", WidthFeet = 5f, Points = { new Vec2(a.Center.X, frontLineZ), new Vec2(a.Center.X, a.Center.Z) } });
                ScatterTrees(map, rng, rng.RangeInt(3, 6), lotX + 5f, lotX + frontage - 5f, rowZ0 + 5f, rowZ0 + lotDepth - 5f, null, 6f);
                return;
            }

            string arch = Weighted(rng);
            var house = HouseGenerator.Generate(arch, hSeed);
            float hx = lotX + MathF.Max(2f, (frontage - house.WidthFeet) / 2f + rng.Range(-3f, 3f));
            bool mirrored = !facesSouth;
            if (mirrored) house = house.FlipZ();
            float offsetZ = facesSouth ? frontLineZ + setback : frontLineZ - setback;
            house = house.OffsetCopy(hx, offsetZ);
            map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = mirrored });

            var fd = FindFrontDoor(house);
            map.Walks.Add(new Driveway
            {
                Surface = "concrete",
                WidthFeet = 4f,
                Points = { new Vec2(fd.X, facesSouth ? frontLineZ + 4.5f : frontLineZ - 4.5f), fd },
            });

            var (garageDoor, garageWall, frontEntry) = FindGarageDoor(house);
            bool garageViaAlley = alley && rng.Chance(0.6f);
            if (garageViaAlley)
            {
                float dgW = 22f, dgD = 26f;
                float dgCX = lotX + frontage / 2f + rng.Range(-6f, 6f);
                float dgZ = facesSouth ? rowZ0 + lotDepth - 9f - dgD / 2f : rowZ0 + 9f + dgD / 2f;
                map.Outbuildings.Add(new Outbuilding
                {
                    Kind = OutbuildingKind.DetachedGarage,
                    Center = new Vec2(dgCX, dgZ),
                    WidthFeet = dgW,
                    DepthFeet = dgD,
                    Label = "DET. GARAGE",
                });
                float alleyZ = facesSouth ? rowZ0 + lotDepth - 8f : rowZ0 + 8f;
                map.Driveways.Add(new Driveway { Surface = "gravel", WidthFeet = 10f, Points = { new Vec2(dgCX, alleyZ), new Vec2(dgCX, dgZ) } });
            }
            else
            {
                if (float.IsNaN(garageDoor.X))
                {
                    bool right = rng.Chance(0.5f);
                    float dgW = 20f, dgD = 22f;
                    float dgX = right ? lotX + frontage - dgW - 3f : lotX + 3f;
                    float dgZ = facesSouth
                        ? Math.Clamp(house.Footprint.Select(p => p.Z).Max() + 12f, frontLineZ + 26f, rowZ0 + lotDepth - dgD - 4f)
                        : Math.Clamp(house.Footprint.Select(p => p.Z).Min() - 12f - dgD, rowZ0 + 22f, rowZ0 + lotDepth - dgD - 4f);
                    map.Outbuildings.Add(new Outbuilding
                    {
                        Kind = OutbuildingKind.DetachedGarage,
                        Center = new Vec2(dgX + dgW / 2f, dgZ + dgD / 2f),
                        WidthFeet = dgW,
                        DepthFeet = dgD,
                        Label = "DET. GARAGE",
                    });
                    garageDoor = new Vec2(dgX + dgW / 2f, facesSouth ? dgZ : dgZ + dgD);
                    frontEntry = true;
                }
                bool garageRight = !frontEntry && GarageSideIsRight(house, garageWall!);
                float zStart = facesSouth ? frontLineZ + 2f : frontLineZ - 2f;
                map.Driveways.Add(DrivewayTo(garageDoor, frontEntry, garageRight, zStart, "concrete", 17f));
            }

            ScatterTrees(map, rng, rng.RangeInt(2, 5), lotX + 4f, lotX + frontage - 4f, rowZ0 + 5f, rowZ0 + lotDepth - 5f, new[] { house }, 5f);
        }

        // faceRow encoding: blockRow = faceRow/2, faceIdx = faceRow%2 (0 = north row of street blockRow, faces south; 1 = south row of street blockRow+1, faces north)
        for (int blockRow = 0; blockRow < size.EWStreets - 1; blockRow++)
        {
            int sa = blockRow;          // street whose north row this is
            int sb2 = blockRow + 1;     // street whose south row this is
            bool alley = col => BlockAlley(blockRow, col);
            for (int col = 0; col < size.Columns; col++)
            {
                float x = lotX0(col);
                // North row of street sa (faces south, toward sa).
                for (int i = 0; i < colFrontages[col].Count; i++)
                {
                    float f = colFrontages[col][i];
                    var anchor = civic.TryGetValue((blockRow * 2, col), out var k0) && i == 0 ? (OutbuildingKind?)k0 : null;
                    FillTownLot(blockRow * 2, sa, col, i, facesSouth: true, x, f, anchor, alley(col));
                    x += f;
                }
                // South row of street sb2 (faces north, toward sb2).
                x = lotX0(col);
                for (int i = 0; i < colFrontages[col].Count; i++)
                {
                    float f = colFrontages[col][i];
                    var anchor = civic.TryGetValue((blockRow * 2 + 1, col), out var k1) && i == 0 ? (OutbuildingKind?)k1 : null;
                    FillTownLot(blockRow * 2 + 1, sb2, col, i, facesSouth: false, x, f, anchor, alley(col));
                    x += f;
                }
            }
        }

        // --- public sidewalks both sides of every E–W street; street trees on both sides.
        foreach (var z in ewZ)
        {
            foreach (var wz in new[] { z + 2.25f, z + StreetRow - 2.25f })
            {
                map.Walks.Add(new Driveway { Surface = "concrete", WidthFeet = 4.5f, Points = { new Vec2(0, wz), new Vec2(totalWidth, wz) } });
            }
            for (float x = 16f; x < totalWidth - 16f; x += rng.Range(30f, 46f))
            {
                if (nsX.Any(sx => MathF.Abs(x - (sx + StreetRow / 2f)) < 26f)) continue;
                if (rng.Chance(0.72f)) map.Trees.Add(new Vec2(x, z + 8f));
                if (rng.Chance(0.72f)) map.Trees.Add(new Vec2(x + 13f, z + StreetRow - 8f));
            }
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

    static Outbuilding PlaceAnchor(OutbuildingKind kind, float lotX, float rowZ0, float frontage, float lotDepth, float frontLineZ, bool facesSouth)
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
