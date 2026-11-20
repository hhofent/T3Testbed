using System;
using System.Collections.Generic;
using System.Linq;
#nullable enable
namespace SpawnGuide
{

/// <summary>
/// Seeded generator for 1990s rural Iowa house footprints. Emits already-fastened
/// wall segments (framed per Builder-Guy Stock dimensions) with openings placed —
/// a placement guide, not a construction simulation.
/// </summary>
public static class HouseGenerator
{
    public static readonly string[] Archetypes = { "ranch", "farmhouse", "split-level" };

    /// <param name="garageMode">"integral" (default) | "detached" | "none" — integral bakes the
    /// attached wing into the plan; detached/none produce a garage-free rectangle (the map layer
    /// then places a pole-built detached garage, or none at all). Farmhouse has no integral mode.</param>
    public static HousePlan Generate(string archetype, uint seed, string garageMode = "integral", HouseConfig? config = null) => archetype switch
    {
        "ranch" => Ranch(seed, garageMode, config),
        "farmhouse" => Farmhouse(seed, config),
        "split-level" => SplitLevel(seed, garageMode, config),
        _ => throw new ArgumentException($"Unknown archetype '{archetype}'. Expected one of: {string.Join(", ", Archetypes)}"),
    };

    static readonly string[] DefaultSiding1990s =
    {
        "Ivory vinyl", "Sage vinyl", "Desert Tan vinyl", "Light Blue vinyl",
        "Beige vinyl", "Brick front + Ivory vinyl sides", "White vinyl",
    };

    static HousePlan Ranch(uint seed, string garageMode, HouseConfig? config)
    {
        var cfg = config ?? new HouseConfig();
        var rng = new Rng(seed);
        float w = rng.Range(cfg.RanchWidth.Min, cfg.RanchWidth.Max);
        float d = rng.Range(cfg.RanchDepth.Min, cfg.RanchDepth.Max);
        float gw = rng.Range(cfg.RanchGarageWidth.Min, cfg.RanchGarageWidth.Max);
        float gd = rng.Range(cfg.RanchGarageDepth.Min, cfg.RanchGarageDepth.Max);
        bool garageLeft = rng.Chance(cfg.RanchGarageLeftChance);

        var plan = BasePlan("ranch", seed, storeys: 1,
            roof: "3.5/12 gable, 3-tab asphalt shingle",
            siding: rng.Pick<string>((cfg.SidingOptions.Length > 0 ? cfg.SidingOptions.Select(e => e.ToString()).ToArray() : DefaultSiding1990s).ToList()));

        if (garageMode == "integral")
        {
            BuildLFootprint(plan, w, d, gw, gd, garageLeft, rng, cfg);
            AnnotateGarage(plan, w, d, gw, gd, garageLeft);
        }
        else
        {
            // Garage-free rectangle; the map layer places a detached garage or none.
            AddQuad(plan, new Vec2(0, 0), new Vec2(w, 0), new Vec2(w, d), new Vec2(0, d),
                EdgeTag.MainFront, EdgeTag.Right, EdgeTag.Back, EdgeTag.Left);
            PlaceFrontOpenings(plan, EdgeTag.MainFront, rng, doorCentered: false, cfg);
            PlaceWindowsOn(plan, EdgeTag.Back, rng, Math.Max(2, (int)MathF.Round(w / 13f)), cfg);
            PlaceWindowsOn(plan, EdgeTag.Left, rng, Math.Max(1, (int)MathF.Round(d / 13f)), cfg);
            PlaceWindowsOn(plan, EdgeTag.Right, rng, Math.Max(1, (int)MathF.Round(d / 13f)), cfg);
        }
        return Normalize(plan);
    }

