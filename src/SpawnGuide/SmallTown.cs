using System;
using System.Collections.Generic;
using System.Linq;

namespace SpawnGuide
{

/// <summary>
/// What the world knows about a town's surroundings that the town itself cannot
/// work out. The world owns the rail lattice; the town needs to know where the
/// branch spur comes in so it can put its grain elevator and its rail-side
/// industry on the railroad.
/// </summary>
public sealed class TownContext
{
    /// <summary>True when the town is served by a branch spur off the rail.</summary>
    public bool HasRailSpur;
    /// <summary>The z of the rail line the spur leaves from, in world feet.</summary>
    public float RailZ;
    /// <summary>The spur's x offset from the town's centre, in feet.</summary>
    public float SpurOffsetFt;

    public static readonly TownContext None = new TownContext();
}

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
    const float FourSideChance = 0.85f; // most blocks get houses on all four sides
    const float BandDepth = 175f;     // commercial band between highway and first grid street

    /// <summary>
    /// The characteristic extras an Iowa town may have. Research:
    /// `docs/research/2026-11-11-iowa-town-composition.md`.
    /// </summary>
    enum TownExtra
    {
        /// <summary>A short street off the perimeter ending in a bulb, with houses on it.</summary>
        Court,
        /// <summary>A street that runs out of town and stops at the edge of the plat.</summary>
        PlatEdge,
        /// <summary>A road beyond the town with a few farm houses strung along it.</summary>
        OutlyingRoad,
        /// <summary>The old highway, now a quiet parallel street with a few shops on it.</summary>
        BypassedHighway,
        /// <summary>A short street running from the highway up into the grid.</summary>
        IndustrialSideStreet,
        /// <summary>A green square where the middle streets cross.</summary>
        TownSquare,
        /// <summary>A wide street with a planted median down the middle.</summary>
        Boulevard,
    }

    sealed class TownSize
    {
        public int Columns;       // block columns (N–S street count = Columns + 1)
        public int EWStreets;     // E–W grid streets (block rows = EWStreets - 1)
        public int LotsMin, LotsMax;
        public int CommercialPads;
        public int CivicAnchors;
        public int DeadEnds;
        public int Tier;          // 0 crossroads, 1 hamlet, 2 village, 3 town
        public string Label = "";
    }

