using System;
using CsharpSpawner;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace csharp_spawnerv0._1
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _loadTimer;
        private CancellationTokenSource _active;
        private bool _isLoading;
        private int _seed = 33;
        private int _loadedElevationChunks;
        private WorldGenerationSettings _settings;
        private static readonly JsonSerializerOptions SettingsJson = new() { WriteIndented = true };

        public MainWindow()
        {
            InitializeComponent();
            SpawnerProcessHost.Diagnostic = message => Dispatcher.BeginInvoke(new Action(() =>
                Log($"{message} Render count {MapView.RenderCount}, last {MapView.LastRenderMilliseconds:N1} ms, max {MapView.MaxRenderMilliseconds:N1} ms.")), DispatcherPriority.Background);
            _settings = LoadSettings();
            _seed = _settings.Seed;
            SpawnerProcessHost.Settings = _settings;
            PopulateSettingsControls();
            _loadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
            _loadTimer.Tick += async (_, __) => { _loadTimer.Stop(); await LoadVisibleChunksAsync(); };
            MapView.VisibleChunksChanged += (_, __) => ScheduleVisibleChunks();
            Loaded += (_, __) => ScheduleVisibleChunks();
        }

        private void RandomSeed_Click(object sender, RoutedEventArgs e)
        {
            int seed = RandomNumberGenerator.GetInt32(int.MaxValue);
            SeedInput.Text = seed.ToString(CultureInfo.InvariantCulture);
            Regenerate_Click(sender, e);
        }

        private static string SettingsPath => Path.Combine(AppContext.BaseDirectory, "settings.json");

        private WorldGenerationSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var loaded = JsonSerializer.Deserialize<WorldGenerationSettings>(File.ReadAllText(SettingsPath)) ?? new WorldGenerationSettings();
                    // Seed 31 was the previous application default. Migrate it once so
                    // stale per-build settings files do not override the new default.
                    if (loaded.Seed == 31)
                    {
                        loaded.Seed = 33;
                        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(loaded, SettingsJson));
                    }
                    // Migrate the former defaults only; preserve customized density settings.
                    if (loaded.FarmCountZeroBelowPercent == 15 && loaded.FarmCountOneBelowPercent == 65 && loaded.AcreageChancePercent == 25)
                    {
                        loaded.FarmCountZeroBelowPercent = 60;
                        loaded.FarmCountOneBelowPercent = 95;
                        loaded.AcreageChancePercent = 12;
                        if (loaded.MajorTownChanceDenominator == 4) loaded.MajorTownChanceDenominator = 3;
                    }
                    return loaded;
                }
            }
            catch (Exception ex) { Log("Could not load settings.json; using defaults: " + ex.Message); }
            return new WorldGenerationSettings();
        }

        private void PopulateSettingsControls()
        {
            SeedInput.Text = _settings.Seed.ToString(CultureInfo.InvariantCulture);
            SettlementSpacingInput.Text = _settings.SettlementSpacingMiles.ToString(CultureInfo.InvariantCulture);
            SettlementSeparationInput.Text = _settings.MinimumSettlementSeparationMiles.ToString(CultureInfo.InvariantCulture);
            WaterSearchInput.Text = _settings.WaterSearchMiles.ToString(CultureInfo.InvariantCulture);
            MajorTownChanceInput.Text = _settings.MajorTownChanceDenominator.ToString(CultureInfo.InvariantCulture);
            PeopleBaseInput.Text = _settings.PeopleAtNewSiteBase.ToString(CultureInfo.InvariantCulture);
            PeopleRollInput.Text = _settings.PeopleAtNewSiteRollMax.ToString(CultureInfo.InvariantCulture);
            FarmZeroInput.Text = _settings.FarmCountZeroBelowPercent.ToString(CultureInfo.InvariantCulture);
            FarmOneInput.Text = _settings.FarmCountOneBelowPercent.ToString(CultureInfo.InvariantCulture);
            AcreageChanceInput.Text = _settings.AcreageChancePercent.ToString(CultureInfo.InvariantCulture);
            AcreageMinInput.Text = _settings.AcreageAreaMinAcres.ToString(CultureInfo.InvariantCulture);
            AcreageMaxInput.Text = _settings.AcreageAreaMaxAcres.ToString(CultureInfo.InvariantCulture);
            FarmMinInput.Text = _settings.FarmsteadAreaMinAcres.ToString(CultureInfo.InvariantCulture);
            FarmMaxInput.Text = _settings.FarmsteadAreaMaxAcres.ToString(CultureInfo.InvariantCulture);
            HomeOccupancyInput.Text = _settings.HomeOccupancyPercent.ToString(CultureInfo.InvariantCulture);
            GarageChanceInput.Text = _settings.GarageChancePercent.ToString(CultureInfo.InvariantCulture);
            SeaLevelInput.Text = _settings.SeaLevelFeet.ToString(CultureInfo.InvariantCulture);
            SouthDropInput.Text = _settings.SouthDropFeetPerMile.ToString(CultureInfo.InvariantCulture);
            GambrelChanceInput.Text = _settings.GambrelBarnChancePercent.ToString(CultureInfo.InvariantCulture);
            ShelterbeltMinRowsInput.Text = _settings.ShelterbeltMinRows.ToString(CultureInfo.InvariantCulture);
            ShelterbeltMaxRowsInput.Text = _settings.ShelterbeltMaxRows.ToString(CultureInfo.InvariantCulture);
            FieldEntrancesMinInput.Text = _settings.FieldEntrancesMinPerQuarter.ToString(CultureInfo.InvariantCulture);
            FieldEntrancesMaxInput.Text = _settings.FieldEntrancesMaxPerQuarter.ToString(CultureInfo.InvariantCulture);
            FieldEntranceWidthInput.Text = _settings.FieldEntranceWidthFeet.ToString(CultureInfo.InvariantCulture);
            FieldEntranceLengthMinInput.Text = _settings.FieldEntranceLengthMinFeet.ToString(CultureInfo.InvariantCulture);
            FieldEntranceLengthMaxInput.Text = _settings.FieldEntranceLengthMaxFeet.ToString(CultureInfo.InvariantCulture);
            FieldEntranceSetbackInput.Text = _settings.FieldEntranceSetbackFeet.ToString(CultureInfo.InvariantCulture);
            CattleYardChanceInput.Text = _settings.CattleYardChancePercent.ToString(CultureInfo.InvariantCulture);
            CattleYardMinInput.Text = _settings.CattleYardMinAcres.ToString(CultureInfo.InvariantCulture);
            CattleYardMaxInput.Text = _settings.CattleYardMaxAcres.ToString(CultureInfo.InvariantCulture);
            CattleYardSectionsInput.Text = _settings.CattleYardSectionsPerSite.ToString(CultureInfo.InvariantCulture);
            CoopRailDistanceInput.Text = _settings.CoopCornerRailDistanceFeet.ToString(CultureInfo.InvariantCulture);
            CoopTownExclusionInput.Text = _settings.CoopCornerTownExclusionMiles.ToString(CultureInfo.InvariantCulture);
            TownNorthInput.Text = _settings.TownNorthPlacementPercent.ToString(CultureInfo.InvariantCulture);
            TownSouthInput.Text = _settings.TownSouthPlacementPercent.ToString(CultureInfo.InvariantCulture);
            TownAlleyChanceInput.Text = _settings.TownAlleyChancePercent.ToString(CultureInfo.InvariantCulture);
            TownDowntownShareInput.Text = _settings.TownDowntownBusinessPercent.ToString(CultureInfo.InvariantCulture);
            TownNoExtraStreetInput.Text = _settings.TownNoExtraStreetPercent.ToString(CultureInfo.InvariantCulture);
            TownMaxExtraStreetsInput.Text = _settings.TownMaxExtraStreets.ToString(CultureInfo.InvariantCulture);
            TownSidewalkWidthInput.Text = _settings.TownSidewalkWidthFeet.ToString(CultureInfo.InvariantCulture);
            TownSidewalkInsetInput.Text = _settings.TownSidewalkInsetFeet.ToString(CultureInfo.InvariantCulture);
            TownStripSearchInput.Text = _settings.TownPavedStripSearchMiles.ToString(CultureInfo.InvariantCulture);
            TownBulbWidthInput.Text = _settings.TownDeadEndBulbWidthFeet.ToString(CultureInfo.InvariantCulture);
            TownWaterTowerInput.Text = _settings.TownWaterTowerChancePercent.ToString(CultureInfo.InvariantCulture);
            TownBallFieldInput.Text = _settings.TownBallFieldChancePercent.ToString(CultureInfo.InvariantCulture);
            TownCemeteryInput.Text = _settings.TownCemeteryChancePercent.ToString(CultureInfo.InvariantCulture);
            TownLibraryInput.Text = _settings.TownLibraryChancePercent.ToString(CultureInfo.InvariantCulture);
            TownEdgeParkInput.Text = _settings.TownEdgeParkChancePercent.ToString(CultureInfo.InvariantCulture);
            TownCentralSquareInput.Text = _settings.TownCentralSquareChancePercent.ToString(CultureInfo.InvariantCulture);
            TownGolfInput.Text = _settings.TownGolfCourseChancePercent.ToString(CultureInfo.InvariantCulture);
            TownFairgroundsInput.Text = _settings.TownFairgroundsChancePercent.ToString(CultureInfo.InvariantCulture);
            TownAirstripInput.Text = _settings.TownAirstripChancePercent.ToString(CultureInfo.InvariantCulture);
            TownMotelSmallInput.Text = _settings.TownMotelSmallChancePercent.ToString(CultureInfo.InvariantCulture);
            TownMotelMediumInput.Text = _settings.TownMotelMediumChancePercent.ToString(CultureInfo.InvariantCulture);
            TownMotelLargeInput.Text = _settings.TownMotelLargeChancePercent.ToString(CultureInfo.InvariantCulture);
            TreeDensityInput.Text = _settings.TreeDensityPercent.ToString(CultureInfo.InvariantCulture);
            StreetTreeChanceInput.Text = _settings.TownStreetTreeChancePercent.ToString(CultureInfo.InvariantCulture);
            GroveSectionsInput.Text = _settings.TreeGroveSectionsPerGrove.ToString(CultureInfo.InvariantCulture);
        }

        private void Regenerate_Click(object sender, RoutedEventArgs e)
        {
            bool Int(TextBox box, out int value) => int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            bool Double(TextBox box, out double value) => double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
            bool Percent(TextBox box, out int value) => Int(box, out value) && value >= 0 && value <= 100;
            if (!Int(SeedInput, out int seed) || !Double(SettlementSpacingInput, out double spacing) || spacing <= 0 ||
                !Double(SettlementSeparationInput, out double separation) || separation <= 0 ||
                !Double(WaterSearchInput, out double waterSearch) || waterSearch < 0 ||
                !Int(MajorTownChanceInput, out int major) || major < 1 ||
                !Int(PeopleBaseInput, out int peopleBase) || peopleBase < 0 || !Int(PeopleRollInput, out int peopleRoll) || peopleRoll < 0 ||
                !Int(FarmZeroInput, out int farmZero) || farmZero < 0 || farmZero > 100 ||
                !Int(FarmOneInput, out int farmOne) || farmOne < farmZero || farmOne > 100 ||
                !Int(AcreageChanceInput, out int acreageChance) || acreageChance < 0 || acreageChance > 100 ||
                !Double(AcreageMinInput, out double acreageMin) || !Double(AcreageMaxInput, out double acreageMax) || acreageMin <= 0 || acreageMax < acreageMin ||
                !Double(FarmMinInput, out double farmMin) || !Double(FarmMaxInput, out double farmMax) || farmMin <= 0 || farmMax < farmMin ||
                !Int(HomeOccupancyInput, out int occupancy) || occupancy < 0 || occupancy > 100 ||
                !Int(GarageChanceInput, out int garage) || garage < 0 || garage > 100 ||
                !Double(SeaLevelInput, out double sea) || !Double(SouthDropInput, out double southDrop) ||
                !Int(GambrelChanceInput,out int gambrel) || gambrel<0 || gambrel>100 ||
                !Int(ShelterbeltMinRowsInput,out int shelterMin) || shelterMin<1 ||
                !Int(ShelterbeltMaxRowsInput,out int shelterMax) || shelterMax<shelterMin || shelterMax>20 ||
                !Int(FieldEntrancesMinInput,out int entranceMin) || entranceMin<0 ||
                !Int(FieldEntrancesMaxInput,out int entranceMax) || entranceMax<entranceMin || entranceMax>10 ||
                !Double(FieldEntranceWidthInput,out double entranceWidth) || entranceWidth<=0 ||
                !Double(FieldEntranceLengthMinInput,out double entranceLengthMin) || entranceLengthMin<=0 ||
                !Double(FieldEntranceLengthMaxInput,out double entranceLengthMax) || entranceLengthMax<entranceLengthMin ||
                !Double(FieldEntranceSetbackInput,out double entranceSetback) || entranceSetback<0 ||
                !Int(CattleYardChanceInput,out int cattleChance) || cattleChance<0 || cattleChance>100 ||
                !Double(CattleYardMinInput,out double cattleMin) || cattleMin<=0 ||
                !Double(CattleYardMaxInput,out double cattleMax) || cattleMax<cattleMin ||
                !Int(CattleYardSectionsInput,out int cattleSections) || cattleSections<1 ||
                !Double(CoopRailDistanceInput,out double coopRailDistance) || coopRailDistance<=0 ||
                !Double(CoopTownExclusionInput,out double coopTownExclusion) || coopTownExclusion<0 ||
                !Percent(TownNorthInput,out int townNorth) || !Percent(TownSouthInput,out int townSouth) || townNorth+townSouth>100 ||
                !Percent(TownAlleyChanceInput,out int townAlley) || !Percent(TownDowntownShareInput,out int townDowntown) ||
                !Percent(TownNoExtraStreetInput,out int townNoExtra) || !Int(TownMaxExtraStreetsInput,out int townMaxExtra) || townMaxExtra<0 || townMaxExtra>2 ||
                !Double(TownSidewalkWidthInput,out double sidewalkWidth) || sidewalkWidth<=0 ||
                !Double(TownSidewalkInsetInput,out double sidewalkInset) || sidewalkInset<0 ||
                !Double(TownStripSearchInput,out double stripSearch) || stripSearch<0 ||
                !Double(TownBulbWidthInput,out double bulbWidth) || bulbWidth<=0 ||
                !Percent(TownWaterTowerInput,out int waterTower) || !Percent(TownBallFieldInput,out int ballField) ||
                !Percent(TownCemeteryInput,out int cemetery) || !Percent(TownLibraryInput,out int library) ||
                !Percent(TownEdgeParkInput,out int edgePark) || !Percent(TownCentralSquareInput,out int centralSquare) ||
                !Percent(TownGolfInput,out int golf) || !Percent(TownFairgroundsInput,out int fairgrounds) || !Percent(TownAirstripInput,out int airstrip) ||
                !Percent(TownMotelSmallInput,out int motelSmall) || !Percent(TownMotelMediumInput,out int motelMedium) || !Percent(TownMotelLargeInput,out int motelLarge) ||
                !Percent(TreeDensityInput,out int treeDensity) || !Percent(StreetTreeChanceInput,out int streetTreeChance) ||
                !Int(GroveSectionsInput,out int groveSections) || groveSections<1 || groveSections>100)
            { StatusText.Text = "Settings error"; Log("Check settings values and ranges, then regenerate."); return; }
            _settings = new WorldGenerationSettings { Seed=seed, SettlementSpacingMiles=spacing, MinimumSettlementSeparationMiles=separation,
                WaterSearchMiles=waterSearch, MajorTownChanceDenominator=major, PeopleAtNewSiteBase=peopleBase, PeopleAtNewSiteRollMax=peopleRoll,
                FarmCountZeroBelowPercent=farmZero, FarmCountOneBelowPercent=farmOne, AcreageChancePercent=acreageChance,
                AcreageAreaMinAcres=acreageMin, AcreageAreaMaxAcres=acreageMax, FarmsteadAreaMinAcres=farmMin, FarmsteadAreaMaxAcres=farmMax,
                HomeOccupancyPercent=occupancy, GarageChancePercent=garage, SeaLevelFeet=sea, SouthDropFeetPerMile=southDrop,
                GambrelBarnChancePercent=gambrel,ShelterbeltMinRows=shelterMin,ShelterbeltMaxRows=shelterMax,
                FieldEntrancesMinPerQuarter=entranceMin,FieldEntrancesMaxPerQuarter=entranceMax,FieldEntranceWidthFeet=entranceWidth,
                FieldEntranceLengthMinFeet=entranceLengthMin,FieldEntranceLengthMaxFeet=entranceLengthMax,FieldEntranceSetbackFeet=entranceSetback,
                CattleYardChancePercent=cattleChance,CattleYardMinAcres=cattleMin,CattleYardMaxAcres=cattleMax,CattleYardSectionsPerSite=cattleSections,
                CoopCornerRailDistanceFeet=coopRailDistance,CoopCornerTownExclusionMiles=coopTownExclusion,
                TownNorthPlacementPercent=townNorth,TownSouthPlacementPercent=townSouth,TownAlleyChancePercent=townAlley,
                TownDowntownBusinessPercent=townDowntown,TownNoExtraStreetPercent=townNoExtra,TownMaxExtraStreets=townMaxExtra,
                TownSidewalkWidthFeet=sidewalkWidth,TownSidewalkInsetFeet=sidewalkInset,TownPavedStripSearchMiles=stripSearch,
                TownDeadEndBulbWidthFeet=bulbWidth,TownWaterTowerChancePercent=waterTower,TownBallFieldChancePercent=ballField,
                TownCemeteryChancePercent=cemetery,TownLibraryChancePercent=library,TownEdgeParkChancePercent=edgePark,
                TownCentralSquareChancePercent=centralSquare,TownGolfCourseChancePercent=golf,
                TownFairgroundsChancePercent=fairgrounds,TownAirstripChancePercent=airstrip,
                TownMotelSmallChancePercent=motelSmall,TownMotelMediumChancePercent=motelMedium,TownMotelLargeChancePercent=motelLarge,
                TreeDensityPercent=treeDensity,TownStreetTreeChancePercent=streetTreeChance,TreeGroveSectionsPerGrove=groveSections };
            SpawnerProcessHost.Settings = _settings;
            _seed = seed; SeedInput.Text = seed.ToString(CultureInfo.InvariantCulture);
            try { File.WriteAllText(SettingsPath, JsonSerializer.Serialize(_settings, SettingsJson)); }
            catch (Exception ex) { Log("Could not save settings.json: " + ex.Message); }
            CancelActiveWorker(); MapView.ClearChunks();
            StatusText.Text = $"Seed {_seed} - regenerating with settings";
            Log($"Applied and saved settings for seed {_seed}; rebuilding visible chunks.");
            ScheduleVisibleChunks();
        }

        private void SpawnerToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox toggle) return;
            string state = toggle.IsChecked == true ? "enabled" : "paused";
            Log($"{toggle.Content} spawner {state}.");
            CancelActiveWorker();
            ScheduleVisibleChunks();
        }

        private void ScheduleVisibleChunks()
        {
            _loadTimer.Stop();
            _loadTimer.Start();
        }

        private async Task LoadVisibleChunksAsync()
        {
            // Let an in-flight tile batch finish when the view changes. Restarting a
            // worker on every zoom event discarded expensive work and could starve water.
            if (_isLoading) return;
            IReadOnlyList<ChunkCoordinate> missing = MapView.GetMissingChunks();
            IReadOnlyList<ViewerScene> needingWater = MapView.GetChunksNeedingWater();
            IReadOnlyList<PopulationInput> needingPopulation = MapView.GetChunksNeedingPopulation(out var savedWaterFeatures, out var existingRoads);
            IReadOnlyList<TransportInput> needingTransport = MapView.GetChunksNeedingTransport(out _, out _, out _);
            IReadOnlyList<TownInput> needingRoads = MapView.GetChunksNeedingRoads(out var roadWaterFeatures,out var roadMainRoadFeatures);
            bool ruralEnabled = SpawnerIsEnabled(RuralSpawnerToggle);
            IReadOnlyList<TownInput> needingRural = MapView.GetChunksNeedingRural(out _, out _,out _,out _);
            IReadOnlyList<TownInput> needingTowns = MapView.GetChunksNeedingTowns(ruralEnabled, out _, out _, out _, out _, out _);
            if (!HasEnabledWork(missing, needingWater, needingPopulation, needingTransport, needingRoads, needingRural, needingTowns))
            {
                CancelActiveWorker();
                StatusText.Text = HasPendingWork(missing, needingWater, needingPopulation, needingTransport, needingRoads, needingRural, needingTowns, ruralEnabled)
                    ? PausedStatus(missing, needingWater, needingPopulation, needingTransport, needingRoads, needingRural, needingTowns, ruralEnabled)
                    : ReadyStatus();
                return;
            }

            CancelActiveWorker();
            _active = new CancellationTokenSource();
            _isLoading = true;
            CancellationToken token = _active.Token;
            int requestSeed = _seed;
            _loadedElevationChunks = 0;
            try
            {
                if (SpawnerIsEnabled(ElevationSpawnerToggle) && missing.Count > 0)
                {
                    StatusText.Text = $"Seed {requestSeed} - elevation loading {missing.Count} chunks...";
                    Log($"Elevation worker: requesting {missing.Count} missing 1 x 1 mile chunks.");
                    await SpawnerProcessHost.LoadElevationAsync(requestSeed, missing, chunks =>
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            MapView.SetChunks(chunks);
                            _loadedElevationChunks += chunks.Count;
                            if (_loadedElevationChunks % 128 < chunks.Count)
                                StatusText.Text = $"Seed {requestSeed} - elevation {_loadedElevationChunks}/{missing.Count} chunks";
                        }), DispatcherPriority.Background);
                    }, token);
                }

                needingWater = MapView.GetChunksNeedingWater();
                if (SpawnerIsEnabled(WaterSpawnerToggle) && needingWater.Count > 0)
                {
                    StatusText.Text = $"Seed {requestSeed} - water filling {needingWater.Count} chunks...";
                    Log($"Water spawner: filling saved riverbeds and below-sea terrain in {needingWater.Count} chunks.");
                    await SpawnerProcessHost.LoadWaterAsync(requestSeed, needingWater, waters =>
                    {
                        Dispatcher.Invoke(new Action(() => MapView.SetWaterChunks(waters)), DispatcherPriority.Background);
                    }, token);
                }
                needingPopulation = MapView.GetChunksNeedingPopulation(out savedWaterFeatures, out existingRoads);
                if (SpawnerIsEnabled(PopulationSpawnerToggle) && needingPopulation.Count > 0)
                {
                    StatusText.Text = $"Seed {requestSeed} - settling population in {needingPopulation.Count} chunks...";
                    Log($"Population spawner: westbound settlers choosing road and crossing sites in {needingPopulation.Count} chunks.");
                    int totalPeople = 0, totalSettlements = 0;
                    await SpawnerProcessHost.LoadPopulationAsync(requestSeed, needingPopulation, savedWaterFeatures, existingRoads, populations =>
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            MapView.SetPopulationChunks(populations);
                            totalPeople += populations.Sum(chunk => chunk.Settlers.Count);
                            totalSettlements += populations.Sum(chunk => chunk.Features.Count(feature => feature.Kind == "settlement"));
                        }), DispatcherPriority.Background);
                    }, token);
                    Log($"Population spawner saved {totalPeople} named settlers at {totalSettlements} settlement sites and generated their main roads.");
                }

                needingTransport = MapView.GetChunksNeedingTransport(out savedWaterFeatures, out var railTowns, out var savedMainRoads);
                if (SpawnerIsEnabled(RailSpawnerToggle) && needingTransport.Count > 0)
                {
                    StatusText.Text = $"Seed {requestSeed} - connecting major and county-road towns by rail in {needingTransport.Count} chunks...";
                    Log($"Rail spawner: evaluating {railTowns.Count} settlement sites; county crossroads can receive seeded rail spurs.");
                    await SpawnerProcessHost.LoadTransportAsync(requestSeed, needingTransport, savedWaterFeatures, railTowns, savedMainRoads, transports =>
                    {
                        Dispatcher.Invoke(new Action(() => MapView.SetTransportChunks(transports)), DispatcherPriority.Background);
                    }, token);
                }
                needingRoads = MapView.GetChunksNeedingRoads(out roadWaterFeatures,out roadMainRoadFeatures);
                if (SpawnerIsEnabled(RoadSpawnerToggle) && needingRoads.Count > 0)
                {
                    StatusText.Text = $"Seed {requestSeed} - laying local roads in {needingRoads.Count} chunks...";
                    Log($"Road spawner: laying the later one-mile road grid in {needingRoads.Count} chunks.");
                    await SpawnerProcessHost.LoadRoadsAsync(requestSeed, needingRoads, roadWaterFeatures,roadMainRoadFeatures, roads =>
                    {
                        Dispatcher.Invoke(new Action(() => MapView.SetRoadChunks(roads)), DispatcherPriority.Background);
                    }, token);
                }
                needingRural = MapView.GetChunksNeedingRural(out var ruralRoadFeatures, out var ruralWaterFeatures,
                    out var ruralRails,out var ruralSettlements);
                if (SpawnerIsEnabled(RuralSpawnerToggle) && needingRural.Count > 0)
                {
                    StatusText.Text = $"Seed {requestSeed} - placing rural lots in {needingRural.Count} chunks...";
                    Log($"Rural spawner: placing reproducible farmsteads and acreages along {ruralRoadFeatures.Count} saved roads.");
                    await SpawnerProcessHost.LoadRuralAsync(requestSeed, needingRural, ruralRoadFeatures, ruralWaterFeatures,ruralRails,ruralSettlements, rural =>
                    {
                        Dispatcher.Invoke(new Action(() => MapView.SetRuralChunks(rural)), DispatcherPriority.Background);
                    }, token);
                }
                needingTowns = MapView.GetChunksNeedingTowns(ruralEnabled, out var settlements, out var savedRails,
                    out var savedTownRoads, out var townWater, out var ruralFeatures);
                if (SpawnerIsEnabled(TownSpawnerToggle) && needingTowns.Count > 0)
                {
                    StatusText.Text = $"Seed {requestSeed} - laying out towns in {needingTowns.Count} chunks...";
                    Log($"Town spawner: growing {settlements.Count} settlements around saved rail and road corridors, after rural lots.");
                    await SpawnerProcessHost.LoadTownsAsync(requestSeed, needingTowns, settlements, MapView.GetAllSettlers(), savedRails,
                        savedTownRoads, townWater, ruralFeatures, towns =>
                    {
                        Dispatcher.Invoke(new Action(() => MapView.SetTownChunks(towns)), DispatcherPriority.Background);
                    }, token);
                }
                var needingTrees = MapView.GetChunksNeedingTrees(out var treeRural, out var treeTowns,
                    out var treeRoads, out var treeRails, out var treeWater);
                if (SpawnerIsEnabled(TreeSpawnerToggle) && needingTrees.Count > 0)
                {
                    StatusText.Text = $"Seed {requestSeed} - placing trunk markers in {needingTrees.Count} chunks...";
                    Log($"Tree spawner: placing sparse trunk markers after town footprints in {needingTrees.Count} chunks.");
                    await SpawnerProcessHost.LoadTreesAsync(requestSeed, needingTrees, treeRural, treeTowns,
                        treeRoads, treeRails, treeWater, trees =>
                    {
                        Dispatcher.Invoke(new Action(() => MapView.SetTreeChunks(trees)), DispatcherPriority.Background);
                    }, token);
                }
                var needingWalls = MapView.GetChunksNeedingWallPlans(out var wallTownFeatures);
                if (SpawnerIsEnabled(WallPlanToggle) && needingWalls.Count > 0)
                {
                    StatusText.Text = $"Seed {requestSeed} - loading near-scale house plans in {needingWalls.Count} chunks...";
                    Log($"Near wall-plan worker: resolving occupied homes into SpawnGuide wall plans in {needingWalls.Count} chunks.");
                    await SpawnerProcessHost.LoadWallPlansAsync(requestSeed, needingWalls, wallTownFeatures, walls =>
                    {
                        Dispatcher.Invoke(new Action(() => MapView.SetWallChunks(walls)), DispatcherPriority.Background);
                    }, token);
                }
                if (requestSeed == _seed)
                {
                    var remainingElevation = MapView.GetMissingChunks();
                    var remainingWater = MapView.GetChunksNeedingWater();
                    var remainingPopulation = MapView.GetChunksNeedingPopulation(out _, out _);
                    var remainingRail = MapView.GetChunksNeedingTransport(out _, out _, out _);
                    var remainingRoads = MapView.GetChunksNeedingRoads(out _,out _);
                    var remainingRural = MapView.GetChunksNeedingRural(out _, out _,out _,out _);
                    var remainingTowns = MapView.GetChunksNeedingTowns(ruralEnabled, out _, out _, out _, out _, out _);
                    if (HasEnabledWork(remainingElevation, remainingWater, remainingPopulation, remainingRail, remainingRoads, remainingRural, remainingTowns))
                    {
                        StatusText.Text = $"Seed {requestSeed} - loading enabled spawners...";
                    }
                    else if (HasPendingWork(remainingElevation, remainingWater, remainingPopulation, remainingRail, remainingRoads, remainingRural, remainingTowns, ruralEnabled))
                    {
                        StatusText.Text = PausedStatus(remainingElevation, remainingWater, remainingPopulation, remainingRail, remainingRoads, remainingRural, remainingTowns, ruralEnabled);
                    }
                    else
                    {
                        StatusText.Text = ReadyStatus();
                        Log("Visible chunks are ready for all enabled spawners.");
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (requestSeed == _seed) { StatusText.Text = "World worker error"; Log("ERROR: " + ex.Message); }
            }
            finally
            {
                _isLoading = false;
                if (_active != null && _active.Token == token) { _active.Dispose(); _active = null; }
            }
            var pendingElevation = MapView.GetMissingChunks();
            var pendingWater = MapView.GetChunksNeedingWater();
            var pendingPopulation = MapView.GetChunksNeedingPopulation(out _, out _);
            var pendingRail = MapView.GetChunksNeedingTransport(out _, out _, out _);
            var pendingRoads = MapView.GetChunksNeedingRoads(out _,out _);
            var pendingRural = MapView.GetChunksNeedingRural(out _, out _,out _,out _);
            var pendingTowns = MapView.GetChunksNeedingTowns(SpawnerIsEnabled(RuralSpawnerToggle), out _, out _, out _, out _, out _);
            if (HasEnabledWork(pendingElevation, pendingWater, pendingPopulation, pendingRail, pendingRoads, pendingRural, pendingTowns)) ScheduleVisibleChunks();
        }

        private string ReadyStatus() => $"Seed {_seed} - {MapView.ViewWidthMiles:0.##} x {MapView.ViewHeightMiles:0.##} mi - ready";

        private static bool SpawnerIsEnabled(CheckBox toggle) => toggle.IsChecked == true;

        private bool HasEnabledWork(IReadOnlyList<ChunkCoordinate> elevation, IReadOnlyList<ViewerScene> water,
            IReadOnlyList<PopulationInput> population, IReadOnlyList<TransportInput> rail, IReadOnlyList<TownInput> roads,
            IReadOnlyList<TownInput> rural, IReadOnlyList<TownInput> towns) =>
            (SpawnerIsEnabled(ElevationSpawnerToggle) && elevation.Count > 0) ||
            (SpawnerIsEnabled(WaterSpawnerToggle) && water.Count > 0) ||
            (SpawnerIsEnabled(PopulationSpawnerToggle) && population.Count > 0) ||
            (SpawnerIsEnabled(RailSpawnerToggle) && rail.Count > 0) ||
            (SpawnerIsEnabled(RoadSpawnerToggle) && roads.Count > 0) ||
            (SpawnerIsEnabled(RuralSpawnerToggle) && rural.Count > 0) ||
            (SpawnerIsEnabled(TownSpawnerToggle) && towns.Count > 0) ||
            (SpawnerIsEnabled(TreeSpawnerToggle) && MapView.GetChunksNeedingTrees(out _, out _, out _, out _, out _).Count > 0) ||
            (SpawnerIsEnabled(WallPlanToggle) && MapView.GetChunksNeedingWallPlans(out _).Count > 0);

        private bool HasPendingWork(IReadOnlyList<ChunkCoordinate> elevation, IReadOnlyList<ViewerScene> water,
            IReadOnlyList<PopulationInput> population, IReadOnlyList<TransportInput> rail, IReadOnlyList<TownInput> roads,
            IReadOnlyList<TownInput> rural, IReadOnlyList<TownInput> towns, bool ruralEnabled) =>
            elevation.Count > 0 || water.Count > 0 || population.Count > 0 || rail.Count > 0 || roads.Count > 0 || (ruralEnabled && rural.Count > 0) || towns.Count > 0 || MapView.GetChunksNeedingTrees(out _, out _, out _, out _, out _).Count > 0 || MapView.GetChunksNeedingWallPlans(out _).Count > 0;

        private string PausedStatus(IReadOnlyList<ChunkCoordinate> elevation, IReadOnlyList<ViewerScene> water,
            IReadOnlyList<PopulationInput> population, IReadOnlyList<TransportInput> rail, IReadOnlyList<TownInput> roads,
            IReadOnlyList<TownInput> rural, IReadOnlyList<TownInput> towns, bool ruralEnabled)
        {
            if (elevation.Count > 0 && !SpawnerIsEnabled(ElevationSpawnerToggle)) return $"Seed {_seed} - paused at Elevation";
            if (water.Count > 0 && !SpawnerIsEnabled(WaterSpawnerToggle)) return $"Seed {_seed} - paused at Water";
            if (population.Count > 0 && !SpawnerIsEnabled(PopulationSpawnerToggle)) return $"Seed {_seed} - paused at Population";
            if (rail.Count > 0 && !SpawnerIsEnabled(RailSpawnerToggle)) return $"Seed {_seed} - paused at Rail";
            if (roads.Count > 0 && !SpawnerIsEnabled(RoadSpawnerToggle)) return $"Seed {_seed} - paused at Roads";
            if (ruralEnabled && rural.Count > 0 && !SpawnerIsEnabled(RuralSpawnerToggle)) return $"Seed {_seed} - paused at Rural lots";
            if (towns.Count > 0 && !SpawnerIsEnabled(TownSpawnerToggle)) return $"Seed {_seed} - paused at Towns";
            if (MapView.GetChunksNeedingTrees(out _, out _, out _, out _, out _).Count > 0 && !SpawnerIsEnabled(TreeSpawnerToggle)) return $"Seed {_seed} - paused at Trees";
            if (MapView.GetChunksNeedingWallPlans(out _).Count > 0 && !SpawnerIsEnabled(WallPlanToggle)) return $"Seed {_seed} - paused at Near walls";
            return $"Seed {_seed} - waiting for enabled stages";
        }

        private void CancelActiveWorker()
        {
            var previous = _active;
            _active = null;
            if (previous == null) return;
            previous.Cancel();
            previous.Dispose();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _loadTimer.Stop();
            CancelActiveWorker();
        }

        private void Log(string message)
        {
            if (Dispatcher.HasShutdownStarted) return;
            ConsoleOutput.AppendText(message + Environment.NewLine);
            ConsoleOutput.ScrollToEnd();
        }
    }
}