    static HousePlan Farmhouse(uint seed, HouseConfig? config)
    {
        var cfg = config ?? new HouseConfig();
        var rng = new Rng(seed);
        float w = rng.Range(cfg.FarmhouseWidth.Min, cfg.FarmhouseWidth.Max);
        float d = rng.Range(cfg.FarmhouseDepth.Min, cfg.FarmhouseDepth.Max);

        var plan = BasePlan("farmhouse", seed, storeys: 2,
            roof: "6/12 gable, 3-tab asphalt, white 1×4 trim",
            siding: rng.Pick<string>((cfg.SidingOptions.Length > 0 ? cfg.SidingOptions.Select(e => e.ToString()).ToArray() : DefaultSiding1990s).ToList()));

        // Simple rectangle, four tagged edges.
        AddQuad(plan, new Vec2(0, 0), new Vec2(w, 0), new Vec2(w, d), new Vec2(0, d),
            EdgeTag.MainFront, EdgeTag.Right, EdgeTag.Back, EdgeTag.Left);

        // Full-width-ish front porch, dashed — no wall, just a covered slab.
        float porchW = w * rng.Range(cfg.FarmhousePorchWidthFraction.Min, cfg.FarmhousePorchWidthFraction.Max);
        float porchD = rng.Range(cfg.FarmhousePorchDepth.Min, cfg.FarmhousePorchDepth.Max);
        plan.Annotations.Add(new PlanAnnotation
        {
            Position = new Vec2((w - porchW) / 2f + porchW / 2f, -porchD / 2f),
            Text = $"PORCH {porchD:0.#} FT",
            DashedHalfExtents = new Vec2(porchW / 2f, porchD / 2f),
        });

        PlaceFrontOpenings(plan, EdgeTag.MainFront, rng, doorCentered: true, cfg);
        PlaceWindowsOn(plan, EdgeTag.Back, rng, Math.Max(2, (int)MathF.Round(w / 13f)), cfg);
        PlaceWindowsOn(plan, EdgeTag.Left, rng, Math.Max(1, (int)MathF.Round(d / 13f)), cfg);
        PlaceWindowsOn(plan, EdgeTag.Right, rng, Math.Max(1, (int)MathF.Round(d / 13f)), cfg);
        return Normalize(plan);
    }

    static HousePlan SplitLevel(uint seed, string garageMode, HouseConfig? config)
    {
        var cfg = config ?? new HouseConfig();
        var rng = new Rng(seed);
        float w = rng.Range(cfg.SplitLevelWidth.Min, cfg.SplitLevelWidth.Max);
        float d = rng.Range(cfg.SplitLevelDepth.Min, cfg.SplitLevelDepth.Max);
        float gw = rng.Range(cfg.SplitLevelGarageWidth.Min, cfg.SplitLevelGarageWidth.Max);
        float gd = rng.Range(cfg.SplitLevelGarageDepth.Min, cfg.SplitLevelGarageDepth.Max);
        bool garageLeft = rng.Chance(cfg.SplitLevelGarageLeftChance);

        var plan = BasePlan("split-level", seed, storeys: 2,
            roof: "4/12 offset gable, 3-tab asphalt shingle — split entry",
            siding: rng.Pick<string>((cfg.SidingOptions.Length > 0 ? cfg.SidingOptions.Select(e => e.ToString()).ToArray() : DefaultSiding1990s).ToList()));

        if (garageMode == "integral")
        {
            // Same L footprint as the ranch, but the garage door faces the side
            // (classic split-level with side-entry garage).
            BuildLFootprint(plan, w, d, gw, gd, garageLeft, rng, cfg, garageDoorOnSide: true);
            AnnotateGarage(plan, w, d, gw, gd, garageLeft);
        }
        else
        {
            AddQuad(plan, new Vec2(0, 0), new Vec2(w, 0), new Vec2(w, d), new Vec2(0, d),
                EdgeTag.MainFront, EdgeTag.Right, EdgeTag.Back, EdgeTag.Left);
            PlaceFrontOpenings(plan, EdgeTag.MainFront, rng, doorCentered: false, cfg);
            PlaceWindowsOn(plan, EdgeTag.Back, rng, Math.Max(2, (int)MathF.Round(w / 13f)), cfg);
            PlaceWindowsOn(plan, EdgeTag.Left, rng, Math.Max(1, (int)MathF.Round(d / 13f)), cfg);
            PlaceWindowsOn(plan, EdgeTag.Right, rng, Math.Max(1, (int)MathF.Round(d / 13f)), cfg);
        }
        return Normalize(plan);
    }

