using System;
using System.Collections.Generic;
using System.Linq;

namespace SpawnGuide
{

/// <summary>
/// Seeded site maps: a rural Iowa farmstead lot and a small-town block.
/// Informed by Lyon County, IA reference imagery (George, Craig, Struble;
/// section-line county roads, 1-mile grid). 1990s-era elements: pole-truss
/// machine shed, steel bins, ranch/split-level house. Existing pre-1990s
/// buildings are rare, optional, and labeled as such.
/// </summary>
public static class MapGenerator
{
    // ---------- shared helpers ----------

    static string WeightedArchetype(Rng rng, bool town)
    {
        float r = rng.NextFloat();
        if (town) return r < 0.50f ? "ranch" : r < 0.80f ? "split-level" : "farmhouse";
        return r < 0.45f ? "ranch" : r < 0.80f ? "split-level" : "farmhouse";
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

    /// <summary>True if a candidate outbuilding would land on the house or another building.</summary>
    static bool FootprintCollides(MapPlan map, HousePlan house, Outbuilding candidate, float pad)
    {
        float cw = candidate.Kind == OutbuildingKind.GrainBin ? candidate.DiameterFeet : candidate.WidthFeet;
        float cd = candidate.Kind == OutbuildingKind.GrainBin ? candidate.DiameterFeet : candidate.DepthFeet;
        float x0 = candidate.Center.X - cw / 2f - pad, x1 = candidate.Center.X + cw / 2f + pad;
        float z0 = candidate.Center.Z - cd / 2f - pad, z1 = candidate.Center.Z + cd / 2f + pad;

        float hx0 = house.Footprint.Min(p => p.X), hx1 = house.Footprint.Max(p => p.X);
        float hz0 = house.Footprint.Min(p => p.Z), hz1 = house.Footprint.Max(p => p.Z);
        if (x0 < hx1 && x1 > hx0 && z0 < hz1 && z1 > hz0) return true;

        foreach (var o in map.Outbuildings)
        {
            float ow = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet : o.WidthFeet;
            float od = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet : o.DepthFeet;
            float ox0 = o.Center.X - ow / 2f, ox1 = o.Center.X + ow / 2f;
            float oz0 = o.Center.Z - od / 2f, oz1 = o.Center.Z + od / 2f;
            if (x0 < ox1 && x1 > ox0 && z0 < oz1 && z1 > oz0) return true;
        }
        return false;
    }

    static void ScatterTrees(MapPlan map, Rng rng, int count, float minX, float maxX, float minZ, float maxZ,
        IReadOnlyList<HousePlan> avoid, float pad)
    {
        int placed = 0, attempts = 0;
        while (placed < count && attempts < 40)
        {
            attempts++;
            var p = new Vec2(rng.Range(minX, maxX), rng.Range(minZ, maxZ));
            if (avoid.Any(h => PointInHouse(p, h, pad))) continue;
            map.Trees.Add(p);
            placed++;
        }
    }

    // ---------- rural farmstead ----------

    public static MapPlan RuralLot(uint seed)
    {
        var rng = new Rng(seed);
        // Farmstead parcel only — the few-acre cluster, not the surrounding fields.
        // Fields are separate parcels in the world, not part of this lot.
        float lotW = rng.Range(215, 285);
        float lotD = rng.Range(300, 420);
        float acres = lotW * lotD / 43560f;

        var map = new MapPlan
        {
            Title = $"RURAL FARMSTEAD — SEED {seed}",
            Seed = seed,
            WidthFeet = lotW,
            DepthFeet = lotD,
        };

        // County road: 66-ft right-of-way, no centerline (Lyon County section-line practice).
        map.Roads.Add(new MapRect(0, -66, lotW, 66));
        map.Labels.Add(new MapLabel { Position = new Vec2(lotW / 2f, -33), Text = "COUNTY ROAD" });
        map.LotLines.Add(new MapRect(0, 0, lotW, lotD));

        // House a ways back from the section line, cluster around a yard.
        string arch = WeightedArchetype(rng, town: false);
        float hx = lotW * rng.Range(0.22f, 0.38f);
        float hz = rng.Range(38, 62);
        var house = HouseGenerator.Generate(arch, seed + 101u).OffsetCopy(hx, hz);
        map.Houses.Add(new PositionedHouse { Plan = house });

        float houseW = house.WidthFeet, houseD = house.DepthFeet;
        float houseTop = hz + houseD;

        var (garageDoor, garageWall, frontEntry) = FindGarageDoor(house);
        bool detached = float.IsNaN(garageDoor.X);
        if (detached)
        {
            // No integral garage (farmhouse): 1990s pole-built detached garage to the side.
            bool doorRight = rng.Chance(0.5f);
            float dgW = 22f, dgD = 24f;
            float dgX = doorRight ? house.Footprint.Select(p => p.X).Max() + 16f : house.Footprint.Select(p => p.X).Min() - 16f - dgW;
            dgX = Math.Clamp(dgX, 6, lotW - dgW - 6);
            float dgZ = hz + 4f;
            map.Outbuildings.Add(new Outbuilding
            {
                Kind = OutbuildingKind.DetachedGarage,
                Center = new Vec2(dgX + dgW / 2f, dgZ + dgD / 2f),
                WidthFeet = dgW,
                DepthFeet = dgD,
                Label = "DET. GARAGE 22×24",
            });
            garageDoor = new Vec2(dgX + dgW / 2f, dgZ);
            frontEntry = true;
        }

        bool garageRight = !frontEntry && GarageSideIsRight(house, garageWall!);

        // Machine shed behind the house — the big 1990s pole shed, doors to the yard.
        float shedW = rng.Pick(new[] { 36f, 40f, 48f });
        float shedD = rng.Pick(new[] { 30f, 36f, 40f });
        float shedX = garageRight
            ? lotW * rng.Range(0.06f, 0.18f)
            : lotW - lotW * rng.Range(0.08f, 0.20f) - shedW;
        shedX = Math.Clamp(shedX, 8, lotW - shedW - 8);
        float shedZ = houseTop + 26f;
        map.Outbuildings.Add(new Outbuilding
        {
            Kind = OutbuildingKind.MachineShed,
            Center = new Vec2(shedX + shedW / 2f, shedZ + shedD / 2f),
            WidthFeet = shedW,
            DepthFeet = shedD,
            Label = $"MACHINE SHED {shedW:0}×{shedD:0}",
        });
        float shedDoorX = shedX + shedW / 2f;

        // --- ONE driveway for the lot -------------------------------------------
        // Iowa farm properties are served by a single entrance — "a farm property
        // may be served by a single driveway for the residence, plus a second
        // driveway to allow access to farm buildings or grain bins" (Woodbury
        // County). So the lot gets one lane: it leaves the road at ~90°, runs up
        // the side of the house, carries on past the house to the machine shed and
        // the bin row, and the garage is a short branch off it in the yard.
        float houseMinX = house.Footprint.Select(p => p.X).Min();
        float houseMaxX = house.Footprint.Select(p => p.X).Max();
        // The lane runs up the side the machine shed is on, so it reaches the shed.
        bool laneLeft = shedX < houseMinX;
        float entryX = laneLeft ? houseMinX - 15f : houseMaxX + 15f;
        if (laneLeft && entryX < 8f) entryX = MathF.Min(houseMaxX + 15f, lotW - 10f);
        if (!laneLeft && entryX > lotW - 10f) entryX = MathF.Max(houseMinX - 15f, 8f);
        entryX = Math.Clamp(entryX, 8f, lotW - 10f);
        float yardZ = hz - 20f;    // the yard, in front of the house
        var lane = new Driveway { Surface = "gravel", WidthFeet = 12f };
        lane.Points.Add(new Vec2(entryX, -58f));     // out in the road, where it flares
        lane.Points.Add(new Vec2(entryX, 0f));       // the road's edge
        lane.Points.Add(new Vec2(entryX, yardZ));    // up to the yard
        lane.Points.Add(new Vec2(entryX, shedZ));     // past the house
        lane.Points.Add(new Vec2(shedDoorX, shedZ));  // along the shed front to its door
        map.Driveways.Add(lane);

        // The garage is a branch off the lane in the yard, not a second entrance.
        var spur = new Driveway { Surface = "gravel", WidthFeet = 12f };
        spur.Points.Add(new Vec2(entryX, yardZ));
        if (frontEntry)
        {
            spur.Points.Add(new Vec2(garageDoor.X, yardZ));
            spur.Points.Add(garageDoor);
        }
        else
        {
            float laneX = garageDoor.X + (garageRight ? 9f : -9f);
            spur.Points.Add(new Vec2(laneX, yardZ));
            spur.Points.Add(new Vec2(laneX, garageDoor.Z));
            spur.Points.Add(garageDoor);
        }
        map.Driveways.Add(spur);

        // The walk leaves the road beside the lane, follows it up the yard, and
        // then curves off to the front door.
        var fd = FindFrontDoor(house);
        float walkX = Math.Clamp(fd.X, houseMinX - 4f, houseMaxX + 4f);
        var walk = new Driveway { WidthFeet = 3.5f, Surface = "concrete" };
        walk.Points.Add(new Vec2(entryX + (laneLeft ? 7f : -7f), 0f));
        walk.Points.Add(new Vec2(entryX + (laneLeft ? 7f : -7f), yardZ));
        walk.Points.Add(new Vec2(walkX, fd.Z - 12f));
        walk.Points.Add(fd);
        map.Walks.Add(walk);

        // Smaller shop / lean-to outbuilding beside the shed, 1990s build-out.
        if (rng.Chance(0.4f))
        {
            float shopW = 22f, shopD = 28f;
            float shopX = shedX + shedW + 10f;
            if (shopX + shopW > lotW - 6f) shopX = shedX - 10f - shopW;
            var shop = new Outbuilding
            {
                Kind = OutbuildingKind.PoleBarn,
                Center = new Vec2(shopX + shopW / 2f, shedZ + shedD / 2f),
                WidthFeet = shopW,
                DepthFeet = shopD,
                Label = "SHOP 22×28",
            };
            if (!FootprintCollides(map, house, shop, 3f)) map.Outbuildings.Add(shop);
        }

        // Steel bins in a row directly behind the shed — the signature aerial pattern.
        int binCount = rng.RangeInt(2, 6);
        float binDiam = rng.Pick(new[] { 18f, 21f, 24f });
        float binSpacing = binDiam + 8f;
        float binZ = shedZ + shedD + 8f + binDiam / 2f;
        float rowWidth = binCount * binSpacing - 8f;
        float rowX = Math.Clamp(shedX + shedW / 2f - rowWidth / 2f, 8f, lotW - rowWidth - 8f);
        for (int i = 0; i < binCount; i++)
        {
            map.Outbuildings.Add(new Outbuilding
            {
                Kind = OutbuildingKind.GrainBin,
                Center = new Vec2(rowX + i * binSpacing + binDiam / 2f, binZ),
                DiameterFeet = binDiam,
                Label = "",
            });
        }
        map.Labels.Add(new MapLabel { Position = new Vec2(rowX + rowWidth / 2f, binZ + binDiam / 2f + 8f), Text = $"STEEL BINS ×{binCount}" });

        // Optional pole barn deeper in the yard.
        float pbZ = binZ + binDiam / 2f + 12f + 22f;
        if (rng.Chance(0.45f) && pbZ + 44f <= lotD - 16f)
        {
            float pbW = 28f, pbD = 44f;
            float pbX = Math.Clamp(shedX + shedW / 2f - pbW / 2f + rng.Range(-30, 30), 8, lotW - pbW - 8);
            var pb = new Outbuilding
            {
                Kind = OutbuildingKind.PoleBarn,
                Center = new Vec2(pbX + pbW / 2f, pbZ + pbD / 2f),
                WidthFeet = pbW,
                DepthFeet = pbD,
                Label = "POLE BARN 28×44",
            };
            if (!FootprintCollides(map, house, pb, 3f)) map.Outbuildings.Add(pb);
        }

        // Rare existing pre-1990s building, kept for storage, honestly labeled.
        if (rng.Chance(0.2f))
        {
            float gW = 32f, gD = 54f;
            float gX = lotW * rng.Range(0.60f, 0.80f);
            float gZ = lotD - gD - 14f;
            var barn = new Outbuilding
            {
                Kind = OutbuildingKind.GambrelBarnExisting,
                Center = new Vec2(Math.Clamp(gX, 8, lotW - gW - 8) + gW / 2f, gZ),
                WidthFeet = gW,
                DepthFeet = gD,
                Label = "GAMBREL BARN — EXISTING",
            };
            if (!FootprintCollides(map, house, barn, 4f)) map.Outbuildings.Add(barn);
        }

        // LP gas tank near the house, away from the shed.
        if (rng.Chance(0.3f))
        {
            float lpX = garageRight ? house.Footprint.Select(p => p.X).Min() - 8f : house.Footprint.Select(p => p.X).Max() + 2f;
            map.Outbuildings.Add(new Outbuilding
            {
                Kind = OutbuildingKind.PropaneTank,
                Center = new Vec2(Math.Clamp(lpX, 4, lotW - 5) + 2.2f, hz + houseD - 6f),
                WidthFeet = 4.5f,
                DepthFeet = 11f,
                Label = "LP",
            });
        }

        // Shelterbelt ringing the building cluster on the north and west, like the
        // satellite farms — pre-1990 rows, still there.
        if (rng.Chance(0.55f))
        {
            float yardLeft = Math.Min(shedX, house.Footprint.Select(p => p.X).Min()) - 10f;
            float yardRight = Math.Max(shedX + shedW, rowX + rowWidth) + 6f;
            float beltNorth = binZ + binDiam / 2f + 8f;
            for (float x = yardLeft + 6f; x <= yardRight; x += 15f)
                map.Trees.Add(new Vec2(x + rng.Range(-2f, 2f), beltNorth));
            for (float z = hz + 10f; z <= beltNorth; z += 17f)
                map.Trees.Add(new Vec2(yardLeft, z + rng.Range(-2f, 2f)));
            map.Labels.Add(new MapLabel { Position = new Vec2((yardLeft + yardRight) / 2f, beltNorth + 7f), Text = "SHELTERBELT" });
        }

        map.Labels.Add(new MapLabel
        {
            Position = new Vec2(lotW - 6f, 8f),
            Text = $"{lotW:0}′ × {lotD:0}′ — {acres:0.#} AC",
            Anchor = "end",
        });
        return map;
    }

    // ---------- town block (both sides of one street) ----------

    const float StreetRow = 36f;      // town street ROW
    const float AlleyRow = 16f;       // alley width

    static void FillLot(MapPlan map, Rng rng, int index, float lotX, float frontage, float lotDepth,
        bool alley, bool southRow, uint seed)
    {
        float sign = southRow ? -1f : 1f;
        float rowZ0 = southRow ? -StreetRow - lotDepth : 0f; // back line of this row
        float frontLine = southRow ? -StreetRow : 0f;        // sidewalk edge at the street

        map.LotLines.Add(new MapRect(lotX, rowZ0, frontage, lotDepth));
        map.Labels.Add(new MapLabel
        {
            Position = new Vec2(lotX + frontage / 2f, southRow ? rowZ0 + (alley ? AlleyRow + 4f : 7f) : lotDepth - (alley ? AlleyRow + 4f : 7f)),
            Text = $"LOT {index + 1}",
        });

        string arch = WeightedArchetype(rng, town: true);
        float setback = rng.Range(10f, 22f);
        var house = HouseGenerator.Generate(arch, seed * 31u + 17u * (uint)(index + 1) + (southRow ? 7u : 0u));
        float hx = lotX + MathF.Max(2f, (frontage - house.WidthFeet) / 2f + rng.Range(-3f, 3f));
        bool mirrored = southRow;
        if (southRow) house = house.FlipZ();
        // After a flip the footprint lives in z ∈ [-D, 0]; place its front on the sidewalk spur.
        float offsetZ = southRow ? -StreetRow - setback : setback;
        house = house.OffsetCopy(hx, offsetZ);
        map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = mirrored });

