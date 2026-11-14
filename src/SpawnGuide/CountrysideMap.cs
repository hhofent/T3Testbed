using System;
using System.Collections.Generic;
using System.Linq;

namespace SpawnGuide
{

/// <summary>
/// The town in its countryside context — the test view for the infinite-map work.
/// A 3-mile square cell (three sections) with the 1-mile section-grid roads, large
/// field fills, scattered farmsteads, and the town seeded at the central grid
/// junction (the real pattern: towns sit at section-line corners). HWY 18 runs
/// through the town and continues to the cell edges, like a real state highway
/// through a small town.
/// The town grows with each test via --size crossroads|hamlet|village|town; town geometry
/// comes from SmallTownGenerator and is shifted into the cell.
/// detail=false → far-LOD (silhouettes, trunk dots) for the zoomed-out view;
/// detail=true  → everything (walls, captions) for the viewer's close-in layer.
/// Always RoadSurface "asphalt" for now (section roads read as blacktop).
/// The town density field is NOT here yet — this is the canvas it will ride on.
/// </summary>
public static class CountrysideMap
{
    public const float CellFeet = 15840f;      // 3 miles
    const float SectionRoad = 66f;             // 66-ft section-line ROW
    const float SectionMid = 7920f;            // one section-line cross at the middle

