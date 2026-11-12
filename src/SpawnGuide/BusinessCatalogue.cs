using System.Collections.Generic;

namespace SpawnGuide
{

/// <summary>
/// The kinds of business that line a small-town main street, and the invented names
/// they can carry.
///
/// Research: `docs/research/2026-11-11-iowa-town-composition.md`. Iowa has no separate
/// downtown — the state highway *is* Main Street, and a single row of storefronts runs
/// along it. The number of shops follows the population, so the mix is gated by a town
/// tier: 0 crossroads, 1 hamlet, 2 village, 3 town.
///
/// Nothing here is a real brand and nothing is borrowed from the reference maps. A
/// business either reads as a plain descriptor ("GAS STATION") or as a descriptor with
/// an invented flavour name in front of it ("CEDAR VALLEY FEED & SEED").
/// </summary>
public static class BusinessCatalogue
{
    /// <summary>
    /// The landmarks a small Iowa town has, and how likely each is. Research:
    /// `docs/research/2026-11-11-iowa-town-composition.md`. The water tower is
    /// near-universal; the depot only exists where the railroad does; the
    /// fairgrounds and the golf course belong to the bigger places.
    /// </summary>
    public sealed class Landmark
    {
        public OutbuildingKind Kind;
        /// <summary>Smallest town tier that gets it (0 crossroads … 3 town).</summary>
        public int MinTier;
        /// <summary>Chance the town has one at all.</summary>
        public float Chance;
        /// <summary>Width and depth in feet.</summary>
        public float Width, Depth;
        /// <summary>True for the ones that stand in their own ground rather than on a lot.</summary>
        public bool FreeStanding;
    }

    public static readonly Landmark[] Landmarks =
    {
        new Landmark { Kind = OutbuildingKind.WaterTower, MinTier = 0, Chance = 0.70f, Width = 44f, Depth = 44f, FreeStanding = true },
        new Landmark { Kind = OutbuildingKind.PostOffice, MinTier = 0, Chance = 0.55f, Width = 58f, Depth = 46f },
        new Landmark { Kind = OutbuildingKind.FireStation, MinTier = 1, Chance = 0.60f, Width = 66f, Depth = 52f },
        new Landmark { Kind = OutbuildingKind.Library, MinTier = 1, Chance = 0.55f, Width = 60f, Depth = 50f },
        new Landmark { Kind = OutbuildingKind.BallField, MinTier = 1, Chance = 0.50f, Width = 210f, Depth = 210f, FreeStanding = true },
        new Landmark { Kind = OutbuildingKind.Cemetery, MinTier = 0, Chance = 0.60f, Width = 260f, Depth = 200f, FreeStanding = true },
        new Landmark { Kind = OutbuildingKind.Depot, MinTier = 0, Chance = 0.35f, Width = 70f, Depth = 34f },
        new Landmark { Kind = OutbuildingKind.GolfCourse, MinTier = 2, Chance = 0.30f, Width = 420f, Depth = 380f, FreeStanding = true },
        new Landmark { Kind = OutbuildingKind.Fairgrounds, MinTier = 2, Chance = 0.28f, Width = 380f, Depth = 300f, FreeStanding = true },
        new Landmark { Kind = OutbuildingKind.Airstrip, MinTier = 2, Chance = 0.22f, Width = 3600f, Depth = 200f, FreeStanding = true },
    };

    /// <summary>One kind of business, with the rules for where and how big it is.</summary>
    public sealed class Business
    {
        /// <summary>The type, as it reads on the map: "GAS STATION", "FEED &amp; SEED".</summary>
        public string Descriptor = "";
        /// <summary>Smallest town tier that gets this business (0 crossroads … 3 town).</summary>
        public int MinTier;
        /// <summary>Chance the business carries an invented flavour name.</summary>
        public float NameChance;
        /// <summary>True for businesses that want a site at the end of the strip with room
        /// in front of them — a gas station, an implement dealer, a motel.</summary>
        public bool Edge;
        /// <summary>True for the big industrial boxes rather than the shops.</summary>
        public bool Industrial;
        public float MinWidth = 68f, MaxWidth = 108f;
        public float MinDepth = 50f, MaxDepth = 72f;
    }

