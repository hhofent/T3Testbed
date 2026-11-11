using System;
using System.Collections.Generic;
using System.Linq;

namespace SpawnGuide
{

/// <summary>
/// The town in its countryside context — the test view for the infinite-map work.
/// A 2-mile square cell (two sections) with the 1-mile section-grid roads, large
/// field fills, scattered farmsteads, and the town seeded at the central grid
/// junction (the real pattern: towns sit at section-line corners). HWY 18 runs
/// through the town and continues to the cell edges, like the real Highway 18 at
/// George / Boyden / Hull.
/// The town grows with each test via --size hamlet|boyden|hull; town geometry
/// comes from SmallTownGenerator and is shifted into the cell.
/// Always RoadSurface "asphalt" for now (section roads read as blacktop).
/// The 3-mile cell and the town density field are NOT here yet — this is the
/// canvas those will ride on.
/// </summary>
public static class CountrysideMap
{
    public const float CellFeet = 10560f;      // 2 miles
    const float SectionRoad = 66f;             // 66-ft section-line ROW
    const float SectionMid = 5280f;            // one section-line cross at the middle

    public static MapPlan Generate(uint seed, string sizeName)
    {
        var town = SmallTownGenerator.Generate(seed, sizeName);

        var rng = new Rng(seed * 7919u + 13u);
        float cell = CellFeet;

        var map = new MapPlan
        {
            Title = $"COUNTRYSIDE — 2 MI VIEW — SEED {seed} ({sizeName.ToUpperInvariant()})",
            Seed = seed,
            WidthFeet = cell,
            DepthFeet = cell,
            RoadSurface = "asphalt",
            LodSimple = true,
        };

        // --- fields: two seeded split lines per axis, nine big muted rects.
        var fieldFills = new[] { "#ded9bd", "#d6d0b4", "#cbc4a2", "#e4e0cb", "#d2cbb0", "#dcd6b8" };
        var xs = new List<float> { 0f, cell };
        var ys = new List<float> { 0f, cell };
        foreach (float vx in new[] { cell * 0.28f, cell * 0.62f }) xs.Insert(xs.Count - 1, vx + rng.Range(-160f, 160f));
        foreach (float hz in new[] { cell * 0.3f, cell * 0.68f }) ys.Insert(ys.Count - 1, hz + rng.Range(-160f, 160f));
        xs.Sort(); ys.Sort();
        int fi = 0;
        for (int ix = 0; ix < xs.Count - 1; ix++)
            for (int iy = 0; iy < ys.Count - 1; iy++)
            {
                map.Fields.Add(new MapRect(xs[ix], ys[iy], xs[ix + 1] - xs[ix], ys[iy + 1] - ys[iy]));
                map.FieldFills.Add(fieldFills[(fi + (int)(seed % 7)) % fieldFills.Length]);
                fi++;
            }

        // --- town origin: centered on the section-line junction.
        float townX0 = SectionMid - town.WidthFeet / 2f;
        float townZ0 = SectionMid - town.DepthFeet / 2f;
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
        while (placedFarms < 4 && farmAttempts++ < 40)
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

        // --- shelterbelt lines along the interior section roads (sparse gaps).
        for (float x = 80f; x < cell; x += rng.Range(90f, 150f))
        {
            if (rng.Chance(0.7f)) map.Trees.Add(new Vec2(x, SectionMid + 46f + rng.Range(-4f, 4f)));
            if (rng.Chance(0.7f)) map.Trees.Add(new Vec2(x, SectionMid - 46f + rng.Range(-4f, 4f)));
        }
        for (float z = 80f; z < cell; z += rng.Range(90f, 150f))
        {
            if (rng.Chance(0.7f)) map.Trees.Add(new Vec2(SectionMid + 46f + rng.Range(-4f, 4f), z));
            if (rng.Chance(0.7f)) map.Trees.Add(new Vec2(SectionMid - 46f + rng.Range(-4f, 4f), z));
        }

        map.Labels.Add(new MapLabel
        {
            Position = new Vec2(cell - 30f, 40f),
            Text = $"1-MILE SECTION GRID · {town.Houses.Count} HOUSES IN TOWN",
            Anchor = "end",
        });
        return map;
    }
}
}
