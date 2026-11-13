using System.Linq;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// Placement zone for town extras.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ExtraPlacementZone
    {
        Perimeter,
        Interior,
        Highway,
        Rail,
        Commercial,
        Industrial,
        Any,
    }

    /// <summary>
    /// Definition of a town extra feature (feature grammar).
    /// Extras are selected by weight, subject to prerequisites and exclusions.
    /// </summary>
    public sealed class TownExtraDef
    {
        /// <summary>Unique identifier for this extra.</summary>
        public string Id { get; set; } = "";

        /// <summary>Human-readable name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Minimum tier this extra can appear in (0=crossroads, 3=town).</summary>
        [Category(CategoryAttribute.Concern.Placement)]
        public int MinTier { get; set; } = 0;

        /// <summary>Maximum tier this extra can appear in.</summary>
        [Category(CategoryAttribute.Concern.Placement)]
        public int MaxTier { get; set; } = 3;

        /// <summary>Base selection weight (relative).</summary>
        [Category(CategoryAttribute.Concern.Distribution)]
        [Rationale("Weight for weighted random selection among eligible extras.")]
        public float BaseWeight { get; set; } = 1f;

        /// <summary>IDs of extras that must also be present (prerequisites).</summary>
        [Category(CategoryAttribute.Concern.Placement)]
        public string[] Requires { get; set; } = Array.Empty<string>();

        /// <summary>IDs of extras that cannot coexist with this one.</summary>
        [Category(CategoryAttribute.Concern.Placement)]
        public string[] Excludes { get; set; } = Array.Empty<string>();

        /// <summary>Valid placement zones for this extra.</summary>
        [Category(CategoryAttribute.Concern.Placement)]
        public ExtraPlacementZone[] Placement { get; set; } = new[] { ExtraPlacementZone.Any };

        /// <summary>Size multiplier distribution for this extra's footprint.</summary>
        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Geometry)]
        public FloatDistribution SizeMultiplier { get; set; } = FloatDistribution.Uniform(0.8f, 1.2f);

        /// <summary>Maximum number of this extra per town (0 = unlimited).</summary>
        [Category(CategoryAttribute.Concern.Density)]
        public int MaxPerTown { get; set; } = 1;

        /// <summary>Whether this extra extends the town boundary (affects blob).</summary>
        [Category(CategoryAttribute.Concern.Geometry)]
        public bool ExtendsBoundary { get; set; } = false;

        /// <summary>Custom generation parameters (key-value pairs for specific extras).</summary>
        [JsonExtensionData]
        public Dictionary<string, object> CustomParams { get; set; } = new();

        public TownExtraDef() { }

        public TownExtraDef(string id, string name, int minTier, int maxTier, float weight, params ExtraPlacementZone[] placement)
        {
            Id = id; Name = name; MinTier = minTier; MaxTier = maxTier; BaseWeight = weight; Placement = placement;
        }

        public void Validate() { }
    }

    /// <summary>
    /// Feature grammar engine for selecting and placing town extras.
    /// </summary>
    public static class TownExtraGrammar
    {
        /// <summary>Select extras for a town based on tier, using weighted random with prerequisites/exclusions.</summary>
        public static List<TownExtraDef> SelectExtras(
            int tier,
            IReadOnlyList<TownExtraDef> pool,
            Rng rng,
            int maxExtras = 2,
            float noneChance = 0.35f,
            float oneVsTwoChance = 0.6f)
        {
            // Filter by tier
            var eligible = pool.Where(e => tier >= e.MinTier && tier <= e.MaxTier).ToList();
            if (eligible.Count == 0) return new List<TownExtraDef>();

            // Chance of no extras
            if (rng.Chance(noneChance)) return new List<TownExtraDef>();

            int count = rng.Chance(oneVsTwoChance) ? 1 : 2;
            count = Math.Min(count, maxExtras);

            var selected = new List<TownExtraDef>();
            var available = new List<TownExtraDef>(eligible);

            for (int i = 0; i < count && available.Count > 0; i++)
            {
                // Compute effective weights (apply prerequisite bonuses)
                var weights = new float[available.Count];
                float totalWeight = 0;
                for (int j = 0; j < available.Count; j++)
                {
                    var e = available[j];
                    float w = e.BaseWeight;

                    // Prerequisite check: if requires something not selected, weight = 0
                    bool reqMet = true;
                    foreach (var req in e.Requires)
                    {
                        if (!selected.Exists(s => s.Id == req)) { reqMet = false; break; }
                    }
                    if (!reqMet) w = 0;

                    // Exclusion check: if excludes something selected, weight = 0
                    bool exclConflict = false;
                    foreach (var exc in e.Excludes)
                    {
                        if (selected.Exists(s => s.Id == exc)) { exclConflict = true; break; }
                    }
                    if (exclConflict) w = 0;

                    weights[j] = w;
                    totalWeight += w;
                }

                if (totalWeight <= 0) break; // Nothing eligible

                // Weighted selection
                float roll = rng.NextFloat() * totalWeight;
                float accum = 0;
                int pick = -1;
                for (int j = 0; j < available.Count; j++)
                {
                    accum += weights[j];
                    if (roll <= accum) { pick = j; break; }
                }
                if (pick < 0) pick = available.Count - 1;

                selected.Add(available[pick]);
                available.RemoveAt(pick);
            }

            return selected;
        }

        /// <summary>Get the default pool of town extras (research-based Iowa town features).</summary>
        public static List<TownExtraDef> GetDefaultPool()
        {
            return new List<TownExtraDef>
            {
                new TownExtraDef
                {
                    Id = "CulDeSacCourt",
                    Name = "Cul-de-sac Court",
                    MinTier = 1, MaxTier = 3,
                    BaseWeight = 1.0f,
                    Placement = new[] { ExtraPlacementZone.Perimeter },
                    ExtendsBoundary = true,
                    CustomParams = new Dictionary<string, object>
                    {
                        ["CourtLengthFt"] = FloatDistribution.LogNormal(5.5f, 0.2f, 200f, 400f),
                        ["HouseCount"] = new IntDistribution(FloatDistribution.Uniform(2, 5)),
                        ["HouseFrontageFt"] = FloatDistribution.LogNormal(4.1f, 0.15f, 45f, 75f),
                    }
                },
                new TownExtraDef
                {
                    Id = "PlatEdgeStreet",
                    Name = "Plat Edge Street",
                    MinTier = 0, MaxTier = 3,
                    BaseWeight = 1.0f,
                    Placement = new[] { ExtraPlacementZone.Perimeter },
                    ExtendsBoundary = true,
                    CustomParams = new Dictionary<string, object>
                    {
                        ["LengthFt"] = FloatDistribution.LogNormal(5.7f, 0.2f, 250f, 500f),
                        ["LotFrontageFt"] = FloatDistribution.LogNormal(4.2f, 0.15f, 50f, 85f),
                    }
                },
                new TownExtraDef
                {
                    Id = "OutlyingRoad",
                    Name = "Outlying Road",
                    MinTier = 0, MaxTier = 2,
                    BaseWeight = 0.8f,
                    Placement = new[] { ExtraPlacementZone.Perimeter },
                    ExtendsBoundary = true,
                    CustomParams = new Dictionary<string, object>
                    {
                        ["ZOffsetFt"] = FloatDistribution.LogNormal(5.5f, 0.2f, 200f, 400f),
                        ["HouseCount"] = new IntDistribution(FloatDistribution.Uniform(2, 6)),
                        ["HouseFrontageFt"] = FloatDistribution.LogNormal(4.2f, 0.15f, 55f, 90f),
                        ["HouseDepthFt"] = FloatDistribution.LogNormal(3.9f, 0.15f, 40f, 60f),
                        ["SpacingFt"] = FloatDistribution.LogNormal(5.7f, 0.2f, 250f, 450f),
                    }
                },
                new TownExtraDef
                {
                    Id = "BypassedHighway",
                    Name = "Bypassed Highway",
                    MinTier = 1, MaxTier = 3,
                    BaseWeight = 0.6f,
                    Placement = new[] { ExtraPlacementZone.Highway, ExtraPlacementZone.Perimeter },
                    ExtendsBoundary = true,
                    CustomParams = new Dictionary<string, object>
                    {
                        ["XRangeFt"] = FloatDistribution.Uniform(90f, 800f),
                        ["ShopCount"] = new IntDistribution(FloatDistribution.Uniform(2, 7)),
                        ["ShopFrontageFt"] = FloatDistribution.LogNormal(4.2f, 0.15f, 55f, 90f),
                    }
                },
                new TownExtraDef
                {
                    Id = "IndustrialSideStreet",
                    Name = "Industrial Side Street",
                    MinTier = 1, MaxTier = 3,
                    BaseWeight = 0.7f,
                    Placement = new[] { ExtraPlacementZone.Highway, ExtraPlacementZone.Industrial },
                    Requires = new[] { "Rail" }, // Only if town has rail spur
                    CustomParams = new Dictionary<string, object>
                    {
                        ["XRangeFt"] = FloatDistribution.Uniform(60f, 800f),
                        ["LotDepthFt"] = FloatDistribution.LogNormal(3.2f, 0.2f, 20f, 60f),
                        ["LotWidthFt"] = FloatDistribution.LogNormal(4.5f, 0.3f, 50f, 150f),
                    }
                },
                new TownExtraDef
                {
                    Id = "TownSquare",
                    Name = "Town Square",
                    MinTier = 1, MaxTier = 3,
                    BaseWeight = 0.5f,
                    Placement = new[] { ExtraPlacementZone.Interior },
                    Excludes = new[] { "Boulevard" },
                    ExtendsBoundary = false,
                    CustomParams = new Dictionary<string, object>
                    {
                        ["SizeFt"] = FloatDistribution.LogNormal(4.8f, 0.2f, 100f, 200f),
                    }
                },
                new TownExtraDef
                {
                    Id = "Boulevard",
                    Name = "Boulevard",
                    MinTier = 2, MaxTier = 3,
                    BaseWeight = 0.3f,
                    Placement = new[] { ExtraPlacementZone.Interior },
                    Excludes = new[] { "TownSquare" },
                    CustomParams = new Dictionary<string, object>
                    {
                        ["MedianWidthFt"] = FloatDistribution.Normal(16f, 4f, 10f, 30f),
                    }
                },
                new TownExtraDef
                {
                    Id = "RailCorridorStreet",
                    Name = "Rail Corridor Street",
                    MinTier = 1, MaxTier = 3,
                    BaseWeight = 0.4f,
                    Placement = new[] { ExtraPlacementZone.Rail },
                    Requires = new[] { "Rail" },
                    ExtendsBoundary = false,
                    CustomParams = new Dictionary<string, object>
                    {
                        ["EraseHalfFt"] = FloatDistribution.Normal(20f, 5f, 10f, 40f),
                    }
                },
                new TownExtraDef
                {
                    Id = "CurvedStreet",
                    Name = "Curved Street",
                    MinTier = 1, MaxTier = 3,
                    BaseWeight = 0.4f,
                    Placement = new[] { ExtraPlacementZone.Interior, ExtraPlacementZone.Perimeter },
                    ExtendsBoundary = true,
                    CustomParams = new Dictionary<string, object>
                    {
                        ["EraseHalfFt"] = FloatDistribution.Normal(20f, 5f, 10f, 40f),
                    }
                },
            };
        }
    }
}