    // --- the shops ---------------------------------------------------------------
    // Tier gating follows the Iowa PBS "Businesses on Main Street" progression: a
    // crossroads has a gas station and maybe a post office; a hamlet adds feed and
    // hardware and a bar; a village adds the implement dealer, bank, insurance,
    // pharmacy, restaurant and auto repair; a town adds the lumber yard, motel and
    // farm supply.

    public static readonly Business[] Shops =
    {
        // Every town has a gas station — that is what makes a crossroads a crossroads.
        new Business { Descriptor = "GAS STATION", MinTier = 0, NameChance = 0.35f, Edge = true,
                       MinWidth = 86f, MaxWidth = 112f, MinDepth = 58f, MaxDepth = 74f },
        new Business { Descriptor = "POST OFFICE", MinTier = 0, NameChance = 0.15f,
                       MinWidth = 62f, MaxWidth = 78f, MinDepth = 46f, MaxDepth = 58f },
        new Business { Descriptor = "GROCERY", MinTier = 0, NameChance = 0.45f,
                       MinWidth = 66f, MaxWidth = 86f, MinDepth = 48f, MaxDepth = 62f },
        new Business { Descriptor = "FEED & SEED", MinTier = 1, NameChance = 0.85f,
                       MinWidth = 78f, MaxWidth = 104f, MinDepth = 52f, MaxDepth = 68f },
        new Business { Descriptor = "HARDWARE", MinTier = 1, NameChance = 0.85f,
                       MinWidth = 74f, MaxWidth = 98f, MinDepth = 50f, MaxDepth = 66f },
        new Business { Descriptor = "BAR & GRILL", MinTier = 1, NameChance = 0.7f,
                       MinWidth = 66f, MaxWidth = 88f, MinDepth = 48f, MaxDepth = 62f },
        new Business { Descriptor = "RESTAURANT", MinTier = 1, NameChance = 0.7f,
                       MinWidth = 68f, MaxWidth = 92f, MinDepth = 50f, MaxDepth = 66f },
        new Business { Descriptor = "AUTO REPAIR", MinTier = 2, NameChance = 0.6f,
                       MinWidth = 72f, MaxWidth = 96f, MinDepth = 52f, MaxDepth = 70f },
        new Business { Descriptor = "IMPLEMENT DEALER", MinTier = 2, NameChance = 0.75f, Edge = true,
                       MinWidth = 96f, MaxWidth = 128f, MinDepth = 60f, MaxDepth = 78f },
        new Business { Descriptor = "BANK", MinTier = 2, NameChance = 0.35f,
                       MinWidth = 68f, MaxWidth = 90f, MinDepth = 50f, MaxDepth = 64f },
        new Business { Descriptor = "INSURANCE", MinTier = 2, NameChance = 0.65f,
                       MinWidth = 58f, MaxWidth = 74f, MinDepth = 44f, MaxDepth = 56f },
        new Business { Descriptor = "PHARMACY", MinTier = 2, NameChance = 0.6f,
                       MinWidth = 64f, MaxWidth = 84f, MinDepth = 48f, MaxDepth = 62f },
        new Business { Descriptor = "LUMBER YARD", MinTier = 3, NameChance = 0.5f, Edge = true,
                       MinWidth = 90f, MaxWidth = 120f, MinDepth = 58f, MaxDepth = 76f },
        new Business { Descriptor = "FARM SUPPLY", MinTier = 3, NameChance = 0.75f,
                       MinWidth = 82f, MaxWidth = 108f, MinDepth = 54f, MaxDepth = 70f },
        new Business { Descriptor = "MOTEL", MinTier = 3, NameChance = 0.5f, Edge = true,
                       MinWidth = 88f, MaxWidth = 116f, MinDepth = 56f, MaxDepth = 74f },
        new Business { Descriptor = "LAUNDROMAT", MinTier = 3, NameChance = 0.55f,
                       MinWidth = 56f, MaxWidth = 72f, MinDepth = 42f, MaxDepth = 54f },
    };

