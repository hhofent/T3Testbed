namespace SpawnGuide
{
    /// <summary>Python reference values; generation is connected in tier 2.</summary>
    public sealed class BuilderGuyConfig
    {
        [Range(1985, 2000), Units("year"), Category(CategoryAttribute.Concern.Historical)]
        [Rationale("N9: default snapshot is 1990; reference parity retains per-seed years."), Source("user")]
        public int SnapshotYear { get; set; } = 1990;

        public BuilderGuyCommercialConfig Commercial { get; set; } = new();
        public BuilderGuyHouseConfig Houses { get; set; } = new();
        public BuilderGuySetbackConfig Setbacks { get; set; } = new();
        public BuilderGuyRegionConfig Region { get; set; } = new();
    }

    public sealed class BuilderGuyCommercialConfig
    {
        [Range(0, 1), Units("fraction"), Category(CategoryAttribute.Concern.Commercial)]
        [Rationale("N12 PLACEHOLDER: downtown share; remaining businesses use the highway strip."), Source("user")]
        public float DowntownShare { get; set; } = 0.7f;
    }

    public sealed class BuilderGuyHouseConfig
    {
        [Range(1, 256), Units("ticks/inch"), Category(CategoryAttribute.Concern.Geometry)]
        [Rationale("spawner.py T: integer geometry precision; conversion constants derive from this."), Source("user")]
        public int TicksPerInch { get; set; } = 16;

        [Range(0, 24), Units("inches"), Category(CategoryAttribute.Concern.Geometry)]
        [Rationale("spawner.py CLR: rule 11 clearance on each side of a door rough opening."), Source("user")]
        public double DoorCornerClearanceInches { get; set; } = 4.5;

        [Range(60, 120), Units("inches"), Category(CategoryAttribute.Concern.Geometry)]
        [Rationale("spawner.py HEAD: rule 28 common opening head, AH73 82-83 inches."), Source("user")]
        public double OpeningHeadInches { get; set; } = 83;

        [Range(0.25f, 8), Units("feet"), Category(CategoryAttribute.Concern.Geometry)]
        [Rationale("spawner.py GRID: rule 20 room split grid."), Source("user")]
        public double GridFeet { get; set; } = 2;

        [Range(0.25f, 16), Units("feet"), Category(CategoryAttribute.Concern.Geometry)]
        [Rationale("spawner.py MODULE: rule 20 footprint snap module."), Source("user")]
        public double ModuleFeet { get; set; } = 4;

        [Range(0, 40), Units("feet"), Category(CategoryAttribute.Concern.Geometry)]
        [Rationale("spawner.py GW double: attached double garage width."), Source("user")]
        public double DoubleGarageWidthFeet { get; set; } = 22;

        [Range(0, 40), Units("feet"), Category(CategoryAttribute.Concern.Geometry)]
        [Rationale("spawner.py GW single: attached single garage width."), Source("user")]
        public double SingleGarageWidthFeet { get; set; } = 12;
    }

    public sealed class BuilderGuySetbackConfig
    {
        [Range(0, 100), Units("percent of lot width"), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("spawner.py SIDE_PCT: PLACEHOLDER each-side setback."), Source("placeholder")]
        public double SidePercent { get; set; } = 10;
        [Range(0, 100), Units("percent of lot width"), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("spawner.py SIDE_MIN_PCT: PLACEHOLDER hard minimum."), Source("placeholder")]
        public double SideMinimumPercent { get; set; } = 5;
        [Range(0, 100), Units("percent of lot depth"), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("spawner.py FRONT_PCT: PLACEHOLDER front setback."), Source("placeholder")]
        public double FrontPercent { get; set; } = 20;
        [Range(0, 100), Units("percent of lot depth"), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("spawner.py FRONT_MIN_PCT: PLACEHOLDER hard minimum."), Source("placeholder")]
        public double FrontMinimumPercent { get; set; } = 15;
        [Range(0, 100), Units("percent of lot depth"), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("spawner.py REAR_PCT: PLACEHOLDER rear setback."), Source("placeholder")]
        public double RearPercent { get; set; } = 25;
        [Range(0, 100), Units("percent of lot depth"), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("spawner.py REAR_MIN_PCT: PLACEHOLDER hard minimum."), Source("placeholder")]
        public double RearMinimumPercent { get; set; } = 20;
        [Range(1, 4), Units("multiplier"), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("spawner.py CORNER_SIDE_X: PLACEHOLDER corner street-side setback multiplier."), Source("placeholder")]
        public double CornerSideMultiplier { get; set; } = 2;
        [Range(0, 200), Units("feet"), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("spawner.py PORCH_PROJECT_MAX_W: PLACEHOLDER maximum lot width for porch projection."), Source("placeholder")]
        public double PorchProjectionMaximumLotWidthFeet { get; set; } = 70;
        [Range(0, 20), Units("feet"), Category(CategoryAttribute.Concern.Placement)]
        [Rationale("spawner.py PORCH_PROJECT: PLACEHOLDER allowed projection into front setback."), Source("placeholder")]
        public double PorchProjectionFeet { get; set; } = 6;
    }
}