    static HousePlan BasePlan(string archetype, uint seed, int storeys, string roof, string siding) =>
        new()
        {
            Archetype = archetype,
            Seed = seed,
            Storeys = storeys,
            RoofNote = roof,
            SidingNote = siding,
        };

    static void BuildLFootprint(HousePlan plan, float w, float d, float gw, float gd,
        bool garageLeft, Rng rng, HouseConfig cfg, bool garageDoorOnSide = false)
    {
        // Tagged outline from the front-left corner, garage on the right unless
        // mirrored. Front is at z = 0. AddEdge also accumulates the Footprint polygon.
        if (!garageLeft)
        {
            AddEdge(plan, new Vec2(0, 0), new Vec2(w, 0), EdgeTag.MainFront);
            AddEdge(plan, new Vec2(w, 0), new Vec2(w + gw, 0), EdgeTag.GarageFront);
            AddEdge(plan, new Vec2(w + gw, 0), new Vec2(w + gw, gd), EdgeTag.GarageSide);
            AddEdge(plan, new Vec2(w + gw, gd), new Vec2(w, gd), EdgeTag.GarageBack);
            AddEdge(plan, new Vec2(w, gd), new Vec2(w, d), EdgeTag.Right);
            AddEdge(plan, new Vec2(w, d), new Vec2(0, d), EdgeTag.Back);
            AddEdge(plan, new Vec2(0, d), new Vec2(0, 0), EdgeTag.Left);
        }
        else
        {
            AddEdge(plan, new Vec2(w, 0), new Vec2(0, 0), EdgeTag.MainFront);
            AddEdge(plan, new Vec2(0, 0), new Vec2(-gw, 0), EdgeTag.GarageFront);
            AddEdge(plan, new Vec2(-gw, 0), new Vec2(-gw, gd), EdgeTag.GarageSide);
            AddEdge(plan, new Vec2(-gw, gd), new Vec2(0, gd), EdgeTag.GarageBack);
            AddEdge(plan, new Vec2(0, gd), new Vec2(0, d), EdgeTag.Left);
            AddEdge(plan, new Vec2(0, d), new Vec2(w, d), EdgeTag.Back);
            AddEdge(plan, new Vec2(w, d), new Vec2(w, 0), EdgeTag.Right);
        }

        PlaceFrontOpenings(plan, EdgeTag.MainFront, rng, doorCentered: false, cfg);

        var garageFront = FindWall(plan, EdgeTag.GarageFront);
        var garageSide = FindWall(plan, EdgeTag.GarageSide);
        if (garageDoorOnSide)
        {
            garageSide!.Openings.Add(new WallOpening
            {
                OffsetFeet = garageSide.LengthFeet / 2f,
                WidthFeet = 9f,
                Kind = OpeningKind.GarageDoor,
            });
        }
        else
        {
            float doorWidth = garageFront!.LengthFeet >= 17f
                ? cfg.GarageDoorWidthFt.Sample(rng)
                : cfg.GarageDoorWidthFt.Min; // Use bimodal distribution
            garageFront!.Openings.Add(new WallOpening
            {
                OffsetFeet = garageFront.LengthFeet / 2f,
                WidthFeet = doorWidth,
                Kind = OpeningKind.GarageDoor,
            });
        }

        // Side and back windows. The garage-side wall runs only from the garage
        // roof up... i.e. from gd to d; it is short, so at most one window.
        foreach (var tag in new[] { EdgeTag.Left, EdgeTag.Right })
        {
            var wall = FindWall(plan, tag);
            if (wall is null) continue;
            int count = wall.LengthFeet >= cfg.TwoWindowMinWall ? 2 : wall.LengthFeet >= cfg.OneWindowMinWall ? 1 : 0;
            if (count > 0) PlaceWindowsOn(plan, tag, rng, count, cfg);
        }
        PlaceWindowsOn(plan, EdgeTag.Back, rng, Math.Max(2, (int)MathF.Round(w / 14f)), cfg);
    }

