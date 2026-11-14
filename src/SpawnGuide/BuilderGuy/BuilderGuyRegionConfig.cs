namespace SpawnGuide { public sealed class BuilderGuyRegionConfig {
    [Range(1, 100), Units("miles"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py N_MI: reference region size"), Source("placeholder")]
    public int RegionMiles { get; set; } = 3;
    [Range(0, 200), Units("feet"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py ROW: road right of way"), Source("CRG")]
    public float RoadRightOfWayFeet { get; set; } = 66f;
    [Range(0, 100), Units("percent"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py TOWN_PCT: blacktop crossing town chance"), Source("placeholder")]
    public float TownChancePercent { get; set; } = 35f;
    [Range(0, 100), Units("percent"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py ACREAGE_PCT: non-farm acreage per road mile"), Source("placeholder")]
    public float AcreageChancePercent { get; set; } = 25f;
    [Range(0, 1000), Units("feet"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py ACRE_BACK: house distance from right of way"), Source("CRG")]
    public float AcreageSetbackFeet { get; set; } = 60f;
    [Range(0, 1000), Units("feet"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py GROVE_W: north and west shelterbelt width"), Source("placeholder")]
    public float ShelterbeltWidthFeet { get; set; } = 80f;
    [Range(1, 500), Units("feet"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py BASE_LOT: base parcel width; houses consume multiple base lots"), Source("CRG")]
    public float BaseLotWidthFeet { get; set; } = 50f;
    [Range(0, 100), Units("percent"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py TRAILER_RUN_PCT: in-block clusters only per N13"), Source("placeholder")]
    public float TrailerClusterChancePercent { get; set; } = 20f;
    [Range(0, 100), Units("percent"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py RAIL_PCT: region rail probability"), Source("placeholder")]
    public float RailChancePercent { get; set; } = 60f;
    [Range(0, 1000), Units("feet"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py RAIL_GAP: parallel rail and road right-of-way gap"), Source("placeholder")]
    public float RailRoadGapFeet { get; set; } = 40f;
    [Range(0, 100), Units("percent"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py RAILTOWN_PCT: rail crossing town chance"), Source("placeholder")]
    public float RailTownChancePercent { get; set; } = 50f;
    [Range(0, 100), Units("percent"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py NORAIL_TOWN_PCT: no-rail town crossing chance"), Source("placeholder")]
    public float NoRailTownChancePercent { get; set; } = 15f;
    [Range(0, 100), Units("percent"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py RAGGED_PCT: outer-ring blocks left as farmland"), Source("placeholder")]
    public float RaggedEdgePercent { get; set; } = 35f;
    [Range(0, 100), Units("percent/block"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py RIBBON_DECAY: keep-chance lost away from both main roads"), Source("placeholder")]
    public float RibbonDecayPercent { get; set; } = 20f;
    [Range(0, 100), Units("percent"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py EDGE_HALF_PCT: omit outer half-blocks"), Source("placeholder")]
    public float EdgeHalfOmissionPercent { get; set; } = 40f;
    [Range(0, 100), Units("percent"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py FRINGE_PCT: connected dead-end stubs per N8"), Source("placeholder")]
    public float DeadEndStubPercent { get; set; } = 30f;
    [Range(0, 100), Units("block faces"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py MAIN_FACES: approximate downtown extent"), Source("CRG")]
    public int MainStreetFaces { get; set; } = 6;
    [Range(0, 1000), Units("feet"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py RAIL_ROW: rail corridor width"), Source("placeholder")]
    public float RailCorridorWidthFeet { get; set; } = 100f;
    [Range(0, 20), Units("people/house"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py PEOPLE_PER_HOUSE: population estimate"), Source("placeholder")]
    public float PeoplePerHouse { get; set; } = 2.4f;
    [Range(1900, 2100), Units("year"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py BASE_YEAR: business rate baseline"), Source("CRG")]
    public int BusinessBaseYear { get; set; } = 1998;
    [Range(0, 5), Units("multiplier"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py FADE_1985: retail rate relative to 1998"), Source("placeholder")]
    public float RetailFade1985 { get; set; } = 1.3f;
    [Range(0, 100), Units("percent"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py VACANT_PCT: vacant storefront share at baseline"), Source("placeholder")]
    public float VacantStorefrontPercent { get; set; } = 15f;
    [Range(0, 100), Units("percent"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py PAVED_PCT: paved grid-line share"), Source("CRG")]
    public FloatRange PavedSharePercent { get; set; } = new(22, 25);
    [Range(0, 2000), Units("feet"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py CLEAR: rural lot intersection exclusion"), Source("CRG")]
    public FloatRange IntersectionClearanceFeet { get; set; } = new(150, 300);
    [Range(0, 100), Units("acres"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py FARM_AC: farmstead parcel area"), Source("CRG")]
    public FloatRange FarmParcelAcres { get; set; } = new(3, 9);
    [Range(0, 2000), Units("feet"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py FARM_BACK: farmhouse distance from right of way"), Source("CRG")]
    public FloatRange FarmHouseSetbackFeet { get; set; } = new(100, 300);
    [Range(0, 100), Units("acres"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py ACRE_AC: non-farm acreage parcel area"), Source("CRG")]
    public FloatRange AcreageParcelAcres { get; set; } = new(1, 5);
    [Range(1, 20), Units("trailers"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py TRAILER_RUN: adjacent single lots in in-block cluster"), Source("placeholder")]
    public IntRange TrailerClusterCount { get; set; } = new(2, 4);
    [Range(0, 180), Units("degrees from east"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py RAIL_ANGLE: reference rail orientation; N4 crossing checks are separate"), Source("placeholder")]
    public FloatRange RailAngleDegrees { get; set; } = new(62, 80);
    [Range(0, 100), Units("acres"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py ELEV_AC: elevator and co-op parcel"), Source("CRG")]
    public FloatRange ElevatorParcelAcres { get; set; } = new(3, 6);
    [Range(0, 100), Units("acres"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py CHURCH_AC: country church parcel"), Source("CRG")]
    public FloatRange ChurchParcelAcres { get; set; } = new(1, 3);
    [Range(0, 1000), Units("feet"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py CREEK_W: channel width"), Source("placeholder")]
    public FloatRange CreekWidthFeet { get; set; } = new(20, 60);
    [Range(0, 5000), Units("feet each side"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py CREEK_BUF: unbuildable floodplain buffer"), Source("placeholder")]
    public FloatRange CreekFloodplainBufferFeet { get; set; } = new(150, 300);
    [Range(0, 100), Units("percent"), Category(CategoryAttribute.Concern.Placement)]
    [Rationale("region.py BRANCH_PCT: 1985 and 2000 endpoint rates; not a random range"), Source("placeholder")]
    public FloatRange BankBranchPercent { get; set; } = new(10, 50);
} }