        var fd = FindFrontDoor(house);
        // Spur from the public sidewalk to the door.
        map.Walks.Add(new Driveway
        {
            Surface = "concrete",
            WidthFeet = 4f,
            Points = { new Vec2(fd.X, southRow ? -4.5f : 4.5f), fd },
        });

        var (garageDoor, garageWall, frontEntry) = FindGarageDoor(house);
        bool garageViaAlley = alley && rng.Chance(0.6f);
        if (garageViaAlley)
        {
            // Rear detached pole garage, door onto the alley. No street driveway.
            float dgW = 22f, dgD = 26f;
            float dgCenterX = lotX + frontage / 2f + rng.Range(-6f, 6f);
            float dgZ = southRow ? rowZ0 + 9f + dgD / 2f : lotDepth - 9f - dgD / 2f;
            map.Outbuildings.Add(new Outbuilding
            {
                Kind = OutbuildingKind.DetachedGarage,
                Center = new Vec2(dgCenterX, dgZ),
                WidthFeet = dgW,
                DepthFeet = dgD,
                Label = "DET. GARAGE",
            });
            float alleyZ = southRow ? rowZ0 + 8f : lotDepth - 8f;
            map.Driveways.Add(new Driveway
            {
                Surface = "gravel",
                WidthFeet = 10f,
                Points = { new Vec2(dgCenterX, alleyZ), new Vec2(dgCenterX, dgZ) },
            });
        }
        else
        {
            if (float.IsNaN(garageDoor.X))
            {
                // Farmhouse without integral garage: pole-built garage beside the house.
                bool right = rng.Chance(0.5f);
                float dgW = 20f, dgD = 22f;
                float dgX = right ? lotX + frontage - dgW - 3f : lotX + 3f;
                float dgZ = southRow
                    ? rowZ0 + lotDepth * 0.25f
                    : Math.Clamp(house.Footprint.Select(p => p.Z).Max() + 12f, 30f, lotDepth - dgD - 4f);
                map.Outbuildings.Add(new Outbuilding
                {
                    Kind = OutbuildingKind.DetachedGarage,
                    Center = new Vec2(dgX + dgW / 2f, dgZ + dgD / 2f),
                    WidthFeet = dgW,
                    DepthFeet = dgD,
                    Label = "DET. GARAGE",
                });
                garageDoor = new Vec2(dgX + dgW / 2f, southRow ? dgZ + dgD : dgZ);
                frontEntry = true;
            }
            bool sideEntry = !frontEntry;
            bool garageRight = sideEntry && GarageSideIsRight(house, garageWall!);
            map.Driveways.Add(DrivewayTo(garageDoor, frontEntry, garageRight, zStart: southRow ? -31f : -31f, surface: "concrete", width: 17f));
        }