    static readonly Dictionary<string, TownSize> Sizes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Generic placeholder sizes. No reference-town names are used in the world.
        // Every town gets one dead-end street; only the biggest get a second.
        ["crossroads"] = new TownSize { Columns = 2, EWStreets = 2, LotsMin = 2, LotsMax = 3, CommercialPads = 3, CivicAnchors = 1, DeadEnds = 1, Tier = 0, Label = "CROSSROADS" },
        ["hamlet"] = new TownSize { Columns = 4, EWStreets = 3, LotsMin = 3, LotsMax = 4, CommercialPads = 6, CivicAnchors = 2, DeadEnds = 1, Tier = 1, Label = "HAMLET" },
        ["village"] = new TownSize { Columns = 7, EWStreets = 5, LotsMin = 3, LotsMax = 5, CommercialPads = 10, CivicAnchors = 4, DeadEnds = 1, Tier = 2, Label = "VILLAGE" },
        ["town"] = new TownSize { Columns = 10, EWStreets = 6, LotsMin = 3, LotsMax = 5, CommercialPads = 14, CivicAnchors = 5, DeadEnds = 2, Tier = 3, Label = "TOWN" },
        // Old preset names stay as aliases so existing commands keep working.
        ["boyden"] = new TownSize { Columns = 7, EWStreets = 5, LotsMin = 3, LotsMax = 5, CommercialPads = 10, CivicAnchors = 4, DeadEnds = 1, Tier = 2, Label = "VILLAGE" },
        ["hull"] = new TownSize { Columns = 10, EWStreets = 6, LotsMin = 3, LotsMax = 5, CommercialPads = 14, CivicAnchors = 5, DeadEnds = 2, Tier = 3, Label = "TOWN" },
    };

    static readonly string[] PlaceNames =
    {
        "MAPLE CROSSING", "ELM CREEK", "WALNUT GROVE", "CEDAR RIDGE", "PRAIRIE ROSE",
        "STONE BRIDGE", "NORTH BEND", "SILVER LAKE", "OAKDALE", "FAIRVIEW",
        "CLOVER HILL", "GRANDVIEW", "HICKORY GROVE", "PLEASANT VALLEY", "RIVERSIDE",
        "BRISTOL", "LINN GROVE", "COLD SPRING", "UNION CENTER", "HARTFORD",
        "BETHANY", "MORNINGSIDE", "SUNNYSIDE", "GREENFIELD", "BROOKVILLE",
        "PINEHURST", "RIDGEWAY", "LAKEVIEW", "MILLBROOK", "WESTFIELD",
        "GLENDALE", "HOMESTEAD", "PRAIRIE VIEW", "SPRINGDALE", "ASH GROVE",
        "BENTON", "CARLTON", "DOVER", "EASTON", "FRANKLIN",
    };

    /// <param name="detail">false → quiet annotations (natural-size embed). true → all captions on (zoom viewer's detail layer).</param>
    /// <param name="ctx">what the world knows about the town's surroundings — today,
    /// whether a branch spur brings the railroad to the town.</param>
    public static MapPlan Generate(uint seed, string sizeName, bool detail = false, TownContext? ctx = null)
    {
        ctx ??= TownContext.None;
        if (!Sizes.TryGetValue(sizeName, out var size))
            throw new ArgumentException($"unknown town size '{sizeName}' (have: {string.Join(", ", Sizes.Keys)})");

        var rng = new Rng(seed);
        float lotDepth = BlockLotDepth + rng.Range(-6f, 6f);
        int blockRows = size.EWStreets - 1;

        // --- what kind of town is this? -------------------------------------------
        // Iowa towns grew in layers, so no two have the same street plan. Each town
        // draws 0–2 characteristic extras from the researched set: an outlying road
        // with a few farm houses on it (Hull's 310th St), a bypassed old highway
        // running parallel to the new one, an industrial side-street off the
        // highway, a cul-de-sac court, a street that runs out to the edge of the
        // plat, or a town square in the middle of the grid.
        var extraPool = new List<TownExtra>();
        if (rng.Chance(0.50f)) extraPool.Add(TownExtra.Court);
        if (rng.Chance(0.45f)) extraPool.Add(TownExtra.PlatEdge);
        if (rng.Chance(0.45f)) extraPool.Add(TownExtra.OutlyingRoad);
        if (rng.Chance(0.35f)) extraPool.Add(TownExtra.BypassedHighway);
        if (rng.Chance(0.40f)) extraPool.Add(TownExtra.IndustrialSideStreet);
        if (rng.Chance(0.35f)) extraPool.Add(TownExtra.TownSquare);
        // A boulevard is a rarity — one town in six or so has one.
        if (rng.Chance(0.16f)) extraPool.Add(TownExtra.Boulevard);
        for (int i = extraPool.Count - 1; i > 0; i--)
        {
            int j = rng.RangeInt(0, i + 1);
            (extraPool[i], extraPool[j]) = (extraPool[j], extraPool[i]);
        }
        int extraCount = rng.Chance(0.35f) ? 0 : (rng.Chance(0.60f) ? 1 : 2);
        var extras = extraPool.Take(extraCount).ToList();
        bool hasCourt = extras.Contains(TownExtra.Court);
        bool hasPlatEdge = extras.Contains(TownExtra.PlatEdge);
        bool hasOutlyingRoad = extras.Contains(TownExtra.OutlyingRoad);
        bool hasBypassedHighway = extras.Contains(TownExtra.BypassedHighway);
        bool hasSideStreet = extras.Contains(TownExtra.IndustrialSideStreet);
        bool hasTownSquare = extras.Contains(TownExtra.TownSquare);
        bool hasBoulevard = extras.Contains(TownExtra.Boulevard);
        // The outlying road and the bypassed highway are the same kind of thing — a
        // street beyond the town edge — but they are dressed differently.
        bool hasOuterRoad = hasOutlyingRoad || hasBypassedHighway;

        // Per-block-row alley decision (row-level so street spacing is uniform per row).
        var rowAlley = new bool[blockRows];
        for (int r = 0; r < blockRows; r++) rowAlley[r] = rng.Chance(0.55f);
        float BlockDepth(int row) => 2 * lotDepth + (rowAlley[row] ? AlleyRow : 0f);

        // Placeholder names only — not borrowed from the reference towns. Replace with the
        // real names list when it lands.
        string townName = rng.Pick(PlaceNames);
        // Street names are drawn per town and shuffled, so no two towns in the world
        // carry the same street list in the same order. The pools are the ones Iowa
        // actually uses: state names, pioneer presidents, and tree names.
        string[] ewPool = { "IOWA", "MICHIGAN", "MINNESOTA", "DAKOTA", "CALUMET", "OHIO",
                            "NEBRASKA", "KANSAS", "ILLINOIS", "WISCONSIN", "ARKANSAS", "NEBASKA" };
        string[] nsPool = { "DIVISION", "LINCOLN", "WASHINGTON", "HARRIS", "WILLIAMS", "CHESTNUT",
                            "BURROUGHS", "ADA", "PARK", "ELEANOR", "VINE", "CHELSEA", "STONE",
                            "HUGH", "LOCUST", "DELTA", "JEFFERSON", "MADISON", "MONROE", "WILSON",
                            "MAPLE", "ELM", "ASH", "OAK", "CEDAR", "POPLAR", "WALNUT", "SPRUCE" };
        var ewNames = ewPool.OrderBy(_ => rng.NextUInt()).ToArray();
        var nsNames = nsPool.OrderBy(_ => rng.NextUInt()).ToArray();
        int mainStreetIndex = size.Columns / 2; // N–S street index; MAIN sits mid-town

        // --- column plan: shared lot frontages per column so facing rows align.
        var colFrontages = new List<List<float>>();
        for (int c = 0; c < size.Columns; c++)
        {
            int n = rng.RangeInt(size.LotsMin, size.LotsMax + 1);
            colFrontages.Add(new List<float>());
            for (int i = 0; i < n; i++) colFrontages[c].Add(rng.Range(55f, 80f));
        }
        float BlockWidth(int col) => colFrontages[col].Sum() + 2 * SideLotDepth;
        float totalWidth = StreetRow * (size.Columns + 1);
        for (int c = 0; c < size.Columns; c++) totalWidth += BlockWidth(c);

        var nsX = new List<float>();
        {
            float x = 0f;
            for (int s = 0; s <= size.Columns; s++)
            {
                nsX.Add(x);
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

        // The blob is the town's ragged silhouette: it trims the corners of the grid so
        // the outline is never a rectangle, but it must not cut off whole rows.
        blobCZ = (zBand + totalDepth) / 2f;
        blobRZ = (totalDepth - zBand) / 2f * 1.04f;
        blobCX = totalWidth / 2f;
        blobRX = totalWidth / 2f * 1.04f;
        hb1 = rng.RangeInt(2, 5);
        hb2 = rng.RangeInt(5, 9);
        ha1 = rng.Range(0.02f, 0.06f);
        ha2 = rng.Range(0.01f, 0.03f);
        hp1 = rng.Range(0f, MathF.PI * 2f);
        hp2 = rng.Range(0f, MathF.PI * 2f);
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

        // --- content tracking: streets end at the town's houses, never at the bounding square.
        var streetZ = Enumerable.Range(0, size.Columns + 1).Select(_ => (z0: float.MaxValue, z1: float.MinValue)).ToList();
        var streetX = Enumerable.Range(0, size.EWStreets).Select(_ => (x0: float.MaxValue, x1: float.MinValue)).ToArray();
        var rowFilled = new bool[blockRows];
        var colFilled = new bool[blockRows, size.Columns];
        var northEdgeFilled = new HashSet<int>();
        void RecordContent(int districtRow, int col, float x0, float x1, float z0, float z1)
        {
            for (int s = col; s <= col + 1 && s <= size.Columns; s++)
            {
                var t = streetZ[s];
                streetZ[s] = (MathF.Min(t.z0, z0), MathF.Max(t.z1, z1));
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
                else if (districtRow == blockRows) northEdgeFilled.Add(col);
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
        // Edge lots go vacant more often than interior ones, but a real town is mostly
        // built out. The one deliberately empty plat is the dead-end street below.
        bool IsVacantLot(float cx, float cz)
        {
            float r = BlobNormR(cx, cz);
            float p = r > 0.70f ? 0.14f : r > 0.50f ? 0.05f : 0.015f;
            return rng.Chance(p);
        }

        var map = new MapPlan
        {
            Title = $"SMALL TOWN — {townName} — SEED {seed} ({size.Label})",
            Seed = seed,
            PlaceName = townName,
            WidthFeet = totalWidth,
            DepthFeet = totalDepth,
            RoadSurface = "asphalt",
            QuietAnnotations = !detail,
        };

        // --- (roads are created after the column sweep — empty columns get dropped first,
        //      so the grid only includes blocks that actually have content.)

        // --- commercial band along the highway. Main street extends south to it.
        // The mix, the sizes, the names and the order are all drawn per town: two
        // towns on the same street never end up with the same row of shops. What
        // stays constant is the shape a real Iowa main street has — the grain
        // elevator by the main junction, the implement dealer and the gas station out
        // at the ends where there is room for them, the shops in between.
        float mainX = nsX[mainStreetIndex];
        var bandOcc = new List<(float x0, float x1)>();

        // An industrial side-street runs from the highway up into the grid, so it has
        // to be placed before the shops (which must keep clear of it).
        float sideStreetX = -1f;
        if (hasSideStreet)
        {
            sideStreetX = rng.Chance(0.5f)
                ? rng.Range(60f, MathF.Max(61f, mainX - StreetRow - 120f))
                : rng.Range(mainX + StreetRow + 120f, MathF.Max(mainX + StreetRow + 121f, totalWidth - 120f));
            sideStreetX = Math.Clamp(sideStreetX, 50f, MathF.Max(50f, totalWidth - 120f));
            bandOcc.Add((sideStreetX - StreetRow / 2f - 6f, sideStreetX + StreetRow / 2f + 6f));
        }

        // The elevator sits near the main junction — that is where every real Iowa
        // elevator is — but which side of MAIN it falls on varies.
        float evW = 48f, evD = 88f;
        bool elevatorRight = rng.Chance(0.62f);
        float evX0 = elevatorRight ? mainX + StreetRow + rng.Range(45f, 150f)
                                   : mainX - StreetRow - rng.Range(45f, 150f) - evW;
        // A grain elevator is a railroad institution: it stands at the end of the
        // branch spur, because that is where the grain comes in. The town's centre is
        // the middle of its grid, so the spur's offset puts it here.
        if (ctx.HasRailSpur)
        {
            float spurLocalX = totalWidth / 2f + ctx.SpurOffsetFt - evW / 2f;
            evX0 = Math.Clamp(spurLocalX, 30f, MathF.Max(30f, totalWidth - evW - 30f));
        }
        evX0 = Math.Clamp(evX0, 40f, MathF.Max(40f, totalWidth - evW - 40f));
        map.Outbuildings.Add(new Outbuilding
        {
            Kind = OutbuildingKind.GrainElevator,
            Center = new Vec2(evX0 + evW / 2f, zBand + BandDepth / 2f),
            WidthFeet = evW,
            DepthFeet = evD,
            Label = "ELEVATOR",
        });
        bandOcc.Add((evX0 - 12f, evX0 + evW + 12f));
        int bins = rng.RangeInt(4, 7);
        float binD = 22f, spacing = binD + 8f;
        float binRowW = bins * spacing - 8f;
        float binX = evX0 + (evW - binRowW) / 2f;
        if (binX + binRowW > totalWidth - 8f) binX = totalWidth - 8f - binRowW;
        if (binX < 8f) binX = 8f;
        for (int i = 0; i < bins; i++)
            map.Outbuildings.Add(new Outbuilding
            {
                Kind = OutbuildingKind.GrainBin,
                Center = new Vec2(binX + i * spacing + binD / 2f, zBand + BandDepth / 2f + evD / 2f + 10f + binD / 2f),
                DiameterFeet = binD,
                Label = "",
            });
        bandOcc.Add((binX - 8f, binX + binRowW + 8f));

        // --- pick this town's row of businesses -----------------------------------
        // A gas station is always in; everything else is drawn from what the town tier
        // can support. The order is then shuffled, so no two towns read alike.
        var eligible = BusinessCatalogue.Shops.Where(b => b.MinTier <= size.Tier).ToList();
        var chosen = new List<BusinessCatalogue.Business>();
        var gas = eligible.First(b => b.Descriptor == "GAS STATION");
        chosen.Add(gas);
        var rest = eligible.Where(b => b != gas).ToList();
        // Fisher–Yates on a copy, then take as many as the town has pads.
        for (int i = rest.Count - 1; i > 0; i--)
        {
            int j = rng.RangeInt(0, i + 1);
            (rest[i], rest[j]) = (rest[j], rest[i]);
        }
        for (int i = 0; i < size.CommercialPads - 1 && i < rest.Count; i++) chosen.Add(rest[i]);

        // Real main streets put the roomy businesses at the two ends and the small
        // ones in the middle, so keep the edge businesses at the ends.
        var edge = chosen.Where(b => b.Edge).ToList();
        var core = chosen.Where(b => !b.Edge).ToList();
        for (int i = core.Count - 1; i > 0; i--)
        {
            int j = rng.RangeInt(0, i + 1);
            (core[i], core[j]) = (core[j], core[i]);
        }
        var strip = new List<BusinessCatalogue.Business>();
        int leftSlots = edge.Count / 2;
        for (int i = 0; i < leftSlots; i++) strip.Add(edge[i]);
        strip.AddRange(core);
        for (int i = edge.Count - 1; i >= leftSlots; i--) strip.Add(edge[i]);

        // The industrial boxes: 1–2 on the highway edge, away from the main junction.
        int indCount = rng.Chance(0.55f) ? 2 : 1;
        var indEligible = BusinessCatalogue.Industrial.Where(b => b.MinTier <= size.Tier).ToList();
        var industrial = new List<BusinessCatalogue.Business>();
        for (int i = 0; i < indCount && indEligible.Count > 0; i++)
        {
            int pick = rng.RangeInt(0, indEligible.Count);
            industrial.Add(indEligible[pick]);
            indEligible.RemoveAt(pick);
        }

        // --- lay the strip out ----------------------------------------------------
        // The band runs the length of the town, broken by the main-street gap and by
        // the elevator. Buildings are placed in the chosen order, skipping the
        // occupied stretches.
        // The band's footprint is NOT trimmed by the town's ragged residential
        // silhouette: a real highway strip runs the full length of the town, and the
        // blob is a shape for the blocks, not for the road frontage.
        {
            var placed = new List<(BusinessCatalogue.Business biz, float x, float w, float d, string label)>();
            // The strip starts at a random point, so the shops are spread along the
            // road rather than bunched at one end.
            float x = rng.Range(40f, 260f);
            int guard = 0;
            foreach (var biz in strip)
            {
                float w = rng.Range(biz.MinWidth, biz.MaxWidth);
                float d = rng.Range(biz.MinDepth, biz.MaxDepth);
                // Find room for it: try the current position, then push right.
                int tries = 0;
                while (tries++ < 24)
                {
                    bool inMainGap = x + w > mainX - 12f && x < mainX + StreetRow + 12f;
                    bool inBandOcc = bandOcc.Any(b => x < b.x1 && x + w > b.x0);
                    if (x + w <= totalWidth - 35f && !inMainGap && !inBandOcc)
                        break;
                    x += 12f;
                }
                if (tries >= 24) continue;   // no room for this one; the town is full
                // A real main street is not a solid wall of shops: there are gaps,
                // yards and parking between them.
                float gap = rng.Range(45f, 140f);
                placed.Add((biz, x, w, d, BusinessCatalogue.LabelFor(rng, biz)));
                bandOcc.Add((x - 5f, x + w + 5f));
                x += w + gap;
                if (guard++ > 40) break;
            }
            foreach (var p in placed)
            {
                RecordContent(-1, 0, p.x, p.x + p.w, zBand, zBand + 80f);
                map.Outbuildings.Add(new Outbuilding
                {
                    Kind = OutbuildingKind.Storefront,
                    Center = new Vec2(p.x + p.w / 2f, zBand + p.d / 2f + 8f),
                    WidthFeet = p.w,
                    DepthFeet = p.d,
                    Label = p.label,
                });
            }

            // The industrial boxes go at whatever frontage is left, never straddling the
            // main junction.
            foreach (var biz in industrial)
            {
                float iw = rng.Range(biz.MinWidth, biz.MaxWidth), id = rng.Range(biz.MinDepth, biz.MaxDepth);
                float ix = rng.Range(45f, MathF.Max(46f, totalWidth - iw - 45f));
                bool straddles = ix < mainX + StreetRow + 20f && ix + iw > mainX - 20f;
                bool collides = bandOcc.Any(b => ix < b.x1 && ix + iw > b.x0);
                if (straddles || collides) continue;
                map.Outbuildings.Add(new Outbuilding
                {
                    Kind = OutbuildingKind.Storefront,
                    Center = new Vec2(ix + iw / 2f, zBand + id / 2f + 8f),
                    WidthFeet = iw,
                    DepthFeet = id,
                    Label = BusinessCatalogue.LabelFor(rng, biz),
                });
                bandOcc.Add((ix - 10f, ix + iw + 10f));
            }

            // --- the cross street gets its own shops -------------------------------
            // Commerce is not all on the highway: a town has shops on the street that
            // runs back from it as well, and they cluster near the junction.
            int crossShops = rng.RangeInt(0, 3);
            for (int i = 0; i < crossShops; i++)
            {
                var biz = eligible.Count > 0 ? eligible[rng.RangeInt(0, eligible.Count)] : null;
                if (biz == null) break;
                bool west = rng.Chance(0.5f);
                float w = rng.Range(biz.MinDepth, biz.MaxDepth);      // across the street
                float d = rng.Range(biz.MinWidth, biz.MaxWidth);      // along the street
                float cx = west ? mainX - StreetRow / 2f - 6f - w / 2f
                                : mainX + StreetRow / 2f + 6f + w / 2f;
                float cz = rng.Range(zBand + 24f, MathF.Max(zBand + 25f, zBand + BandDepth - d - 12f));
                bool clash = bandOcc.Any(b => cx - w / 2f < b.x1 && cx + w / 2f > b.x0);
                if (clash) continue;
                map.Outbuildings.Add(new Outbuilding
                {
                    Kind = OutbuildingKind.Storefront,
                    Center = new Vec2(cx, cz),
                    WidthFeet = w,
                    DepthFeet = d,
                    Label = BusinessCatalogue.LabelFor(rng, biz),
                });
                bandOcc.Add((cx - w / 2f - 6f, cx + w / 2f + 6f));
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
            // one industrial or storefront at the remaining side, drawn from the same
            // catalogue as the north strip so no two towns share a name. A crossroads
            // is too small for any of them, so it just gets houses down here.
            var southPool = BusinessCatalogue.Industrial.Where(b => b.MinTier <= size.Tier).ToList();
            if (southPool.Count > 0 && rng.Chance(0.6f))
            {
                var southBiz = southPool[rng.RangeInt(0, southPool.Count)];
                float iw = rng.Range(southBiz.MinWidth, southBiz.MaxWidth);
                float id = rng.Range(southBiz.MinDepth, southBiz.MaxDepth);
                float ix = totalWidth - iw - 45f;
                if (ix > x + 20f)
                {
                    map.Outbuildings.Add(new Outbuilding
                    {
                        Kind = OutbuildingKind.Storefront,
                        Center = new Vec2(ix + iw / 2f, southEdge + id / 2f + 10f),
                        WidthFeet = iw,
                        DepthFeet = id,
                        Label = BusinessCatalogue.LabelFor(rng, southBiz),
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
        // Which civic buildings a town has, and how many of each, varies with the seed
        // and with the town tier — a village is not a hamlet with more parks. What
        // stays constant is the shape of the list: a church is nearly always there,
        // the school appears once the town is big enough to have one, and a park is
        // the thing that turns a grid into a place.
        var civicKinds = new List<OutbuildingKind>();
        int anchors = size.CivicAnchors;
        // Church: the one near-universal. Two of them in the bigger towns.
        int churches = rng.Chance(0.72f) ? 2 : 1;
        if (size.Tier >= 2) churches = rng.Chance(0.5f) ? 2 : 1;
        if (size.Tier == 0) churches = rng.Chance(0.75f) ? 1 : 0;
        for (int i = 0; i < churches && civicKinds.Count < anchors; i++) civicKinds.Add(OutbuildingKind.Church);
        // School: a one-room schoolhouse in a crossroads, a real schoolhouse in a town.
        float schoolChance = size.Tier == 0 ? 0.3f : size.Tier == 1 ? 0.6f : 0.95f;
        if (civicKinds.Count < anchors && rng.Chance(schoolChance)) civicKinds.Add(OutbuildingKind.School);
        // Park.
        float parkChance = size.Tier == 0 ? 0.35f : size.Tier == 1 ? 0.6f : 0.85f;
        if (civicKinds.Count < anchors && rng.Chance(parkChance)) civicKinds.Add(OutbuildingKind.CityPark);
        // Fill any remaining slots with whatever is left, so a big town always uses
        // the anchors it has.
        var spare = new List<OutbuildingKind> { OutbuildingKind.Church, OutbuildingKind.CityPark, OutbuildingKind.School };
        while (civicKinds.Count < anchors && spare.Count > 0)
        {
            int k = rng.RangeInt(0, spare.Count);
            civicKinds.Add(spare[k]);
            spare.RemoveAt(k);
        }
        // The order the anchors go in is the town's, not the list's.
        for (int i = civicKinds.Count - 1; i > 0; i--)
        {
            int j = rng.RangeInt(0, i + 1);
            (civicKinds[i], civicKinds[j]) = (civicKinds[j], civicKinds[i]);
        }
        // A town square sits where the middle streets cross: the park goes on the
        // most central face, so the four streets around it frame the green.
        if (hasTownSquare && faceIds.Count > 0)
        {
            int bestIdx = 0;
            int bestScore = int.MaxValue;
            for (int i = 0; i < faceIds.Count; i++)
            {
                var f = faceIds[i];
                int score = Math.Abs(f.s - size.EWStreets / 2) * 100 + Math.Abs(f.col - size.Columns / 2);
                if (score < bestScore) { bestScore = score; bestIdx = i; }
            }
            var sq = faceIds[bestIdx];
            faceIds.RemoveAt(bestIdx);
            civic[sq] = OutbuildingKind.CityPark;
            // If the town was going to have a park anyway, the square is it.
            civicKinds.RemoveAll(k => k == OutbuildingKind.CityPark);
        }
        for (int i = 0; i < civicKinds.Count && faceIds.Count > 0; i++)
        {
            int idx = rng.RangeInt(0, faceIds.Count);
            civic[faceIds[idx]] = civicKinds[i];
            faceIds.RemoveAt(idx);
        }

        // --- the landmarks -------------------------------------------------------
        // Every small Iowa town has a water tower; the bigger ones have a library, a
        // fire hall, a fairground and a golf course. They go on a lot face when they
        // are buildings, and stand in their own ground when they are not.
        var freeLandmarks = new List<BusinessCatalogue.Landmark>();
        foreach (var lm in BusinessCatalogue.Landmarks)
        {
            if (lm.MinTier > size.Tier) continue;
            if (!rng.Chance(lm.Chance)) continue;
            if (lm.Kind == OutbuildingKind.Depot && !ctx.HasRailSpur) continue;   // no depot without a railroad
            if (lm.FreeStanding) { freeLandmarks.Add(lm); continue; }
            // A building: it takes a lot face like any other civic anchor.
            if (faceIds.Count == 0) continue;
            int pick = rng.RangeInt(0, faceIds.Count);
            var face = faceIds[pick];
            faceIds.RemoveAt(pick);
            civic[face] = lm.Kind;
        }

        void FillTownLot(int streetIdx, int col, int lotIdx, bool facesSouth, int districtRow, float lotX, float frontage, OutbuildingKind? anchor, bool alley, float? depthOverride = null)
        {
            float streetZ = ewZ[streetIdx];
            float dep = depthOverride ?? lotDepth;
            float rowZ0 = facesSouth ? streetZ + StreetRow : streetZ - dep;
            float cx = lotX + frontage / 2f, cz = rowZ0 + dep / 2f;
            // Ragged edge: the outermost lots of a block fall away, so the town ends
            // unevenly. The block itself is kept.
            if (!InsideBlob(cx, cz)) return;
            RecordContent(districtRow, col, lotX, lotX + frontage, rowZ0, rowZ0 + dep);
            // Seeded vacancy, weighted toward the ragged outskirts.
            if (!anchor.HasValue && IsVacantLot(cx, cz))
            {
                map.LotLines.Add(new MapRect(lotX, rowZ0, frontage, dep));
                return;
            }
            map.LotLines.Add(new MapRect(lotX, rowZ0, frontage, dep));

            float setback = rng.Range(8f, 17f);
            float frontLineZ = facesSouth ? streetZ + StreetRow : streetZ;
            int gRoll = rng.RangeInt(0, 100);
            // Most houses: integral garage. A real fraction get a detached pole garage instead.
            string garageMode = gRoll < 45 ? "detached" : gRoll < 92 ? "integral" : "none";
            uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u;

            if (anchor.HasValue)
            {
                var a = PlaceAnchor(anchor.Value, lotX, rowZ0, frontage, dep);
                map.Outbuildings.Add(a);
                RegisterOccupied(OccupiedBounds(a));
                map.Walks.Add(new Driveway
                {
                    Surface = "concrete",
                    WidthFeet = 5f,
                    Points = { new Vec2(a.Center.X, frontLineZ), new Vec2(a.Center.X, a.Center.Z) },
                });
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
            if (hzSpan > dep - 8f) { RecordContent(districtRow, col, lotX, lotX + frontage, rowZ0, rowZ0 + dep); map.LotLines.Add(new MapRect(lotX, rowZ0, frontage, dep)); return; }
            setback = MathF.Min(setback, dep - 4f - hzSpan);
            if (setback < 4f) { map.LotLines.Add(new MapRect(lotX, rowZ0, frontage, dep)); return; }
            float offsetZ = facesSouth ? frontLineZ + setback : frontLineZ - setback;
            house = house.OffsetCopy(hx, offsetZ);
            map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = mirrored });
            RegisterOccupied(FootprintBounds(house));

            var fd = FindFrontDoor(house);
            // The walk starts on the sidewalk — which is drawn 2.25 ft inside the
            // street's edge — and runs to the door. It used to start 4.5 ft past the
            // lot line, leaving a gap you could see at any zoom.
            float walkStartZ = facesSouth ? frontLineZ - 2.25f : frontLineZ + 2.25f;
            map.Walks.Add(new Driveway
            {
                Surface = "concrete",
                WidthFeet = 4f,
                Points = { new Vec2(fd.X, walkStartZ), fd },
            });

            // ~12% of houses get a small pole shed at the back of the lot, away from the garage side.
            if (rng.Chance(0.12f))
            {
                float shedW = rng.Range(8f, 11f), shedD = rng.Range(10f, 13f);
                bool garageRight = house.Walls.Any(w => w.Tag == EdgeTag.GarageSide) &&
                    (house.Walls.First(w => w.Tag == EdgeTag.GarageSide).Start.X + house.Walls.First(w => w.Tag == EdgeTag.GarageSide).End.X) / 2f >
                    (house.Footprint.Select(p => p.X).Min() + house.Footprint.Select(p => p.X).Max()) / 2f;
                float shedX = garageRight ? lotX + 4f : lotX + frontage - shedW - 4f;
                float shedZ = rowZ0 + dep - shedD - 5f;
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
                float dgZ = facesSouth ? rowZ0 + dep - 9f - dgD / 2f : rowZ0 + 9f + dgD / 2f;
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
                float alleyZ = facesSouth ? rowZ0 + dep - 8f : rowZ0 + 8f;
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
                        ? Math.Clamp(house.Footprint.Select(p => p.Z).Max() + 12f, frontLineZ + 26f, rowZ0 + dep - dgD - 4f)
                        : Math.Clamp(house.Footprint.Select(p => p.Z).Min() - 12f - dgD, rowZ0 + 22f, rowZ0 + dep - dgD - 4f);
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
                    // The drive starts on the sidewalk and crosses it to the kerb,
                    // the way a real Iowa driveway does.
                    float zStart = facesSouth ? frontLineZ - 2.25f : frontLineZ + 2.25f;
                    map.Driveways.Add(DrivewayTo(garageDoor, frontEntry, garageRight, zStart, "concrete", 17f));
                }
            }

        }

        // Fill one E/W-facing lot on the side of a 4-sided block (x-axis frontage/facing).
        void FillSideFace(int blockRow, int col, int side, float blockX0, float blockX1, float bz0, float bz1)
        {
            bool west = side == 0;
            // The outermost side strips now front the perimeter street, so they are filled
            // like every other side. Interior faces still only fill on 4-sided blocks.
            float lotX = west ? blockX0 : blockX1 - SideLotDepth;
            float z = bz0 + 3f;
            while (true)
            {
                float f = rng.Range(58f, 76f);
                if (z + f > bz1 - 3f) break;
                float cx = lotX + SideLotDepth / 2f, cz = z + f / 2f;
                if (!InsideBlob(cx, cz)) { z += f; continue; } // ragged edge: outer lots fall away
                if (IsVacantLot(cx, cz))
                {
                    RecordContent(blockRow, col, lotX, lotX + SideLotDepth, z, z + f);
                    map.LotLines.Add(new MapRect(lotX, z, SideLotDepth, f));
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
            RegisterOccupied(FootprintBounds(house));

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
            // The drive starts on the sidewalk and crosses it to the kerb.
            float xStart = west ? lotX - 2.25f : lotX + SideLotDepth + 2.25f;
            if (!float.IsNaN(garageDoor.X))
            {
                float fc = (house.Footprint.Select(p => p.Z).Min() + house.Footprint.Select(p => p.Z).Max()) / 2f;
                bool garagePositiveZ = garageDoor.Z >= fc;
                map.Driveways.Add(DrivewayToX(garageDoor, frontEntry, garagePositiveZ, xStart, "concrete", 17f));
            }

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
                bool four = IsFourSide(blockRow, col);
                if (four || col == 0) FillSideFace(blockRow, col, 0, bx0, bx1, bz0, bz1);
                if (four || col == size.Columns - 1) FillSideFace(blockRow, col, 1, bx0, bx1, bz0, bz1);
            }
        }

        // --- drop empty columns: a column with no houses, no civic buildings and no
        // outbuildings vanishes entirely (its lots, alleys and its east NS street),
        // so the town never has a lone house ringed by dead roads. Everything east
        // of the deleted column slides west by the column + street width.
        bool ColumnHasContent(int col)
        {
            float lo = nsX[col] + StreetRow;
            float hi = lo + colFrontages[col].Sum() + 2 * SideLotDepth;
            int houses = 0, buildings = 0, lots = 0;
            foreach (var h in map.Houses) if (h.Plan.MinX >= lo && h.Plan.MinX < hi) houses++;
            foreach (var o in map.Outbuildings) if (o.Center.X >= lo && o.Center.X < hi) buildings++;
            foreach (var r in map.LotLines) { float cx = r.X + r.W / 2f; if (cx >= lo && cx < hi) lots++; }
            // a mostly-empty block is worse than no block — delete it, and slide everything west
            bool keep = houses >= 4 && houses * 2 >= lots;
            return keep;
        }
        void ShiftAllX(float bound, float dx)
        {
            foreach (var h in map.Houses)
            {
                var p = h.Plan;
                if (p.MinX >= bound) h.Plan = p.OffsetCopy(dx, 0f);
            }
            for (int i = 0; i < map.LotLines.Count; i++) { var r = map.LotLines[i]; if (r.X >= bound) map.LotLines[i] = new MapRect(r.X + dx, r.Z, r.W, r.H); }
            for (int i = 0; i < map.Alleys.Count; i++) { var r = map.Alleys[i]; if (r.X >= bound) map.Alleys[i] = new MapRect(r.X + dx, r.Z, r.W, r.H); }
            for (int i = 0; i < map.Outbuildings.Count; i++) { var o = map.Outbuildings[i]; if (o.Center.X >= bound) o.Center = new Vec2(o.Center.X + dx, o.Center.Z); }
            for (int i = 0; i < map.Walks.Count; i++)
            {
                var w = map.Walks[i];
                if (!w.Points.Any(p => p.X >= bound)) continue;
                var d = new Driveway { WidthFeet = w.WidthFeet, Surface = w.Surface };
                foreach (var p in w.Points) d.Points.Add(new Vec2(p.X + (p.X >= bound ? dx : 0f), p.Z));
                map.Walks[i] = d;
            }
            for (int i = 0; i < map.Driveways.Count; i++)
            {
                var d = map.Driveways[i];
                if (!d.Points.Any(p => p.X >= bound)) continue;
                var n = new Driveway { WidthFeet = d.WidthFeet, Surface = d.Surface };
                foreach (var p in d.Points) n.Points.Add(new Vec2(p.X + (p.X >= bound ? dx : 0f), p.Z));
                map.Driveways[i] = n;
            }
            for (int i = 0; i < map.Trees.Count; i++) if (map.Trees[i].X >= bound) map.Trees[i] = new Vec2(map.Trees[i].X + dx, map.Trees[i].Z);
            for (int i = 0; i < map.Labels.Count; i++) { var l = map.Labels[i]; if (l.Position.X >= bound) map.Labels[i] = new MapLabel { Position = new Vec2(l.Position.X + dx, l.Position.Z), Text = l.Text, Large = l.Large, RotateDeg = l.RotateDeg, Anchor = l.Anchor }; }
            for (int i = 0; i < map.Bulbs.Count; i++) { var b = map.Bulbs[i]; if (b.Center.X >= bound) map.Bulbs[i] = new RoadBulb { Center = new Vec2(b.Center.X + dx, b.Center.Z), RadiusFeet = b.RadiusFeet, Fill = b.Fill }; }
        }
        for (int c = size.Columns - 1; c >= 0; c--)
        {
            bool has = ColumnHasContent(c);
            if (has) continue;
            float colW = colFrontages[c].Sum() + 2 * SideLotDepth + StreetRow;
            float lo = nsX[c] + StreetRow;
            float hi = lo + colFrontages[c].Sum() + 2 * SideLotDepth;
            map.Houses.RemoveAll(h => h.Plan.MinX >= lo && h.Plan.MinX < hi);
            map.LotLines.RemoveAll(r => r.X + r.W > lo && r.X < hi);
            map.Alleys.RemoveAll(r => r.X + r.W > lo && r.X < hi);
            map.Outbuildings.RemoveAll(o => o.Center.X >= lo && o.Center.X < hi);
            map.Walks.RemoveAll(w => w.Points.Any(p => p.X >= lo && p.X < hi));
            map.Driveways.RemoveAll(d => d.Points.Any(p => p.X >= lo && p.X < hi));
            map.Trees.RemoveAll(t => t.X >= lo && t.X < hi);
            map.Labels.RemoveAll(l => l.Position.X >= lo && l.Position.X < hi);
            map.Bulbs.RemoveAll(b => b.Center.X >= lo && b.Center.X < hi);
            float bound = nsX[c + 1] + StreetRow;
            ShiftAllX(bound, -colW);
            for (int i = c + 2; i < nsX.Count; i++) nsX[i] -= colW;
            nsX.RemoveAt(c + 1);
            streetZ.RemoveAt(c + 1);
            colFrontages.RemoveAt(c);
            totalWidth -= colW;
            map.WidthFeet = totalWidth;
        }
        int effColumns = nsX.Count - 1;   // effective column count after sweep

        // --- the street network ---------------------------------------------------
        // The highway band is the town's south side. The outermost N–S streets are the
        // west and east sides, and they run north to a perimeter street that jogs, so
        // the loop around town is never an even rectangle. Every E–W street reaches
        // both side streets; every N–S street meets the highway and an E–W street.
        // The only dead end is the stub below, its new lots still empty.
        bool[] ewKeep = new bool[size.EWStreets];
        for (int s = 0; s < size.EWStreets; s++)
            ewKeep[s] = streetX[s].x1 >= streetX[s].x0;
        int lastKeptEW = -1;
        for (int s = size.EWStreets - 1; s >= 0; s--)
            if (ewKeep[s]) { lastKeptEW = s; break; }
        if (lastKeptEW < 0) lastKeptEW = 0;

        // The built extent the lots actually used.
        float builtMinX = float.MaxValue, builtMaxX = float.MinValue, builtMaxZ = float.MinValue;
        foreach (var r in map.LotLines)
        {
            builtMinX = MathF.Min(builtMinX, r.X);
            builtMaxX = MathF.Max(builtMaxX, r.X + r.W);
            builtMaxZ = MathF.Max(builtMaxZ, r.Z + r.H);
        }
        foreach (var h in map.Houses)
        {
            builtMinX = MathF.Min(builtMinX, h.Plan.MinX);
            builtMaxX = MathF.Max(builtMaxX, h.Plan.MinX + h.Plan.WidthFeet);
            builtMaxZ = MathF.Max(builtMaxZ, h.Plan.MinZ + h.Plan.DepthFeet);
        }
        if (builtMinX == float.MaxValue) { builtMinX = 0; builtMaxX = totalWidth; builtMaxZ = totalDepth; }

        float gapN = rng.Range(30f, 70f);
        // The step is at least one street wide, so the two halves of the perimeter
        // never overlap each other.
        float jogH = rng.Range(24f, 130f);
        float northZ0 = builtMaxZ + gapN;
        float northZ1 = northZ0 + StreetRow + jogH;
        // The north edge steps up at one N–S street, so the loop is never a tidy
        // rectangle — it follows the ground the way a real town street does. The
        // stepping street carries the jog itself.
        int jogStreet = rng.RangeInt(1, effColumns);
        float jogX = nsX[jogStreet];
        float PerimZ(float x) => x < jogX ? northZ0 : northZ1;

        // An outlying road (or a bypassed old highway) sits beyond the town edge.
        // The two boundary streets carry on north to reach it, so it is a through
        // road and not a dangling end. It is kept clear of the lot row that sits
        // outside the perimeter.
        float outerRoadZ = northZ1 + StreetRow + rng.Range(200f, 300f);
        bool outerRoadIsShops = hasBypassedHighway && !hasOutlyingRoad;

        float nsZ1 = ewZ[lastKeptEW] + StreetRow;
        bool[] nsKeep = new bool[nsX.Count];
        for (int s = 0; s < nsX.Count; s++) nsKeep[s] = streetZ[s].z1 > zBand + 40f;
        // The two boundary streets always exist: they are the town's west and east sides.
        nsKeep[0] = true;
        nsKeep[nsX.Count - 1] = true;
        nsKeep[jogStreet] = true;
        {
            // Highway band: the town's south side.
            map.Roads.Add(new MapRect(0, 0, totalWidth, HighwayWidth));
            map.RoadDashed.Add(true);
            map.Labels.Add(new MapLabel { Position = new Vec2(totalWidth / 2f, HighwayWidth / 2f), Text = "MAIN STREET", Large = true });

            // E–W grid streets: they run the full width, meeting the side streets.
            for (int s = 0; s < size.EWStreets; s++)
            {
                if (!ewKeep[s]) continue;
                map.Roads.Add(new MapRect(0, ewZ[s], totalWidth, StreetRow));
                map.RoadDashed.Add(false);
            }
            // N–S grid streets: highway at the south, the perimeter street at the north.
            // Every one of them runs all the way through, so nothing dead-ends mid-block.
            for (int s = 0; s < nsX.Count; s++)
            {
                if (!nsKeep[s]) continue;
                float zEnd = PerimZ(nsX[s]) + StreetRow;
                // The two boundary streets carry on north to meet an outlying road.
                if (hasOuterRoad && (s == 0 || s == nsX.Count - 1))
                    zEnd = outerRoadZ + StreetRow;
                map.Roads.Add(new MapRect(nsX[s], zBand, StreetRow, zEnd - zBand));
                map.RoadDashed.Add(false);
            }
            // The perimeter street north of town, with a jog so it is never even. The
            // two halves overlap the stepping street, so the loop closes cleanly and
            // no street end is left hanging.
            map.Roads.Add(new MapRect(0, northZ0, jogX + StreetRow, StreetRow));
            map.RoadDashed.Add(false);
            map.Roads.Add(new MapRect(jogX, northZ1, totalWidth - jogX, StreetRow));
            map.RoadDashed.Add(false);

            // A boulevard: one street with a planted median down the middle. Rare,
            // but a town that has one has it for a reason.
            if (hasBoulevard && size.EWStreets > 0)
            {
                int bs = rng.RangeInt(0, size.EWStreets);
                if (!ewKeep[bs]) bs = 0;
                float mz = ewZ[bs] + StreetRow / 2f - 9f;
                map.Medians.Add(new MapRect(24f, mz, totalWidth - 48f, 18f));
                // A row of street trees down the median.
                for (float tx = 60f; tx < totalWidth - 60f; tx += 58f)
                    map.Trees.Add(new Vec2(tx, mz + 9f));
            }

            // The outlying road (or the bypassed old highway): a street beyond the
            // town, reached by the two boundary streets, so it is a through road.
            if (hasOuterRoad)
            {
                map.Roads.Add(new MapRect(0, outerRoadZ, totalWidth, StreetRow));
                map.RoadDashed.Add(false);
                map.Labels.Add(new MapLabel
                {
                    Position = new Vec2(42f, outerRoadZ + StreetRow / 2f),
                    Text = outerRoadIsShops ? "OLD HIGHWAY" : "RURAL ROAD",
                    Anchor = "start",
                });
            }

            // An industrial side-street: a short street running from the highway up
            // into the grid. Both ends land on a street.
            if (hasSideStreet && sideStreetX > 0f)
            {
                map.Roads.Add(new MapRect(sideStreetX, HighwayWidth, StreetRow, ewZ[0] + StreetRow - HighwayWidth));
                map.RoadDashed.Add(false);
            }
        }

        // --- alleys, only through blocks that actually ended up with content.
        for (int row = 0; row < blockRows; row++)
        {
            if (!rowAlley[row]) continue;
            float aZ = ewZ[row] + StreetRow + lotDepth;
            for (int col = 0; col < effColumns; col++)
            {
                if (!colFilled[row, col]) continue;
                float bX = nsX[col] + StreetRow;
                if (bX + colFrontages[col].Sum() > totalWidth + 1f) continue;
                map.Alleys.Add(new MapRect(bX, aZ, colFrontages[col].Sum() + 2 * SideLotDepth, AlleyRow));
            }
        }

        // --- sidewalks run with the streets (they connect end to end like the roads).
        for (int i = 0; i < size.EWStreets; i++)
        {
            if (!ewKeep[i]) continue;
            float z = ewZ[i];
            foreach (var wz in new[] { z + 2.25f, z + StreetRow - 2.25f })
            {
                map.Walks.Add(new Driveway { Surface = "concrete", WidthFeet = 4.5f, Points = { new Vec2(0, wz), new Vec2(totalWidth, wz) } });
            }
        }
        for (int s = 0; s < nsX.Count; s++)
        {
            if (!nsKeep[s]) continue;
            // The walk stops with the town's street. The run out to an outlying road
            // is country, and a sidewalk across a field looks like a mistake.
            float zEnd = PerimZ(nsX[s]) + StreetRow;
            foreach (var wx in new[] { nsX[s] + 2.25f, nsX[s] + StreetRow - 2.25f })
            {
                map.Walks.Add(new Driveway
                {
                    Surface = "concrete",
                    WidthFeet = 4.5f,
                    Points = { new Vec2(wx, zBand), new Vec2(wx, zEnd) },
                });
            }
        }

        // Street names.
        for (int i = 0; i < size.EWStreets; i++)
        {
            if (!ewKeep[i]) continue;
            map.Labels.Add(new MapLabel { Position = new Vec2(totalWidth / 2f, ewZ[i] + StreetRow / 2f), Text = ewNames[i % ewNames.Length] + " AVENUE" });
        }
        for (int s = 0; s < nsX.Count; s++)
        {
            if (!nsKeep[s]) continue;
            int ni = s < mainStreetIndex ? s : s - 1;
            string nm = s == mainStreetIndex ? "MAIN" : nsNames[ni % nsNames.Length];
            map.Labels.Add(new MapLabel
            {
                Position = new Vec2(nsX[s] + StreetRow / 2f, zBand + (PerimZ(nsX[s]) - zBand) * 0.62f),
                Text = nm + " STREET",
                RotateDeg = -90f,
            });
        }

        // --- dead-end streets. One per town (two for the biggest). A short street
        // that leaves the perimeter and ends in an asphalt bulb, with the lots platted
        // along it still empty — the one deliberate vacancy in town. Everything else
        // is built out.
        for (int d = 0; d < size.DeadEnds; d++)
        {
            // It hangs off the perimeter street, so it attaches to the road network.
            float stubX = 140f + (totalWidth - 300f) * ((d + 0.5f) / size.DeadEnds) + rng.Range(-40f, 40f);
            // Keep clear of the step in the perimeter, where the stub would land on a
            // corner instead of a street.
            if (stubX > jogX - 90f && stubX < jogX + StreetRow + 90f)
                stubX = stubX < jogX ? jogX - 90f : jogX + StreetRow + 90f;
            stubX = Math.Clamp(stubX, 60f, MathF.Max(60f, totalWidth - 90f));
            float stubLen = rng.Range(210f, 300f);
            float stubZ = PerimZ(stubX) + StreetRow;
            map.Roads.Add(new MapRect(stubX, stubZ, StreetRow, stubLen));
            map.RoadDashed.Add(false);
            map.Bulbs.Add(new RoadBulb
            {
                Center = new Vec2(stubX + StreetRow / 2f, stubZ + stubLen - 10f),
                RadiusFeet = 44f,
            });
            // Platted lots on both sides of the stub, waiting to be built.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    float lx = side < 0
                        ? stubX - (i + 1) * 62f - i * 6f
                        : stubX + StreetRow + 6f + i * 68f;
                    map.LotLines.Add(new MapRect(lx, stubZ + 18f + i * 100f, 62f, 96f));
                }
            }
        }

        // --- a cul-de-sac court -------------------------------------------------
        // A short street off the perimeter that ends in a bulb, with houses built
        // along it. The town's one deliberate vacancy is the dead-end above; this
        // court is the opposite — a built-out cul-de-sac.
        if (hasCourt)
        {
            float courtX = rng.Range(120f, MathF.Max(121f, totalWidth - 200f));
            if (courtX > jogX - 110f && courtX < jogX + StreetRow + 110f)
                courtX = courtX < jogX ? jogX - 110f : jogX + StreetRow + 110f;
            courtX = Math.Clamp(courtX, 100f, MathF.Max(100f, totalWidth - 180f));
            float courtLen = rng.Range(240f, 340f);
            float courtZ = PerimZ(courtX) + StreetRow;
            map.Roads.Add(new MapRect(courtX, courtZ, StreetRow, courtLen));
            map.RoadDashed.Add(false);
            map.Bulbs.Add(new RoadBulb
            {
                Center = new Vec2(courtX + StreetRow / 2f, courtZ + courtLen - 10f),
                RadiusFeet = 44f,
            });
            // Houses down the west side of the court, facing it.
            float hz = courtZ + 24f;
            int courtHouses = rng.RangeInt(2, 4);
            for (int i = 0; i < courtHouses && hz + 80f < courtZ + courtLen - 30f; i++)
            {
                uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u;
                var house = HouseGenerator.Generate(Weighted(rng), hSeed);
                house = house.Rotate90CCW();          // front faces east, at the court
                float hx = courtX - 12f - house.DepthFeet - rng.Range(8f, 16f);
                house = house.OffsetCopy(hx, hz);
                map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = false });
                RegisterOccupied(FootprintBounds(house));
                map.LotLines.Add(new MapRect(hx - 10f, hz - 8f, house.WidthFeet + 20f, house.DepthFeet + 16f));
                hz += 92f;
            }
        }

        // --- a street that runs out to the edge of the plat ----------------------
        // Iowa plats have them: a street that leaves the grid and stops in the
        // fields. Its far end is free by design — it leaves town, it does not dangle.
        if (hasPlatEdge)
        {
            float edgeX = rng.Range(150f, MathF.Max(151f, totalWidth - 250f));
            if (edgeX > jogX - 120f && edgeX < jogX + StreetRow + 120f)
                edgeX = edgeX < jogX ? jogX - 120f : jogX + StreetRow + 120f;
            edgeX = Math.Clamp(edgeX, 130f, MathF.Max(130f, totalWidth - 220f));
            float edgeZ = PerimZ(edgeX) + StreetRow;
            float edgeLen = rng.Range(260f, 420f);
            map.Roads.Add(new MapRect(edgeX, edgeZ, StreetRow, edgeLen));
            map.RoadDashed.Add(false);
            // It leaves town; the far end is not a dead end.
            map.RoadMayEndFree.Add(new MapRect(edgeX, edgeZ, StreetRow, edgeLen));
        }

        // --- the outlying road's buildings ---------------------------------------
        // A few farm houses strung along a road beyond the town, or the odd shop on
        // the old highway.
        if (hasOuterRoad)
        {
            float bx = rng.Range(90f, MathF.Max(91f, totalWidth - 200f));
            int n = rng.RangeInt(3, 6);
            float x = bx;
            for (int i = 0; i < n; i++)
            {
                if (outerRoadIsShops)
                {
                    // A few shops left on the old road. They are spaced well apart so
                    // their names do not pile up on each other.
                    float w = rng.Range(58f, 88f);
                    float depth = rng.Range(44f, 58f);
                    if (x + w > totalWidth - 40f) break;
                    float y = outerRoadZ - 58f - depth / 2f;
                    map.Outbuildings.Add(new Outbuilding
                    {
                        Kind = OutbuildingKind.Storefront,
                        Center = new Vec2(x + w / 2f, y),
                        WidthFeet = w,
                        DepthFeet = depth,
                        Label = BusinessCatalogue.LabelFor(rng, BusinessCatalogue.Shops[rng.RangeInt(0, BusinessCatalogue.Shops.Length)]),
                    });
                    x += w + rng.Range(300f, 430f);
                }
                else
                {
                    // Farm houses strung along the rural road, facing it.
                    uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u;
                    var house = HouseGenerator.Generate(Weighted(rng), hSeed);
                    house = house.FlipZ();   // front faces the road, to the south
                    if (x + house.WidthFeet > totalWidth - 40f) break;
                    float hz = outerRoadZ - 14f - house.DepthFeet;
                    house = house.OffsetCopy(x, hz);
                    map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = true });
                    RegisterOccupied(FootprintBounds(house));
                    map.LotLines.Add(new MapRect(x - 12f, hz - 10f, house.WidthFeet + 24f, house.DepthFeet + 20f));
                    x += house.WidthFeet + rng.Range(140f, 230f);
                }
            }
        }

        // --- the final "plat and build" pass --------------------------------------
        // The town's own ground: the plat ends here, and the streets beyond it are
        // country roads, not town.
        // The town's own ground: the plat ends here, and the streets beyond it are
        // country roads, not town. The budget has to clear the *highest* point of the
        // perimeter (it jogs), or the row of lots outside the jogged half gets
        // squeezed out.
        float plattedMaxZ = northZ1 + StreetRow + 130f;

        // Walks every street in the town and, for each side that is completely
        // empty, plats a row of lots and builds most of them. A few are left empty
        // on purpose: real towns have vacant lots.
        bool RectFree(float x0, float z0, float x1, float z1)
        {
            foreach (var r in map.LotLines)
                if (r.X < x1 && r.X + r.W > x0 && r.Z < z1 && r.Z + r.H > z0) return false;
            foreach (var r in map.Alleys)
                if (r.X < x1 && r.X + r.W > x0 && r.Z < z1 && r.Z + r.H > z0) return false;
            foreach (var r in map.Roads)
                if (r.X < x1 && r.X + r.W > x0 && r.Z < z1 && r.Z + r.H > z0) return false;
            foreach (var h in map.Houses)
            {
                float hx0 = h.Plan.Footprint.Min(p => p.X), hx1 = h.Plan.Footprint.Max(p => p.X);
                float hz0 = h.Plan.Footprint.Min(p => p.Z), hz1 = h.Plan.Footprint.Max(p => p.Z);
                if (hx0 < x1 && hx1 > x0 && hz0 < z1 && hz1 > z0) return false;
            }
            foreach (var o in map.Outbuildings)
            {
                float ow = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet : o.WidthFeet;
                float od = o.Kind == OutbuildingKind.GrainBin ? o.DiameterFeet : o.DepthFeet;
                if (o.Center.X - ow / 2f < x1 && o.Center.X + ow / 2f > x0 &&
                    o.Center.Z - od / 2f < z1 && o.Center.Z + od / 2f > z0) return false;
            }
            return true;
        }

        // Plat a row of lots along one side of a street and build most of them.
        // Each lot is checked on its own, so one occupied spot (a cul-de-sac stub,
        // say) does not stop the whole row.
        void PlatEWStrip(float x0, float x1, float streetEdgeZ, bool northSide)
        {
            // The street's own extent bounds the row; a street may sit on the town's
            // edge, so there is no width limit here.
            // The town's own ground ends here; a strip that would reach out into the
            // fields is not platted. The depth is measured from the street and the
            // row of lots is anchored to it; anchoring it to its far edge instead
            // would pull the plats away from the road whenever the depth was cut.
            float depth = northSide
                ? MathF.Min(lotDepth, plattedMaxZ - streetEdgeZ)
                : MathF.Min(lotDepth, streetEdgeZ - zBand);
            if (depth < 40f) return;
            float lotZ0 = northSide ? streetEdgeZ : streetEdgeZ - depth;
            float lotZ1 = lotZ0 + depth;
            float walkZ = northSide ? streetEdgeZ - 2.25f : streetEdgeZ + 2.25f;
            float cursor = x0;
            int guard = 0;
            while (cursor < x1 - 30f && guard++ < 30)
            {
                float frontage = MathF.Min(rng.Range(58f, 92f), x1 - cursor);
                if (frontage < 40f) break;
                float a = cursor + 2f, b = cursor + frontage - 2f;
                cursor += frontage;
                if (b - a < 36f) continue;
                if (!RectFree(a, lotZ0, b, lotZ1))
                {
                    continue;
                }
                map.LotLines.Add(new MapRect(a, lotZ0, b - a, depth));
                // A few plats stay empty — a town with no vacant lot reads as a
                // pattern, not a place.
                if (rng.Chance(0.10f)) { continue; }
                uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u;
                string arch = Weighted(rng);
                var house = HouseGenerator.Generate(arch, hSeed);
                // A house wider than its plat cannot be built there, so the narrow
                // archetype takes its place.
                if (house.WidthFeet > (b - a) - 4f && arch != "farmhouse")
                    house = HouseGenerator.Generate("farmhouse", hSeed ^ 0x5f3759dfu);
                float setback = rng.Range(9f, 16f);
                float hx = a + MathF.Max(2f, ((b - a) - house.WidthFeet) / 2f);
                bool mirrored = !northSide;                 // a south-side lot faces north
                if (mirrored) house = house.FlipZ();
                house = house.OffsetCopy(hx, mirrored ? streetEdgeZ - setback : streetEdgeZ + setback);
                // The house has to fit inside its own plat, or it lands on its
                // neighbour. If it does not, the plat stays empty.
                float fhx0 = house.Footprint.Min(p => p.X), fhx1 = house.Footprint.Max(p => p.X);
                float fhz0 = house.Footprint.Min(p => p.Z), fhz1 = house.Footprint.Max(p => p.Z);
                if (fhx0 < a - 2f || fhx1 > b + 2f || fhz0 < lotZ0 - 2f || fhz1 > lotZ1 + 2f)
                {
                    continue;
                }
                map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = mirrored });
                RegisterOccupied(FootprintBounds(house));
                var fd = FindFrontDoor(house);
                map.Walks.Add(new Driveway
                {
                    Surface = "concrete",
                    WidthFeet = 4f,
                    Points = { new Vec2(fd.X, walkZ), fd },
                });
                var (garageDoor, garageWall, frontEntry) = FindGarageDoor(house);
                if (!float.IsNaN(garageDoor.X))
                {
                    bool gr = !frontEntry && GarageSideIsRight(house, garageWall!);
                    map.Driveways.Add(DrivewayTo(garageDoor, frontEntry, gr, walkZ, "concrete", 17f));
                }
            }
        }

        // The same, along the side of a north–south street.
        void PlatNSStrip(float z0, float z1, float streetEdgeX, bool eastSide)
        {
            if (z0 < zBand - 1f) return;
            float depth = SideLotDepth;
            float lotX0 = eastSide ? streetEdgeX : streetEdgeX - depth;
            float lotX1 = eastSide ? streetEdgeX + depth : streetEdgeX;
            // The town may reach a little past its outermost street — a street with
            // nothing on its outside reads as a mistake — but not out into the fields.
            if (lotX0 < -depth - 60f || lotX1 > totalWidth + depth + 60f) return;
            float cap = MathF.Min(z1, plattedMaxZ);
            if (cap - z0 < 40f) return;
            float walkX = eastSide ? streetEdgeX - 2.25f : streetEdgeX + 2.25f;
            float cursor = z0;
            int guard = 0;
            while (cursor < cap - 30f && guard++ < 30)
            {
                float frontage = MathF.Min(rng.Range(52f, 82f), cap - cursor);
                if (frontage < 34f) break;
                float a = cursor + 3f, b = cursor + frontage - 3f;
                cursor += frontage;
                if (b - a < 30f) continue;
                if (!RectFree(lotX0, a, lotX1, b)) { continue; }
                map.LotLines.Add(new MapRect(lotX0, a, depth, b - a));
                if (rng.Chance(0.10f)) { continue; }
                uint hSeed = houseSeed = houseSeed * 1664525u + 1013904223u;
                var house = HouseGenerator.Generate(Weighted(rng), hSeed);
                house = eastSide ? house.Rotate90CCW() : house.Rotate90CW();
                float setback = rng.Range(9f, 16f);
                float hx = eastSide ? lotX1 - setback : lotX0 + setback;
                float hz = a + MathF.Max(2f, ((b - a) - house.DepthFeet) / 2f);
                house = house.OffsetCopy(hx, hz);
                float gx0 = house.Footprint.Min(p => p.X), gx1 = house.Footprint.Max(p => p.X);
                float gz0 = house.Footprint.Min(p => p.Z), gz1 = house.Footprint.Max(p => p.Z);
                if (gx0 < lotX0 - 2f || gx1 > lotX1 + 2f || gz0 < a - 2f || gz1 > b + 2f)
                {
                    continue;
                }
                map.Houses.Add(new PositionedHouse { Plan = house, MirroredZ = false });
                RegisterOccupied(FootprintBounds(house));
                var fd = FindFrontDoor(house);
                map.Walks.Add(new Driveway
                {
                    Surface = "concrete",
                    WidthFeet = 4f,
                    Points = { new Vec2(walkX, fd.Z), fd },
                });
                var (garageDoor, garageWall, frontEntry) = FindGarageDoor(house);
                if (!float.IsNaN(garageDoor.X))
                {
                    // The house faces across the street, so the drive runs across it.
                    float fc = (house.Footprint.Select(p => p.Z).Min() + house.Footprint.Select(p => p.Z).Max()) / 2f;
                    bool garagePositiveZ = garageDoor.Z >= fc;
                    map.Driveways.Add(DrivewayToX(garageDoor, frontEntry, garagePositiveZ, walkX, "concrete", 17f));
                }
            }
        }

        void PlatStreetFrontage()
        {
            // The town's own roads only — the highway band is the paved corridor and
            // is not the town's to plat.
            for (int i = 0; i < map.Roads.Count; i++)
            {
                var r = map.Roads[i];
                // The town's highway band is the paved corridor itself — not the
                // town's to plat.
                if (MathF.Abs(r.Z) < 0.5f && MathF.Abs(r.H - HighwayWidth) < 0.5f) continue;
                bool horizontal = r.W >= r.H;
                if (horizontal)
                {
                    PlatEWStrip(r.X, r.X + r.W, r.Z, false);              // south side
                    PlatEWStrip(r.X, r.X + r.W, r.Z + r.H, true);          // north side
                }
                else
                {
                    PlatNSStrip(r.Z, r.Z + r.H, r.X, false);                // west side
                    PlatNSStrip(r.Z, r.Z + r.H, r.X + r.W, true);           // east side
                }
            }
        }

        // --- the free-standing landmarks -----------------------------------------
        // A water tower, a cemetery, a ball field, a fairground: they stand in their
        // own ground rather than on a lot. They go out at the town's edge, where the
        // ground is free.
        foreach (var lm in freeLandmarks)
        {
            float lx = rng.Range(40f, MathF.Max(41f, totalWidth - lm.Width - 40f));
            float lo = zBand + BandDepth + 30f;
            float hi = northZ0 - lm.Depth - 40f;
            if (hi < lo) continue;
            float lz = rng.Range(lo, hi);
            var ob = new Outbuilding
            {
                Kind = lm.Kind,
                Center = new Vec2(lx + lm.Width / 2f, lz + lm.Depth / 2f),
                WidthFeet = lm.Width,
                DepthFeet = lm.Depth,
                Label = lm.Kind switch
                {
                    OutbuildingKind.WaterTower => "WATER TOWER",
                    OutbuildingKind.Cemetery => "CEMETERY",
                    OutbuildingKind.BallField => "BALL FIELD",
                    OutbuildingKind.GolfCourse => "GOLF COURSE",
                    OutbuildingKind.Fairgrounds => "FAIRGROUNDS",
                    OutbuildingKind.Airstrip => "AIRSTRIP",
                    _ => "",
                },
            };
            if (OccupiedCollides(OccupiedBounds(ob))) continue;
            map.Outbuildings.Add(ob);
            RegisterOccupied(OccupiedBounds(ob));
        }

        // --- final pass: plat every street that has nothing facing it ------------
        // The street network was laid after the lots, so the perimeter loop and the
        // extra streets started out with no frontage at all — they read as roads
        // going nowhere. A real town is built out to its streets. This pass walks
        // every street and plats a row of lots along any side that is completely
        // empty, then builds most of them, leaving a few plats empty on purpose.
        PlatStreetFrontage();

        // The plat pass may have reached past the town's outermost street, so the
        // built extent is measured again — the county roads have to clip around
        // whatever the town actually is now.
        builtMinX = float.MaxValue; builtMaxX = float.MinValue;
        foreach (var r in map.LotLines)
        {
            builtMinX = MathF.Min(builtMinX, r.X);
            builtMaxX = MathF.Max(builtMaxX, r.X + r.W);
        }
        foreach (var h in map.Houses)
        {
            builtMinX = MathF.Min(builtMinX, h.Plan.Footprint.Min(p => p.X));
            builtMaxX = MathF.Max(builtMaxX, h.Plan.Footprint.Max(p => p.X));
        }
        if (builtMinX == float.MaxValue) { builtMinX = 0; builtMaxX = totalWidth; }

        // Loop bounds: county roads stop here so they meet a town street. The loop runs
        // from the town's north-of-highway edge out to the perimeter street.
        map.LoopMinX = MathF.Min(0f, builtMinX - 12f);
        map.LoopMaxX = MathF.Max(totalWidth, builtMaxX + 12f);
        map.LoopMinZ = zBand;
        map.LoopMaxZ = hasOuterRoad ? outerRoadZ + StreetRow : northZ1 + StreetRow;
        map.BuiltMinX = builtMinX;
        map.BuiltMaxX = builtMaxX;
        map.BuiltMinZ = zBand;
        map.BuiltMaxZ = builtMaxZ;

        // The plan keeps its nominal width: the town stays centred on its corridor
        // even though the outer plat may reach a little past its last street.
        map.WidthFeet = totalWidth;
        map.DepthFeet = MathF.Max(map.DepthFeet, northZ1 + StreetRow + (size.DeadEnds > 0 ? 320f : 0f));

        map.Labels.Add(new MapLabel
        {
            Position = new Vec2(map.WidthFeet - 8f, 14f),
            Text = $"{map.WidthFeet:0}′ × {map.DepthFeet:0}′",
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
            OutbuildingKind.Library => new Outbuilding { Kind = kind, Center = new Vec2(cx, midZ), WidthFeet = MathF.Min(frontage - 12f, 62f), DepthFeet = 48f, Label = "LIBRARY" },
            OutbuildingKind.PostOffice => new Outbuilding { Kind = kind, Center = new Vec2(cx, midZ), WidthFeet = MathF.Min(frontage - 12f, 54f), DepthFeet = 42f, Label = "POST OFFICE" },
            OutbuildingKind.FireStation => new Outbuilding { Kind = kind, Center = new Vec2(cx, midZ), WidthFeet = MathF.Min(frontage - 12f, 64f), DepthFeet = 48f, Label = "FIRE STATION" },
            OutbuildingKind.Depot => new Outbuilding { Kind = kind, Center = new Vec2(cx, midZ), WidthFeet = MathF.Min(frontage - 12f, 64f), DepthFeet = 32f, Label = "DEPOT" },
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

}
}