    /// <param name="edge">null → town centered on the interior junction (full town);
    /// "west"|"north" → town straddles that cell boundary — partial town, mostly rural (user's request).</param>
    /// <param name="config">Optional configuration for countryside generation parameters.</param>
    public static MapPlan Generate(uint seed, string sizeName, bool detail = false, string? edge = null, CountrysideConfig? config = null, SpawnConfig? townConfig = null)
    {
        var cfg = config ?? new CountrysideConfig();
        var town = SmallTownGenerator.Generate(seed, sizeName, detail: detail, config: townConfig?.SmallTown, townTiers: townConfig?.TownTiers, commercial: townConfig?.Commercial);

        var rng = new Rng(seed * 7919u + 13u);
        float cell = cfg.CellFeet;

        var map = new MapPlan
        {
            Title = $"3 MI CELL — SEED {seed} ({sizeName.ToUpperInvariant()})",
            Seed = seed,
            WidthFeet = cell,
            DepthFeet = cell,
            RoadSurface = "asphalt",
            LodSimple = !detail,
            // Captions stay off at every zoom — they are 11-FEET tall in plan units and smear
            // the moment you zoom. Geometry is the detail layer's job. Floor plans come later as their own layer.
            QuietAnnotations = true,
        };

        // --- fields: 1/3 and 2/3 split lines per axis, jittered off the grid, nine muted rects.
        var fieldFills = new[] { "#ded9bd", "#d6d0b4", "#cbc4a2", "#e4e0cb", "#d2cbb0", "#dcd6b8" };
        var xs = new List<float> { 0f, cell };
        var ys = new List<float> { 0f, cell };
        foreach (float vx in new[] { cell / 3f, cell * 2f / 3f }) xs.Insert(xs.Count - 1, vx + rng.Range(-220f, 220f));
        foreach (float hz in new[] { cell / 3f, cell * 2f / 3f }) ys.Insert(ys.Count - 1, hz + rng.Range(-220f, 220f));
        xs.Sort(); ys.Sort();
        int fi = 0;
        for (int ix = 0; ix < xs.Count - 1; ix++)
            for (int iy = 0; iy < ys.Count - 1; iy++)
            {
                map.Fields.Add(new MapRect(xs[ix], ys[iy], xs[ix + 1] - xs[ix], ys[iy + 1] - ys[iy]));
                map.FieldFills.Add(fieldFills[(fi + (int)(seed % 7)) % fieldFills.Length]);
                fi++;
            }

        // --- town origin: centered on the section-line junction, or hugging a cell boundary.
        float townX0 = SectionMid - town.WidthFeet / 2f;
        float townZ0 = SectionMid - town.DepthFeet / 2f;
        if (edge == "west")
        {
            townX0 = -town.WidthFeet * 0.35f;              // ~65% of the town inside the cell
            townZ0 = SectionMid - town.DepthFeet / 2f;
        }
        else if (edge == "north")
        {
            townX0 = SectionMid - town.WidthFeet / 2f;
            townZ0 = cell - town.DepthFeet * 0.45f;        // clipped by the cell's north boundary
        }
        float townX1 = townX0 + town.WidthFeet;
        float townZ1 = townZ0 + town.DepthFeet;

        // --- section-line roads (66-ft ROW, no centerline). Interior cross split
        // around the town; boundary roads run full length.
        void RoadV(float x, float z0, float z1)
        {
            if (z1 - z0 <= 0f) return;
            map.Roads.Add(new MapRect(x - SectionRoad / 2f, z0, SectionRoad, z1 - z0));
            map.RoadDashed.Add(false);
        }
        void RoadH(float z, float x0, float x1)
        {
            if (x1 - x0 <= 0f) return;
            map.Roads.Add(new MapRect(x0, z - SectionRoad / 2f, x1 - x0, SectionRoad));
            map.RoadDashed.Add(false);
        }
        RoadV(0f, 0f, cell); RoadV(cell, 0f, cell);
        RoadH(0f, 0f, cell); RoadH(cell, 0f, cell);
        RoadV(SectionMid, 0f, townZ0 - 4f); RoadV(SectionMid, townZ1 + 4f, cell);
        RoadH(SectionMid, 0f, townX0 - 4f); RoadH(SectionMid, townX1 + 4f, cell);

        // --- the highway continues through the town to the cell edges (HWY 18).
        map.Roads.Add(new MapRect(0f, townZ0, townX0, 66f)); map.RoadDashed.Add(true);
        map.Roads.Add(new MapRect(townX1, townZ0, cell - townX1, 66f)); map.RoadDashed.Add(true);

        // --- merge the town plan into the cell (shift by town origin).
        for (int ri = 0; ri < town.Roads.Count; ri++)
        {
            var r = town.Roads[ri];
            map.Roads.Add(new MapRect(r.X + townX0, r.Z + townZ0, r.W, r.H));
            map.RoadDashed.Add(ri < town.RoadDashed.Count ? town.RoadDashed[ri] : false);
        }
        foreach (var r in town.Alleys) map.Alleys.Add(new MapRect(r.X + townX0, r.Z + townZ0, r.W, r.H));
        foreach (var r in town.LotLines) map.LotLines.Add(new MapRect(r.X + townX0, r.Z + townZ0, r.W, r.H));
        foreach (var w in town.Walks)
        {
            var d = new Driveway { WidthFeet = w.WidthFeet, Surface = w.Surface };
            foreach (var p in w.Points) d.Points.Add(new Vec2(p.X + townX0, p.Z + townZ0));
            map.Walks.Add(d);
        }
        foreach (var d in town.Driveways)
        {
            var n = new Driveway { WidthFeet = d.WidthFeet, Surface = d.Surface };
            foreach (var p in d.Points) n.Points.Add(new Vec2(p.X + townX0, p.Z + townZ0));
            map.Driveways.Add(n);
        }
        foreach (var h in town.Houses)
            map.Houses.Add(new PositionedHouse { Plan = h.Plan.OffsetCopy(townX0, townZ0), MirroredZ = h.MirroredZ });
        foreach (var o in town.Outbuildings)
        {
            map.Outbuildings.Add(new Outbuilding
            {
                Kind = o.Kind,
                Center = new Vec2(o.Center.X + townX0, o.Center.Z + townZ0),
                WidthFeet = o.WidthFeet,
                DepthFeet = o.DepthFeet,
                DiameterFeet = o.DiameterFeet,
                Label = o.Label,
            });
        }
        foreach (var t in town.Trees) map.Trees.Add(new Vec2(t.X + townX0, t.Z + townZ0));
        foreach (var l in town.Labels)
            map.Labels.Add(new MapLabel { Position = new Vec2(l.Position.X + townX0, l.Position.Z + townZ0), Text = l.Text, Large = l.Large, RotateDeg = l.RotateDeg, Anchor = l.Anchor });

        // --- scattered farmsteads: house + machine shed + bins + tree ring,
        //     near a grid road, clear of the town.
        int placedFarms = 0, farmAttempts = 0;
        while (placedFarms < 6 && farmAttempts++ < 60)
        {
            float fx = rng.Range(700f, cell - 700f);
            float fz = rng.Range(700f, cell - 700f);
            float distRoad = MathF.Min(MathF.Min(MathF.Abs(fx - SectionMid), MathF.Abs(fz - SectionMid)),
                                       MathF.Min(MathF.Min(fx, cell - fx), MathF.Min(fz, cell - fz)));
            if (distRoad > 320f) continue;
            var fp = new Vec2(fx, fz);
            if (fp.X >= townX0 - 520f && fp.X <= townX1 + 520f && fp.Z >= townZ0 - 520f && fp.Z <= townZ1 + 520f) continue;
            map.Outbuildings.Add(new Outbuilding { Kind = OutbuildingKind.PoleBarn, Center = new Vec2(fx, fz), WidthFeet = 42f, DepthFeet = 48f, Label = "FARMSTEAD" });
            map.Outbuildings.Add(new Outbuilding { Kind = OutbuildingKind.MachineShed, Center = new Vec2(fx + 44f, fz + 52f), WidthFeet = 34f, DepthFeet = 55f, Label = "" });
            map.Outbuildings.Add(new Outbuilding { Kind = OutbuildingKind.GrainBin, Center = new Vec2(fx + 64f, fz + 96f), DiameterFeet = 15f, Label = "" });
            map.Outbuildings.Add(new Outbuilding { Kind = OutbuildingKind.GrainBin, Center = new Vec2(fx + 86f, fz + 96f), DiameterFeet = 15f, Label = "" });
            for (int i = 0; i < 10; i++)
            {
                float ang = i * MathF.PI * 2f / 10f + rng.Range(-0.2f, 0.2f);
                map.Trees.Add(new Vec2(fx + MathF.Cos(ang) * rng.Range(55f, 95f), fz + MathF.Sin(ang) * rng.Range(55f, 95f)));
            }
            placedFarms++;
        }

        // --- a few grove clusters out in the fields — never on a lot, never beside a road.
        // (The farmstead belts stay — the farmstead pattern is wanted; trees alongside
        //  roads or on lots are not.)
        bool InsideTown(float x, float z, float pad) =>
            x >= townX0 - pad && x <= townX1 + pad && z >= townZ0 - pad && z <= townZ1 + pad;
        for (int g = 0; g < 6; g++)
        {
            float gx = rng.Range(600f, cell - 600f);
            float gz = rng.Range(600f, cell - 600f);
            if (MathF.Abs(gx - SectionMid) < 320f || MathF.Abs(gz - SectionMid) < 320f) continue;   // clear of the grid roads
            if (InsideTown(gx, gz, 600f)) continue;
            int n = rng.RangeInt(6, 13);
            for (int i = 0; i < n; i++)
            {
                float ang = i * MathF.PI * 2f / n + rng.Range(-0.3f, 0.3f);
                map.Trees.Add(new Vec2(gx + MathF.Cos(ang) * rng.Range(35f, 95f), gz + MathF.Sin(ang) * rng.Range(35f, 95f)));
            }
        }

        if (edge != null) ClipToCell(map, cell);

        map.Labels.Add(new MapLabel
        {
            Position = new Vec2(cell - 30f, 40f),
            Text = $"1-MILE SECTION GRID · {town.Houses.Count} HOUSES IN TOWN",
            Anchor = "end",
        });
        return map;
    }