        // Yard trees — the canopy you see from above in these towns.
        ScatterTrees(map, rng, rng.RangeInt(2, 5), lotX + 4f, lotX + frontage - 4f,
            southRow ? rowZ0 + 8f : 6f, southRow ? frontLine - 4f : lotDepth - 4f,
            new[] { house }, pad: 5f);
    }

    public static MapPlan TownBlock(uint seed)
    {
        var rng = new Rng(seed);
        string street = rng.Pick(new[] { "OAK", "MAPLE", "CEDAR", "ELM", "WASHINGTON", "MAIN", "COLLEGE", "SECOND" })
            + (rng.Chance(0.7f) ? " STREET" : " AVENUE");
        int lotCount = 4;
        float lotDepth = rng.Range(120, 140);
        bool alley = rng.Chance(0.35f);

        var frontages = new List<float>();
        float blockW = 0f;
        for (int i = 0; i < lotCount; i++)
        {
            float f = rng.Range(72, 88);
            frontages.Add(f);
            blockW += f;
        }

        float totalDepth = lotDepth * 2 + StreetRow + (alley ? AlleyRow * 2 : 0f);
        var map = new MapPlan
        {
            Title = $"TOWN BLOCK — {street} — SEED {seed}",
            Seed = seed,
            WidthFeet = blockW,
            DepthFeet = totalDepth,
            RoadCenterDashes = true,
        };

        // Street in the middle, 36-ft ROW, yellow centerline, both rows front onto it.
        map.Roads.Add(new MapRect(0, -StreetRow, blockW, StreetRow));
        map.Labels.Add(new MapLabel { Position = new Vec2(blockW / 2f, -StreetRow / 2f), Text = street, Large = true });

        // Public sidewalks on both edges of the street.
        foreach (var z in new[] { 2.25f, -StreetRow + 2.25f })
        {
            map.Walks.Add(new Driveway
            {
                Surface = "concrete",
                WidthFeet = 4.5f,
                Points = { new Vec2(0, z), new Vec2(blockW, z) },
            });
        }

        if (alley)
        {
            map.Alleys.Add(new MapRect(0, lotDepth - 8f, blockW, AlleyRow));
            map.Alleys.Add(new MapRect(0, -StreetRow - lotDepth - 8f, blockW, AlleyRow));
            map.Labels.Add(new MapLabel { Position = new Vec2(blockW - 6f, lotDepth - 8f + 3f), Text = "ALLEY", Anchor = "end" });
            map.Labels.Add(new MapLabel { Position = new Vec2(blockW - 6f, -StreetRow - lotDepth + 3f), Text = "ALLEY", Anchor = "end" });
        }

        float lotX = 0f;
        for (int i = 0; i < lotCount; i++)
        {
            float frontage = frontages[i];
            FillLot(map, rng, i, lotX, frontage, lotDepth, alley, southRow: false, seed);
            FillLot(map, rng, i, lotX, frontage, lotDepth, alley, southRow: true, seed + 53u);
            lotX += frontage;
        }

        // Street trees along both sidewalks.
        for (float x = 14f; x <= blockW - 14f; x += rng.Range(28f, 46f))
        {
            if (rng.Chance(0.75f)) map.Trees.Add(new Vec2(x, 7f));
            if (rng.Chance(0.75f)) map.Trees.Add(new Vec2(x + 12f, -StreetRow - 7f));
        }

        // Shift everything so the south row starts at z = 0 — the renderer works in
        // all-positive map space with the street in the middle.
        float shift = StreetRow + lotDepth + (alley ? AlleyRow : 0f);
        var shifted = ShiftMap(map, shift);
        return shifted;
    }