    static void AddEdge(HousePlan plan, Vec2 a, Vec2 b, EdgeTag tag)
    {
        plan.Walls.Add(new WallSegment
        {
            Start = a,
            End = b,
            Tag = tag,
            HeightFeet = plan.Storeys * BuilderGuyDims.WallHeightFeet,
            ThicknessFeet = BuilderGuyDims.ExteriorThicknessFeet,
        });
        plan.Footprint.Add(a);
    }

    static void AddQuad(HousePlan plan, Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3,
        EdgeTag t0, EdgeTag t1, EdgeTag t2, EdgeTag t3)
    {
        AddEdge(plan, p0, p1, t0);
        AddEdge(plan, p1, p2, t1);
        AddEdge(plan, p2, p3, t2);
        AddEdge(plan, p3, p0, t3);
    }

    static WallSegment? FindWall(HousePlan plan, EdgeTag tag) =>
        plan.Walls.FirstOrDefault(w => w.Tag == tag);

    static void PlaceFrontOpenings(HousePlan plan, EdgeTag tag, Rng rng, bool doorCentered, HouseConfig cfg)
    {
        var wall = FindWall(plan, tag);
        if (wall is null) return;
        float len = wall.LengthFeet;

        float doorOffset = doorCentered ? len / 2f : len * rng.Range(cfg.DoorOffsetFraction.Min, cfg.DoorOffsetFraction.Max);
        wall.Openings.Add(new WallOpening { OffsetFeet = doorOffset, WidthFeet = 3f, Kind = OpeningKind.Door });

        // Windows in the stretch of wall away from the door.
        float freeStart = doorOffset + 3.5f;
        float freeEnd = len - 2.5f;
        float freeLen = freeEnd - freeStart;
        int count = freeLen >= 16 ? 2 : freeLen >= 8 ? 1 : 0;
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : (i + 0.5f) / count;
            float jitter = rng.Range(-cfg.FrontWindowJitterFraction.Max, cfg.FrontWindowJitterFraction.Max) * freeLen;
            wall.Openings.Add(new WallOpening
            {
                OffsetFeet = freeStart + t * freeLen + jitter,
                WidthFeet = 3f,
                Kind = OpeningKind.Window,
            });
        }
    }

    static void PlaceWindowsOn(HousePlan plan, EdgeTag tag, Rng rng, int count, HouseConfig cfg)
    {
        var wall = FindWall(plan, tag);
        if (wall is null) return;
        float len = wall.LengthFeet;
        float usable = len - 6f; // stay clear of corners
        if (usable < 3f) return;

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : (i + 0.5f) / count;
            float jitter = rng.Range(-cfg.SideWindowJitterFraction.Max, cfg.SideWindowJitterFraction.Max) * usable;
            float offset = 3f + t * usable + jitter;
            offset = Math.Clamp(offset, 2.5f, len - 2.5f);
            wall.Openings.Add(new WallOpening { OffsetFeet = offset, WidthFeet = 3f, Kind = OpeningKind.Window });
        }
    }

    static void AnnotateGarage(HousePlan plan, float w, float d, float gw, float gd, bool garageLeft)
    {
        float cx = garageLeft ? -gw / 2f : w + gw / 2f;
        plan.Annotations.Add(new PlanAnnotation { Position = new Vec2(cx, gd / 2f), Text = "GARAGE" });
    }

    /// <summary>Shift everything so the footprint bounds start at (0, 0).</summary>
    static HousePlan Normalize(HousePlan plan)
    {
        float minX = plan.Walls.SelectMany(w => new[] { w.Start.X, w.End.X }).Min();
        float minZ = plan.Walls.SelectMany(w => new[] { w.Start.Z, w.End.Z }).Min();
        if (minX == 0 && minZ == 0) return plan;

        var shift = new Vec2(-minX, -minZ);
        foreach (var wall in plan.Walls) { wall.Start += shift; wall.End += shift; }
        for (int i = 0; i < plan.Footprint.Count; i++) plan.Footprint[i] += shift;
        foreach (var ann in plan.Annotations) ann.Position += shift;
        return plan;
    }
}
}
