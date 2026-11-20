using System.Text.Json.Serialization;
namespace SpawnGuide
{
    public sealed class HouseConfig
    {
        // --- Ranch ---
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(3.7, 0.1) â‰ˆ 40ft - typical ranch width.")]
        [JsonPropertyName("ranchWidthFt")] public FloatDistribution RanchWidth { get; set; } = FloatDistribution.LogNormal(3.7f, 0.1f, 30f, 55f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(3.2, 0.1) â‰ˆ 25ft - shallow ranch depth.")]
        [JsonPropertyName("ranchDepthFt")] public FloatDistribution RanchDepth { get; set; } = FloatDistribution.LogNormal(3.2f, 0.1f, 20f, 35f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(22, 3) - standard 2-car garage width.")]
        [JsonPropertyName("ranchGarageWidthFt")] public FloatDistribution RanchGarageWidth { get; set; } = FloatDistribution.Normal(22f, 3f, 16f, 30f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(22, 2) - standard garage depth.")]
        [JsonPropertyName("ranchGarageDepthFt")] public FloatDistribution RanchGarageDepth { get; set; } = FloatDistribution.Normal(22f, 2f, 18f, 26f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("50/50 - no side preference.")]
        [JsonPropertyName("ranchGarageLeftChance")] public float RanchGarageLeftChance { get; set; } = 0.5f;

        // --- Farmhouse ---
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(3.4, 0.1) â‰ˆ 30ft - narrow traditional form.")]
        [JsonPropertyName("farmhouseWidthFt")] public FloatDistribution FarmhouseWidth { get; set; } = FloatDistribution.LogNormal(3.4f, 0.1f, 24f, 40f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(3.5, 0.1) â‰ˆ 33ft - deeper than wide.")]
        [JsonPropertyName("farmhouseDepthFt")] public FloatDistribution FarmhouseDepth { get; set; } = FloatDistribution.LogNormal(3.5f, 0.1f, 26f, 44f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Beta(5,3, 0.5, 0.8) - porch spans 50-80% of front.")]
        [JsonPropertyName("farmhousePorchWidthFraction")] public FloatDistribution FarmhousePorchWidthFraction { get; set; } = FloatDistribution.Beta(5f, 3f, 0.5f, 0.8f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(7, 1) - usable porch depth.")]
        [JsonPropertyName("farmhousePorchDepthFt")] public FloatDistribution FarmhousePorchDepth { get; set; } = FloatDistribution.Normal(7f, 1f, 5f, 10f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("90% - traditional centered door.")]
        [JsonPropertyName("farmhouseDoorCenteredChance")] public float FarmhouseDoorCenteredChance { get; set; } = 0.9f;

        // --- Split-Level ---
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(3.4, 0.1) â‰ˆ 30ft - compact footprint.")]
        [JsonPropertyName("splitLevelWidthFt")] public FloatDistribution SplitLevelWidth { get; set; } = FloatDistribution.LogNormal(3.4f, 0.1f, 24f, 40f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("LogNormal(3.4, 0.1) â‰ˆ 30ft - roughly square.")]
        [JsonPropertyName("splitLevelDepthFt")] public FloatDistribution SplitLevelDepth { get; set; } = FloatDistribution.LogNormal(3.4f, 0.1f, 24f, 40f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(16, 2) - single-car integral garage typical.")]
        [JsonPropertyName("splitLevelGarageWidthFt")] public FloatDistribution SplitLevelGarageWidth { get; set; } = FloatDistribution.Normal(16f, 2f, 12f, 22f);

        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(20, 2) - garage under living space.")]
        [JsonPropertyName("splitLevelGarageDepthFt")] public FloatDistribution SplitLevelGarageDepth { get; set; } = FloatDistribution.Normal(20f, 2f, 16f, 26f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [JsonPropertyName("splitLevelGarageLeftChance")] public float SplitLevelGarageLeftChance { get; set; } = 0.5f;

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("90% - split-levels usually have side-entry garage.")]
        [JsonPropertyName("splitLevelGarageDoorOnSideChance")] public float SplitLevelGarageDoorOnSideChance { get; set; } = 0.9f;

        // --- Shared ---
        [Category(CategoryAttribute.Concern.Visual), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Market order: White/Ivory/Beige vinyl dominant, brick front legacy, wood/aluminum rare.")]
        [JsonPropertyName("sidingOptions")] public SidingType[] SidingOptions { get; set; } = new[]
        {
            SidingType.WhiteVinyl, SidingType.IvoryVinyl, SidingType.BeigeVinyl,
            SidingType.LightGrayVinyl, SidingType.BrickFrontVinylSides,
            SidingType.WoodLap, SidingType.Aluminum,
        };

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Beta(2,5, 0.15, 0.35) - door biased toward garage side.")]
        [JsonPropertyName("doorOffsetFractionRange")] public FloatDistribution DoorOffsetFraction { get; set; } = FloatDistribution.Beta(2f, 5f, 0.15f, 0.35f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Visual)]
        [Rationale("Beta(2,5, 0, 0.08) - windows nearly aligned with slight variance.")]
        [JsonPropertyName("frontWindowJitterFraction")] public FloatDistribution FrontWindowJitterFraction { get; set; } = FloatDistribution.Beta(2f, 5f, 0f, 0.08f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Visual)]
        [Rationale("Beta(2,5, 0, 0.06) - side windows tighter.")]
        [JsonPropertyName("sideWindowJitterFraction")] public FloatDistribution SideWindowJitterFraction { get; set; } = FloatDistribution.Beta(2f, 5f, 0f, 0.06f);

        [Category(CategoryAttribute.Concern.Distribution), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Bimodal(9ft:65%, 16ft:35%) - single vs double door.")]
        [JsonPropertyName("garageDoorWidthFt")] public FloatDistribution GarageDoorWidthFt { get; set; } = FloatDistribution.Bimodal(9f, 16f, 0.65f);

        /// <summary>Minimum wall length (ft) to place 2 windows instead of 1.</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(22, 2) - need ~22ft for two 3ft windows + trim.")]
        [JsonPropertyName("twoWindowMinWallFt")] public FloatDistribution TwoWindowMinWall { get; set; } = FloatDistribution.Normal(22f, 2f, 18f, 28f);

        /// <summary>Minimum wall length (ft) to place 1 window.</summary>
        [Category(CategoryAttribute.Concern.Geometry), Category(CategoryAttribute.Concern.Residential)]
        [Rationale("Normal(9, 1) - need ~9ft for one 3ft window + trim.")]
        [JsonPropertyName("oneWindowMinWallFt")] public FloatDistribution OneWindowMinWall { get; set; } = FloatDistribution.Normal(9f, 1f, 7f, 12f);

        // Legacy garage mode chances (used by town generators)
        [JsonPropertyName("garageModeIntegralChance")] public float GarageModeIntegralChance { get; set; } = 0.55f;
        [JsonPropertyName("garageModeDetachedChance")] public float GarageModeDetachedChance { get; set; } = 0.35f;
        [JsonPropertyName("garageModeNoneChance")] public float GarageModeNoneChance { get; set; } = 0.10f;

        public void Validate()
        {
            // Validate distributions
            foreach (var prop in GetType().GetProperties())
            {
                if (prop.PropertyType == typeof(FloatDistribution))
                {
                    var fd = (FloatDistribution)prop.GetValue(this);
                    // Basic validation happens in ConfigValidator
                }
            }
        }
    }
}