    static MapPlan ShiftMap(MapPlan map, float dz)
    {
        Vec2 M(Vec2 v) => new(v.X, v.Z + dz);
        var s = new MapPlan
        {
            Title = map.Title,
            Seed = map.Seed,
            WidthFeet = map.WidthFeet,
            DepthFeet = map.DepthFeet,
            RoadCenterDashes = map.RoadCenterDashes,
        };
        foreach (var r in map.Roads) s.Roads.Add(new MapRect(r.X, r.Z + dz, r.W, r.H));
        foreach (var r in map.Alleys) s.Alleys.Add(new MapRect(r.X, r.Z + dz, r.W, r.H));
        foreach (var r in map.LotLines) s.LotLines.Add(new MapRect(r.X, r.Z + dz, r.W, r.H));
        foreach (var w in map.Walks)
        {
            var d = new Driveway { WidthFeet = w.WidthFeet, Surface = w.Surface };
            foreach (var p in w.Points) d.Points.Add(M(p));
            s.Walks.Add(d);
        }
        foreach (var d in map.Driveways)
        {
            var n = new Driveway { WidthFeet = d.WidthFeet, Surface = d.Surface };
            foreach (var p in d.Points) n.Points.Add(M(p));
            s.Driveways.Add(n);
        }
        foreach (var h in map.Houses)
        {
            s.Houses.Add(new PositionedHouse { Plan = h.Plan.OffsetCopy(0, dz), MirroredZ = h.MirroredZ });
        }
        foreach (var o in map.Outbuildings)
        {
            s.Outbuildings.Add(new Outbuilding
            {
                Kind = o.Kind,
                Center = M(o.Center),
                WidthFeet = o.WidthFeet,
                DepthFeet = o.DepthFeet,
                DiameterFeet = o.DiameterFeet,
                Label = o.Label,
            });
        }
        foreach (var t in map.Trees) s.Trees.Add(M(t));
        foreach (var l in map.Labels)
            s.Labels.Add(new MapLabel { Position = M(l.Position), Text = l.Text, Large = l.Large, RotateDeg = l.RotateDeg, Anchor = l.Anchor });
        return s;
    }
}
}