    // --- the industrial boxes ----------------------------------------------------
    // Real Iowa industrial follows the railroad, not the highway; the highway fringe
    // gets implement dealerships, propane and a feed store. These sit on the strip's
    // edge because they need room for trucks and yard.

    public static readonly Business[] Industrial =
    {
        new Business { Descriptor = "MFG PLANT", MinTier = 1, NameChance = 0.8f, Industrial = true, Edge = true,
                       MinWidth = 88f, MaxWidth = 118f, MinDepth = 56f, MaxDepth = 74f },
        new Business { Descriptor = "CO-OP PLANT", MinTier = 1, NameChance = 0.6f, Industrial = true, Edge = true,
                       MinWidth = 84f, MaxWidth = 112f, MinDepth = 54f, MaxDepth = 70f },
        new Business { Descriptor = "MACHINE WORKS", MinTier = 2, NameChance = 0.8f, Industrial = true, Edge = true,
                       MinWidth = 86f, MaxWidth = 116f, MinDepth = 56f, MaxDepth = 72f },
        new Business { Descriptor = "STEEL WORKS", MinTier = 2, NameChance = 0.65f, Industrial = true, Edge = true,
                       MinWidth = 92f, MaxWidth = 124f, MinDepth = 58f, MaxDepth = 76f },
        new Business { Descriptor = "FEED MILL", MinTier = 2, NameChance = 0.7f, Industrial = true, Edge = true,
                       MinWidth = 88f, MaxWidth = 118f, MinDepth = 56f, MaxDepth = 74f },
    };

    /// <summary>
    /// Invented flavour names. They are plausible Iowa-flavoured words, but none of
    /// them is a real company, and none is borrowed from the reference towns. Kept
    /// separate from the town place-name pool so a business never repeats its own town.
    /// </summary>
    public static readonly string[] FlavourNames =
    {
        "CEDAR VALLEY", "WINDFIELD", "BLUE GRASS", "CORN BELT", "RIVER BEND",
        "SUNRISE", "WESTGATE", "NORTHFIELD", "ELMWOOD", "BROOKSIDE",
        "STONEBROOK", "HAVERHILL", "WOODBINE", "SILVER CREEK", "LONE PINE",
        "RED BARN", "COTTONWOOD", "WILLOW BROOK", "FAIRLANDS", "IRONWOOD",
        "KINGFISHER", "MEADOWLARK", "SANDHILL", "TIMBERLINE", "GOLDENROD",
        "BRIDGEPORT", "CLIFFORD", "EASTBROOK", "HOLLOWAY", "KETTLE CREEK",
        "LARKSPUR", "MERIDEN", "NORTHGATE", "ORCHARD", "PLUM CREEK",
        "QUAIL RUN", "RIDGEWAY", "STEELE", "THISTLE", "WESTBROOK",
    };

    /// <summary>Draw an invented flavour name for a business, or empty for a plain label.</summary>
    public static string FlavourFor(Rng rng, Business b)
    {
        if (b.NameChance <= 0f || !rng.Chance(b.NameChance)) return "";
        return FlavourNames[rng.RangeInt(0, FlavourNames.Length)];
    }

    /// <summary>The label as it appears on the map: "GAS STATION" or "CEDAR VALLEY FEED".</summary>
    public static string LabelFor(Rng rng, Business b)
    {
        string flavour = FlavourFor(rng, b);
        return flavour.Length == 0 ? b.Descriptor : flavour + " " + b.Descriptor;
    }
}

}