    // Partial-town cells: everything not fully inside the cell is cut away — a cropped
    // aerial view where the town continues past the frame, the way a lens sees it.
    static void ClipToCell(MapPlan map, float cell)
    {
        bool In(float x, float z) => x > 4f && z > 4f && x < cell - 4f && z < cell - 4f;
        bool Any(Driveway d) => d.Points.Any(p => In(p.X, p.Z));

        map.Houses.RemoveAll(h =>
        {
            float cx = h.Plan.Footprint.Select(p => p.X).Average();
            float cz = h.Plan.Footprint.Select(p => p.Z).Average();
            return !In(cx, cz);
        });
        map.Outbuildings.RemoveAll(o => !In(o.Center.X, o.Center.Z));
        map.LotLines.RemoveAll(r => !In(r.X + r.W / 2f, r.Z + r.H / 2f));
        map.Alleys.RemoveAll(r => !In(r.X + r.W / 2f, r.Z + r.H / 2f));
        map.Walks.RemoveAll(Any);
        map.Driveways.RemoveAll(Any);
        map.Trees.RemoveAll(t => !In(t.X, t.Z));
        map.Labels.RemoveAll(l => !In(l.Position.X, l.Position.Z));
        map.Bulbs.RemoveAll(b => !In(b.Center.X, b.Center.Z));

        for (int i = map.Roads.Count - 1; i >= 0; i--)
        {
            var r = map.Roads[i];
            float x0 = MathF.Max(r.X, 0f), x1 = MathF.Min(r.X + r.W, cell);
            float z0 = MathF.Max(r.Z, 0f), z1 = MathF.Min(r.Z + r.H, cell);
            if (x1 - x0 <= 0.5f || z1 - z0 <= 0.5f)
            {
                map.Roads.RemoveAt(i);
                map.RoadDashed.RemoveAt(i);
            }
            else if (x0 != r.X || z0 != r.Z || x1 - x0 != r.W || z1 - z0 != r.H)
            {
                map.Roads[i] = new MapRect(x0, z0, x1 - x0, z1 - z0);
            }
        }
    }
}
}
