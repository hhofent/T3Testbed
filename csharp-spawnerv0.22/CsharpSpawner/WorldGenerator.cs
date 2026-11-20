using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace CsharpSpawner
{
    // Compatibility scene contract for the existing viewer/worker protocol.
    // Its X/Y coordinates are miles, north-positive Y. New library records in
    // WorldRecords.cs use feet with X east, Z north, and Y elevation.
    public sealed class MapPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public sealed class MapFeature
    {
        public string Kind { get; set; } = "contours";
        public string RouteId { get; set; }
        public string Carrier { get; set; }
        public string RailPassThrough { get; set; }
        public string Surface { get; set; }
        public List<MapPoint> Points { get; set; } = new List<MapPoint>();
        public double Width { get; set; }
        public double Height { get; set; }
        public double? DepthFt { get; set; }
        public bool Waterline { get; set; }
        public bool Disjoint { get; set; } = true;
        public bool Closed { get; set; }
        public bool IsMajorTown { get; set; }
        public int Population { get; set; }
        public string Name { get; set; }
        public int StreamOrder { get; set; }
        public double SiteSuitability { get; set; }
        public bool WaveApplied { get; set; }
        public int YearBuilt { get; set; }
        public string BuildingArchetype { get; set; }
        public uint BuildingPlanSeed { get; set; }
        public double FacingRadians { get; set; }
        public bool? IsOccupied { get; set; }
        public int HousingSlots { get; set; }
    }

    public sealed class RiverPath
    {
        public List<(double X, double Y)> Points { get; set; } = new List<(double, double)>();
        public int StreamOrder { get; set; }
        public double ValleyRadiusFeet { get; set; }
        public double CutDepthFeet { get; set; }
    }

    public sealed class ViewerScene
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public int ChunkX { get; set; }
        public int ChunkY { get; set; }
        public double SeaLevelFt { get; set; } = -50;
        // Conservative elevation-stage candidate tag; defaults true for old serialized scenes.
        public bool MayHaveWater { get; set; } = true;
        public List<MapFeature> Features { get; set; } = new List<MapFeature>();
        public List<MapFeature> RiverBeds { get; set; } = new List<MapFeature>();
        public ElevationGridData TerrainGrid { get; set; }
        public ElevationGridData WaterGrid { get; set; }
        public int CellCount { get; set; }
    }

    public sealed class ElevationGridData
    {
        public double OriginXFt { get; set; }
        public double OriginYFt { get; set; }
        public double StepFt { get; set; }
        public List<List<double>> Values { get; set; } = new List<List<double>>();
    }

    public sealed class WaterChunk
    {
        public int ChunkX { get; set; }
        public int ChunkY { get; set; }
        public List<MapFeature> Features { get; set; } = new List<MapFeature>();
    }

    public sealed class TransportChunk
    {
        public int ChunkX { get; set; }
        public int ChunkY { get; set; }
        public List<MapFeature> Features { get; set; } = new List<MapFeature>();
        public List<string> RemovedRuralLotIds { get; set; } = new List<string>();
        public List<string> RemovedRuralFeatureIds { get; set; } = new List<string>();
        public List<MapFeature> TreeReservations { get; set; } = new List<MapFeature>();
    }

    public sealed class Settler
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ClusterId { get; set; }
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public sealed class PopulationChunk
    {
        public int ChunkX { get; set; }
        public int ChunkY { get; set; }
        public List<MapFeature> Features { get; set; } = new List<MapFeature>();
        public List<Settler> Settlers { get; set; } = new List<Settler>();
    }

    public sealed class PopulationRequest
    {
        public int Seed { get; set; }
        public WorldGenerationSettings Settings { get; set; } = new();
        public List<PopulationInput> Chunks { get; set; } = new List<PopulationInput>();
        public List<MapFeature> WaterFeatures { get; set; } = new List<MapFeature>();
        public List<MapFeature> ExistingRoads { get; set; } = new List<MapFeature>();
    }

    public sealed class PopulationInput
    {
        public int ChunkX { get; set; }
        public int ChunkY { get; set; }
        public ElevationGridData TerrainGrid { get; set; }
    }

    public sealed class RoadRequest
    {
        public int Seed { get; set; }
        public WorldGenerationSettings Settings { get; set; } = new();
        public List<TownInput> Chunks { get; set; } = new List<TownInput>();
        public List<MapFeature> WaterFeatures { get; set; } = new List<MapFeature>();
        public List<MapFeature> MainRoadFeatures { get; set; } = new List<MapFeature>();
    }


    public sealed class TownRequest
    {
        public int Seed { get; set; }
        public WorldGenerationSettings Settings { get; set; } = new();
        public List<TownInput> Chunks { get; set; } = new List<TownInput>();
        public List<MapFeature> Settlements { get; set; } = new List<MapFeature>();
        public List<Settler> Settlers { get; set; } = new List<Settler>();
        public List<MapFeature> RailFeatures { get; set; } = new List<MapFeature>();
        public List<MapFeature> RoadFeatures { get; set; } = new List<MapFeature>();
        public List<MapFeature> WaterFeatures { get; set; } = new List<MapFeature>();
        public List<MapFeature> RuralFeatures { get; set; } = new List<MapFeature>();
    }

    public sealed class TownInput
    {
        public int ChunkX { get; set; }
        public int ChunkY { get; set; }
        public ElevationGridData TerrainGrid { get; set; }
    }

    public sealed class TreeInput
    {
        public int ChunkX { get; set; }
        public int ChunkY { get; set; }
    }

    public sealed class TreeChunk
    {
        public int ChunkX { get; set; }
        public int ChunkY { get; set; }
        public List<MapFeature> Features { get; set; } = new List<MapFeature>();
    }

    public sealed class TreeRequest
    {
        public int Seed { get; set; }
        public WorldGenerationSettings Settings { get; set; } = new();
        public List<TreeInput> Chunks { get; set; } = new List<TreeInput>();
        public List<MapFeature> RuralFeatures { get; set; } = new List<MapFeature>();
        public List<MapFeature> TreeReservations { get; set; } = new List<MapFeature>();
        public List<MapFeature> TownFeatures { get; set; } = new List<MapFeature>();
        public List<MapFeature> RoadFeatures { get; set; } = new List<MapFeature>();
        public List<MapFeature> RailFeatures { get; set; } = new List<MapFeature>();
        public List<MapFeature> WaterFeatures { get; set; } = new List<MapFeature>();
    }

    public sealed class WallInput { public int ChunkX { get; set; } public int ChunkY { get; set; } }
    public sealed class WallChunk { public int ChunkX { get; set; } public int ChunkY { get; set; } public List<WorldWallPlan> Plans { get; set; } = new(); }
    public sealed class WallRequest { public int Seed { get; set; } public List<WallInput> Chunks { get; set; } = new(); public List<MapFeature> TownFeatures { get; set; } = new(); }

    public sealed class RuralRequest
    {
        public int Seed { get; set; }
        public WorldGenerationSettings Settings { get; set; } = new();
        public List<TownInput> Chunks { get; set; } = new List<TownInput>();
        public List<MapFeature> Roads { get; set; } = new List<MapFeature>();
        public List<MapFeature> WaterFeatures { get; set; } = new List<MapFeature>();
        public List<MapFeature> RailFeatures { get; set; } = new List<MapFeature>();
        public List<MapFeature> Settlements { get; set; } = new List<MapFeature>();
    }

    public sealed class TransportRequest
    {
        public int Seed { get; set; }
        public WorldGenerationSettings Settings { get; set; } = new();
        public List<TransportInput> Chunks { get; set; } = new List<TransportInput>();
        public List<MapFeature> WaterFeatures { get; set; } = new List<MapFeature>();
        public List<MapFeature> MajorTowns { get; set; } = new List<MapFeature>();
        public List<MapFeature> MainRoads { get; set; } = new List<MapFeature>();
    }

    public sealed class TransportInput
    {
        public int ChunkX { get; set; }
        public int ChunkY { get; set; }
        public ElevationGridData TerrainGrid { get; set; }
        public List<MapFeature> WaterFeatures { get; set; } = new List<MapFeature>();
    }

    public sealed class WaterRequest
    {
        public int Seed { get; set; }
        public WorldGenerationSettings Settings { get; set; } = new();
        public List<WaterChunkInput> Chunks { get; set; } = new List<WaterChunkInput>();
    }

    public sealed class WaterChunkInput
    {
        public int ChunkX { get; set; }
        public int ChunkY { get; set; }
        public double SeaLevelFt { get; set; }
        public ElevationGridData WaterGrid { get; set; }
        public List<MapFeature> RiverBeds { get; set; } = new List<MapFeature>();
    }

    public sealed class ElevationRequest
    {
        public int Seed { get; set; }
        public WorldGenerationSettings Settings { get; set; } = new();
        public List<ChunkCoordinate> Chunks { get; set; } = new List<ChunkCoordinate>();
    }

    public sealed class ChunkCoordinate
    {
        public int X { get; set; }
        public int Y { get; set; }
        public ChunkCoordinate() { }
        public ChunkCoordinate(int x, int y) { X = x; Y = y; }
    }

    public static class WorldGenerator
    {
        // These defaults match python-spawnerv0.12split/settings.py.
        private const double FeetPerMile = 5280.0;
        private const double ContourIntervalFeet = 20.0;
        private const double ContourCellFeet = 660.0;
        private static WorldGenerationSettings _settings = new();
        private static double SeaLevelFeet => _settings.SeaLevelFeet;
        private static double SouthDropFeetPerMile => _settings.SouthDropFeetPerMile;
        private const double BasinMiles = 32.0;
        private const double Tau = Math.PI * 2.0;
        private static readonly Dictionary<(int Seed, int X, int Y), List<(double X, double Y)>> RouteCache = new Dictionary<(int, int, int), List<(double, double)>>();
        private static readonly Dictionary<(int Seed, int X, int Y), double> BaseTerrainCache = new Dictionary<(int, int, int), double>();
        internal readonly record struct LakeBasin(bool Active,double X,double Y,double Radius,double Floor,double Aspect,double Angle,double Phase);
        private static readonly Dictionary<(int Seed,int X,int Y,bool Pond),LakeBasin> LakeBasinCache = new();

        public static void Configure(WorldGenerationSettings settings)
        {
            settings ??= new WorldGenerationSettings();
            if (_settings.SeaLevelFeet != settings.SeaLevelFeet || _settings.SouthDropFeetPerMile != settings.SouthDropFeetPerMile)
            { RouteCache.Clear(); BaseTerrainCache.Clear(); LakeBasinCache.Clear(); }
            _settings = settings;
        }

        /// <summary>Higher-resolution samples of the same seeded, carved terrain surface used by the elevation and water passes.</summary>
        public static ElevationGridData GenerateDetailTerrainGrid(int seed, double leftFeet, double southFeet,
            double rightFeet, double northFeet, double stepFeet, WorldGenerationSettings settings = null,
            CancellationToken token = default)
        {
            Configure(settings ?? _settings);
            if (stepFeet < 10 || stepFeet > 30) throw new ArgumentOutOfRangeException(nameof(stepFeet));
            var runs = FindRiverBeds(seed, leftFeet - 16 * FeetPerMile, southFeet - 16 * FeetPerMile,
                rightFeet + 16 * FeetPerMile, northFeet + 16 * FeetPerMile, token)
                .Where(r => PathIntersects(r.Points, leftFeet - 4 * FeetPerMile, southFeet - 4 * FeetPerMile,
                    rightFeet + 4 * FeetPerMile, northFeet + 4 * FeetPerMile)).ToList();
            return MakeAnalyticGrid(seed, leftFeet, southFeet, rightFeet, northFeet, stepFeet, runs, token);
        }

        /// <summary>Typed, feet-based view of generated terrain for library consumers.</summary>
        public static WorldScene GenerateWorldChunk(int seed, int chunkX, int chunkNorth, CancellationToken token = default) =>
            LegacyMapFeatureConverter.ToWorldScene(GenerateChunk(seed, chunkX, chunkNorth, token));

        public static WorldScene GenerateWorldChunk(int seed, int chunkX, int chunkNorth, WorldGenerationSettings settings, CancellationToken token = default)
        { Configure(settings); return GenerateWorldChunk(seed, chunkX, chunkNorth, token); }

        public static ViewerScene GenerateChunk(int seed, int chunkX, int chunkY, CancellationToken token = default)
        {
            var scene = new ViewerScene { Width = 1, Height = 1, ChunkX = chunkX, ChunkY = chunkY, SeaLevelFt = SeaLevelFeet };
            double left = chunkX * FeetPerMile, top = chunkY * FeetPerMile;
            double right = left + FeetPerMile, bottom = top + FeetPerMile;
            double margin = 4 * FeetPerMile;
            var networkMargin = 16 * FeetPerMile;
            var routeRuns = FindRiverBeds(seed, left - networkMargin, top - networkMargin, right + networkMargin, bottom + networkMargin, token);
            scene.RiverBeds = ClipRiverBeds(routeRuns, left, top, right, bottom);
            var localRuns = routeRuns.Where(r => PathIntersects(r.Points, left - margin, top - margin, right + margin, bottom + margin)).ToList();
            scene.TerrainGrid = MakeTerrainGrid(seed, left - margin, top - margin, right + margin, bottom + margin, localRuns, token);
            scene.WaterGrid = MakeAnalyticGrid(seed, left, top, right, bottom, WaterSamplingStep(seed,left,top,right,bottom), localRuns, token);
            scene.MayHaveWater = scene.RiverBeds.Any(bed => bed.Points?.Count >= 2) ||
                scene.WaterGrid == null || scene.WaterGrid.Values == null || scene.WaterGrid.Values.Count < 2 ||
                scene.WaterGrid.Values.Any(row => row == null || row.Any(height => height <= SeaLevelFeet));

            int minX = (int)Math.Floor(left / ContourCellFeet) - 1;
            int maxX = (int)Math.Ceiling(right / ContourCellFeet) + 1;
            int minY = (int)Math.Floor(top / ContourCellFeet) - 1;
            int maxY = (int)Math.Ceiling(bottom / ContourCellFeet) + 1;
            int gridWidth = maxX - minX + 1, gridHeight = maxY - minY + 1;
            var elevations = new double[gridHeight, gridWidth];
            for (int gy = 0; gy < gridHeight; gy++)
            {
                token.ThrowIfCancellationRequested();
                for (int gx = 0; gx < gridWidth; gx++)
                    elevations[gy, gx] = SampleGrid(scene.TerrainGrid, (minX + gx) * ContourCellFeet, (minY + gy) * ContourCellFeet);
            }

            scene.CellCount = (gridWidth - 1) * (gridHeight - 1);
            var levels = new SortedDictionary<double, MapFeature>();
            for (int gy = 0; gy < gridHeight - 1; gy++)
            {
                token.ThrowIfCancellationRequested();
                for (int gx = 0; gx < gridWidth - 1; gx++)
                {
                    int ix = minX + gx, iy = minY + gy;
                    double x = ix * ContourCellFeet, y = iy * ContourCellFeet;
                    double cellRight = x + ContourCellFeet, cellBottom = y + ContourCellFeet;
                    if (cellRight <= left || x >= right || cellBottom <= top || y >= bottom) continue;
                    double a = elevations[gy, gx], b = elevations[gy, gx + 1];
                    double c = elevations[gy + 1, gx + 1], d = elevations[gy + 1, gx];
                    int topIndex = (int)Math.Ceiling((Math.Max(Math.Max(a, b), Math.Max(c, d)) - SeaLevelFeet) / ContourIntervalFeet);
                    int bottomIndex = (int)Math.Ceiling((SeaLevelFeet - Math.Min(Math.Min(a, b), Math.Min(c, d))) / ContourIntervalFeet);

                    for (int index = 0; index <= Math.Max(0, topIndex); index++)
                        AddCellContour(levels, x, y, a, b, c, d, SeaLevelFeet + index * ContourIntervalFeet, null, left, top, right, bottom);
                    for (int index = 1; index <= Math.Max(0, bottomIndex); index++)
                        AddCellContour(levels, x, y, a, b, c, d, SeaLevelFeet - index * ContourIntervalFeet, index * ContourIntervalFeet, left, top, right, bottom);
                }
            }

            foreach (var feature in levels.Values) if (feature.Points.Count > 0) scene.Features.Add(feature);
            return scene;
        }

        private static ElevationGridData MakeTerrainGrid(int seed, double left, double top, double right, double bottom,
            List<RiverPath> rivers, CancellationToken token)
        {
            // Half-mile samples retain the hill and valley shape needed by
            // route planning without multiplying wide-view chunk data too much.
            double step = FeetPerMile / 2.0;
            double ox = Math.Floor(left / step) * step, oy = Math.Floor(top / step) * step;
            int width = (int)Math.Ceiling((right - ox) / step) + 1, height = (int)Math.Ceiling((bottom - oy) / step) + 1;
            var grid = new ElevationGridData { OriginXFt = ox, OriginYFt = oy, StepFt = step };
            var influence = new double[height, width];
            var valleyFloor = new double[height, width];
            foreach (var path in rivers)
            {
            var river = path.Points;
            double radius = path.ValleyRadiusFeet;
            for (int j = 0; j + 1 < river.Count; j++)
            {
                var a = river[j]; var b = river[j + 1];
                int x0 = Math.Max(0, (int)Math.Floor((Math.Min(a.X, b.X) - radius - ox) / step));
                int x1 = Math.Min(width - 1, (int)Math.Ceiling((Math.Max(a.X, b.X) + radius - ox) / step));
                int y0 = Math.Max(0, (int)Math.Floor((Math.Min(a.Y, b.Y) - radius - oy) / step));
                int y1 = Math.Min(height - 1, (int)Math.Ceiling((Math.Max(a.Y, b.Y) + radius - oy) / step));
                for (int iy = y0; iy <= y1; iy++) for (int ix = x0; ix <= x1; ix++)
                {
                    var nearest = ClosestPoint(ox + ix * step, oy + iy * step, a, b);
                    double distance = nearest.Distance;
                    if (distance >= radius) continue;
                    double u = distance / radius, weight = Math.Exp(-4.5 * u * u);
                    if (weight > influence[iy, ix])
                    {
                        influence[iy, ix] = weight;
                        valleyFloor[iy, ix] = Math.Max(SeaLevelFeet + .25, SavedLandHeight(seed, nearest.Point.X, nearest.Point.Y) - path.CutDepthFeet);
                    }
                }
            }
            }
            for (int iy = 0; iy < height; iy++)
            {
                token.ThrowIfCancellationRequested();
                var row = new List<double>(width);
                for (int ix = 0; ix < width; ix++)
                {
                    double x = ox + ix * step, y = oy + iy * step;
                    double value = Math.Round(TerrainElevation(seed, x, y), 2);
                    double weightAtNode = influence[iy, ix], target = valleyFloor[iy, ix];
                    if (weightAtNode > 0 && value > target) value -= (value - target) * weightAtNode;
                    row.Add(Math.Round(value, 2));
                }
                grid.Values.Add(row);
            }
            return grid;
        }

        private static ElevationGridData MakeAnalyticGrid(int seed, double left, double top, double right, double bottom,
            double step, List<RiverPath> rivers, CancellationToken token)
        {
            double ox = Math.Floor(left / step) * step, oy = Math.Floor(top / step) * step;
            int width = (int)Math.Ceiling((right - ox) / step) + 1, height = (int)Math.Ceiling((bottom - oy) / step) + 1;
            var grid = new ElevationGridData { OriginXFt = ox, OriginYFt = oy, StepFt = step };
            for (int iy = 0; iy < height; iy++)
            {
                token.ThrowIfCancellationRequested();
                var row = new List<double>(width);
                for (int ix = 0; ix < width; ix++)
                    row.Add(Math.Round(CarvedElevation(seed, ox + ix * step, oy + iy * step, rivers), 2));
                grid.Values.Add(row);
            }
            return grid;
        }

        private static double SampleGrid(ElevationGridData grid, double x, double y)
        {
            double gx = (x - grid.OriginXFt) / grid.StepFt, gy = (y - grid.OriginYFt) / grid.StepFt;
            int ix = Math.Max(0, Math.Min(grid.Values[0].Count - 2, (int)Math.Floor(gx)));
            int iy = Math.Max(0, Math.Min(grid.Values.Count - 2, (int)Math.Floor(gy)));
            double tx = Math.Max(0, Math.Min(1, gx - ix)), ty = Math.Max(0, Math.Min(1, gy - iy));
            double a = grid.Values[iy][ix] * (1 - tx) + grid.Values[iy][ix + 1] * tx;
            double b = grid.Values[iy + 1][ix] * (1 - tx) + grid.Values[iy + 1][ix + 1] * tx;
            return a * (1 - ty) + b * ty;
        }

        private static double DistanceToSegment(double x, double y, (double X, double Y) a, (double X, double Y) b)
        {
            return ClosestPoint(x, y, a, b).Distance;
        }

        private static ((double X, double Y) Point, double Distance) ClosestPoint(double x, double y,
            (double X, double Y) a, (double X, double Y) b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double t = dx == 0 && dy == 0 ? 0 : Math.Max(0, Math.Min(1, ((x - a.X) * dx + (y - a.Y) * dy) / (dx * dx + dy * dy)));
            var point = (a.X + t * dx, a.Y + t * dy);
            return (point, Math.Sqrt(Math.Pow(x - point.Item1, 2) + Math.Pow(y - point.Item2, 2)));
        }

        private static List<RiverPath> FindRiverBeds(int seed, double left, double top, double right, double bottom, CancellationToken token)
        {
            double angle = BasinAngle(seed), basin = BasinMiles * FeetPerMile;
            var corners = new[] { Rotate(left, top, angle), Rotate(right, top, angle), Rotate(right, bottom, angle), Rotate(left, bottom, angle) };
            int minX = (int)Math.Floor(corners.Min(p => p.X) / basin) - 1;
            int maxX = (int)Math.Ceiling(corners.Max(p => p.X) / basin) + 1;
            int minY = (int)Math.Floor(corners.Min(p => p.Y) / basin) - 1;
            int maxY = (int)Math.Ceiling(corners.Max(p => p.Y) / basin) + 1;
            var result = new List<RiverPath>();
            for (int cx = minX; cx <= maxX; cx++)
            for (int cy = minY; cy <= maxY; cy++)
            {
                token.ThrowIfCancellationRequested();
                var route = RouteCurve(seed, cx, cy);
                double rx0 = route.Min(p => p.X), rx1 = route.Max(p => p.X), ry0 = route.Min(p => p.Y), ry1 = route.Max(p => p.Y);
                if (rx1 < left || rx0 > right || ry1 < top || ry0 > bottom) continue;
                AddAboveSeaRuns(seed, route, 3, cx, cy, result);
                var tributaries = BuildTributaries(seed, cx, cy, route);
                foreach (var tributary in tributaries)
                    AddAboveSeaRuns(seed, tributary.Points, tributary.StreamOrder, cx, cy, result);
            }
            return result;
        }

        private static void AddAboveSeaRuns(int seed, List<(double X, double Y)> route, int order, int cx, int cy, List<RiverPath> output)
        {
                var run = new List<(double X, double Y)>();
                for (int i = 0; i + 1 < route.Count; i++)
                {
                    var a = route[i]; var b = route[i + 1];
                    double ha = SavedLandHeight(seed, a.X, a.Y), hb = SavedLandHeight(seed, b.X, b.Y);
                    bool aboveA = ha > SeaLevelFeet, aboveB = hb > SeaLevelFeet;
                    if (aboveA && run.Count == 0) run.Add(a);
                    if (aboveA != aboveB)
                    {
                        double t = (SeaLevelFeet - ha) / (hb - ha);
                        var shore = (a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y));
                        if (aboveA) { run.Add(shore); if (run.Count > 1) output.Add(CreateRiverRun(seed, run, order)); run = new List<(double, double)>(); }
                        else { run = new List<(double, double)> { shore, b }; }
                    }
                    else if (aboveB) { if (run.Count == 0) run.Add(a); run.Add(b); }
                    else if (run.Count > 1) { output.Add(CreateRiverRun(seed, run, order)); run = new List<(double, double)>(); }
                }
                if (run.Count > 1) output.Add(CreateRiverRun(seed, run, order));
        }

        private static RiverPath CreateRiverRun(int seed, List<(double X, double Y)> points, int order)
        {
            var middle = points[points.Count / 2];
            ulong key = StableHash(seed, "river-valley-profile", order,
                (long)Math.Floor(middle.X / FeetPerMile), (long)Math.Floor(middle.Y / FeetPerMile));
            double unit = (key % 10001) / 10000.0;
            double radiusMiles = order >= 3 ? 2.0 + .95 * unit : order == 2 ? .65 + .45 * unit : .24 + .26 * unit;
            double cutDepth = order >= 3 ? 32 + 24 * unit : order == 2 ? 17 + 14 * unit : 8 + 10 * unit;
            return new RiverPath
            {
                Points = points,
                StreamOrder = order,
                ValleyRadiusFeet = radiusMiles * FeetPerMile,
                CutDepthFeet = cutDepth
            };
        }

        private static List<MapFeature> ClipRiverBeds(List<RiverPath> runs, double left, double top, double right, double bottom)
        {
            var output = new List<MapFeature>();
            foreach (var path in runs)
            {
                double widthFeet = path.StreamOrder >= 3 ? 100 : path.StreamOrder == 2 ? 38 : 14;
                var feature = new MapFeature
                {
                    Kind = "riverbeds", Width = widthFeet / FeetPerMile, Disjoint = true,
                    StreamOrder = path.StreamOrder, WaveApplied = true
                };
                var run = path.Points;
                for (int i = 0; i + 1 < run.Count; i++)
                {
                    if (!Clip(run[i], run[i + 1], left, top, right, bottom, out var a, out var b)) continue;
                    feature.Points.Add(new MapPoint { X = a.X / FeetPerMile, Y = a.Y / FeetPerMile });
                    feature.Points.Add(new MapPoint { X = b.X / FeetPerMile, Y = b.Y / FeetPerMile });
                }
                if (feature.Points.Count > 0) output.Add(feature);
            }
            return output;
        }

        private static List<RiverPath> BuildTributaries(int seed, int cx, int cy, List<(double X, double Y)> main)
        {
            var output = new List<RiverPath>();
            double mainLength = PolylineLength(main);
            if (mainLength < 8 * FeetPerMile) return output;
            int count = 2 + (int)(StableHash(seed, "trib-count", cx, cy) % 2);
            var attachments = new List<double>();
            for (int i = 0; i < count; i++)
            {
                ulong key = StableHash(seed, "trib-attach", cx, cy, i);
                double t = .16 + .68 * ((key % 10001) / 10000.0);
                if (attachments.Any(previous => Math.Abs(previous - t) * mainLength < 4.5 * FeetPerMile)) continue;
                attachments.Add(t);
                var join = PointAlong(main, t, out var tangent);
                double branchLength = (.16 + .14 * ((StableHash(seed, "trib-length", cx, cy, i) % 10001) / 10000.0)) * mainLength;
                var source = FindHigherSource(seed, join, tangent, branchLength, StableHash(seed, "trib-source", cx, cy, i), 28, 66);
                var tributary = new RiverPath { StreamOrder = 2, Points = ExtendUpstream(BuildIrregularReach(seed, $"trib:{cx}:{cy}:{i}", source, join, 2), .22 * FeetPerMile) };
                output.Add(tributary);

                int heads = 1 + (int)(StableHash(seed, "head-count", cx, cy, i) % 3);
                for (int h = 0; h < heads; h++)
                {
                    ulong headKey = StableHash(seed, "head-attach", cx, cy, i, h);
                    double ht = .24 + .52 * ((headKey % 10001) / 10000.0);
                    var headJoin = PointAlong(tributary.Points, ht, out var headTangent);
                    double headLength = (.045 + .065 * ((StableHash(seed, "head-length", cx, cy, i, h) % 10001) / 10000.0)) * mainLength;
                    var headSource = FindHigherSource(seed, headJoin, headTangent, headLength,
                        StableHash(seed, "head-source", cx, cy, i, h), 34, 72);
                    output.Add(new RiverPath { StreamOrder = 1, Points = ExtendUpstream(BuildIrregularReach(seed, $"head:{cx}:{cy}:{i}:{h}", headSource, headJoin, 1), .16 * FeetPerMile) });
                }
            }
            return output;
        }

        private static List<(double X, double Y)> ExtendUpstream(List<(double X, double Y)> points, double distance)
        {
            if (points == null || points.Count < 2) return points ?? new List<(double, double)>();
            var first = points[0]; var next = points[1];
            double length = Distance(first, next);
            if (length < 1e-9) return points;
            var extended = new List<(double X, double Y)>(points.Count + 1)
            {
                (first.X - (next.X - first.X) / length * distance, first.Y - (next.Y - first.Y) / length * distance)
            };
            extended.AddRange(points);
            return extended;
        }

        private static (double X, double Y) FindHigherSource(int seed, (double X, double Y) join,
            (double X, double Y) flow, double length, ulong key, double minAngle, double maxAngle)
        {
            double norm = Math.Sqrt(flow.X * flow.X + flow.Y * flow.Y);
            if (norm < 1e-9) { flow = (1, 0); norm = 1; }
            double ux = flow.X / norm, uy = flow.Y / norm, nx = -uy, ny = ux;
            (double X, double Y) best = (join.X - ux * length, join.Y - uy * length);
            double bestHeight = double.NegativeInfinity;
            for (int attempt = 0; attempt < 9; attempt++)
            {
                double u = ((key >> (attempt % 6 * 8)) & 0xffff) / 65535.0;
                double distanceFactor = .72 + .56 * (((u + attempt * .381966) % 1.0));
                double angle = (minAngle + (maxAngle - minAngle) * (((u + attempt * .618034) % 1.0))) * Math.PI / 180;
                double side = ((key >> (attempt % 6 * 8 + 3)) & 1) == 0 ? 1 : -1;
                var candidate = (join.X - ux * length * distanceFactor * Math.Cos(angle) + nx * side * length * distanceFactor * Math.Sin(angle),
                                 join.Y - uy * length * distanceFactor * Math.Cos(angle) + ny * side * length * distanceFactor * Math.Sin(angle));
                double elevation = SavedLandHeight(seed, candidate.Item1, candidate.Item2);
                if (elevation > bestHeight) { best = candidate; bestHeight = elevation; }
                if (elevation > SavedLandHeight(seed, join.X, join.Y) + 8) return candidate;
            }
            return best;
        }

        private static double PolylineLength(List<(double X, double Y)> points)
        {
            double length = 0;
            for (int i = 0; i + 1 < points.Count; i++) length += Distance(points[i], points[i + 1]);
            return length;
        }

        private static (double X, double Y) PointAlong(List<(double X, double Y)> points, double fraction, out (double X, double Y) tangent)
        {
            double total = PolylineLength(points), target = Math.Max(0, Math.Min(1, fraction)) * total, passed = 0;
            for (int i = 0; i + 1 < points.Count; i++)
            {
                double segment = Distance(points[i], points[i + 1]);
                if (passed + segment >= target || i == points.Count - 2)
                {
                    double t = segment <= 0 ? 0 : (target - passed) / segment;
                    tangent = (points[i + 1].X - points[i].X, points[i + 1].Y - points[i].Y);
                    return (points[i].X + tangent.X * t, points[i].Y + tangent.Y * t);
                }
                passed += segment;
            }
            tangent = (1, 0);
            return points.Count > 0 ? points[points.Count - 1] : (0, 0);
        }

        private static List<(double X, double Y)> BuildIrregularReach(int seed, string id,
            (double X, double Y) a, (double X, double Y) b, int order)
        {
            double chord = Distance(a, b);
            if (chord <= 1) return new List<(double, double)> { a, b };
            double ux = (b.X - a.X) / chord, uy = (b.Y - a.Y) / chord, nx = -uy, ny = ux;
            double baseSpacing = order >= 3 ? 18 * FeetPerMile : Math.Max(.9 * FeetPerMile, chord * .43);
            double amplitude = chord * (order >= 3 ? .009 : order == 2 ? .055 : .085);
            ulong broadKey = StableHash(seed, "river-broad-wave", id);
            double broadUnit = (broadKey % 10001) / 10000.0;
            double broadPhase = ((broadKey >> 16) / (double)(1UL << 48)) * Tau;
            double broadLength = chord * (order >= 3 ? 1.05 + .5 * broadUnit : order == 2 ? .65 + .6 * broadUnit : .45 + .55 * broadUnit);
            double broadAmp = chord * (order >= 3 ? .018 : order == 2 ? .024 : .03) *
                (broadKey % 11 == 0 ? 1.5 : .82 + .55 * broadUnit);
            var positions = new List<double> { 0 };
            int knot = 0;
            while (positions[positions.Count - 1] < .86 && knot < 12)
            {
                double variation = .62 + 1.12 * ((StableHash(seed, "river-knot-spacing", id, knot) % 10001) / 10000.0);
                double next = positions[positions.Count - 1] + baseSpacing * variation / chord;
                if (next >= .92) break;
                positions.Add(next);
                knot++;
            }
            positions.Add(1);

            var offsets = new double[positions.Count];
            double previous = 0;
            for (int k = 1; k + 1 < positions.Count; k++)
            {
                double spacing = (positions[k] - positions[k - 1]) * chord;
                double correlation = Math.Exp(-spacing / Math.Max(baseSpacing * 1.7, 1));
                double random = (StableHash(seed, "river-bend", id, k) % 20001) / 10000.0 - 1;
                previous = correlation * previous + Math.Sqrt(Math.Max(0, 1 - correlation * correlation)) * random;
                double t = positions[k];
                ulong lobeHash = StableHash(seed, "river-long-meander", id, k);
                double lobe = lobeHash % 9 == 0 ? 1.8 : .62 + 1.02 * ((lobeHash % 10001) / 10000.0);
                double preferred = previous * amplitude * lobe;
                offsets[k] = FollowTerrainSwales(seed, a, chord, ux, uy, nx, ny, t, preferred, amplitude * 1.8, order);
            }
            // Keep each reach tangent to its parent at the confluence and avoid
            // a visible kink where the global basin lattice joins adjacent links.
            if (offsets.Length > 3) { offsets[1] = 0; offsets[offsets.Length - 2] = 0; }

            double sampleStep = Math.Max(300, Math.Min(650, chord / 50));
            int sampleCount = Math.Max(2, (int)Math.Ceiling(chord / sampleStep));
            var points = new List<(double X, double Y)>(sampleCount + 1);
            for (int i = 0; i <= sampleCount; i++)
            {
                double t = (double)i / sampleCount;
                int segment = 0;
                while (segment + 2 < positions.Count && t > positions[segment + 1]) segment++;
                double t0 = positions[segment], t1 = positions[segment + 1], span = t1 - t0;
                double u = span <= 0 ? 0 : (t - t0) / span;
                double m0 = segment == 0 ? 0 : (offsets[segment + 1] - offsets[segment - 1]) / (positions[segment + 1] - positions[segment - 1]);
                double m1 = segment + 1 == positions.Count - 1 ? 0 : (offsets[segment + 2] - offsets[segment]) / (positions[segment + 2] - positions[segment]);
                double u2 = u * u, u3 = u2 * u;
                double lateral = (2 * u3 - 3 * u2 + 1) * offsets[segment] + (u3 - 2 * u2 + u) * span * m0 +
                                 (-2 * u3 + 3 * u2) * offsets[segment + 1] + (u3 - u2) * span * m1;
                double envelope = Math.Pow(Math.Sin(Math.PI * t), 2);
                lateral += broadAmp * Math.Sin(Tau * (chord * t) / broadLength + broadPhase) * envelope;
                points.Add((a.X + ux * chord * t + nx * lateral, a.Y + uy * chord * t + ny * lateral));
            }
            points[0] = a; points[points.Count - 1] = b;
            return points;
        }

        private static double FollowTerrainSwales(int seed, (double X, double Y) start, double chord,
            double ux, double uy, double nx, double ny, double t, double preferredOffset, double maxOffset, int order)
        {
            double search = Math.Min((order >= 3 ? .06 : .12) * FeetPerMile, maxOffset * .25);
            if (search < 40) return preferredOffset;
            double bestOffset = preferredOffset, bestScore = double.PositiveInfinity;
            foreach (double shift in new[] { -search, -search * .5, 0.0, search * .5, search })
            {
                double offset = Math.Max(-maxOffset, Math.Min(maxOffset, preferredOffset + shift));
                double x = start.X + ux * chord * t + nx * offset;
                double y = start.Y + uy * chord * t + ny * offset;
                double center = SavedLandHeight(seed, x, y);
                double left = SavedLandHeight(seed, x + nx * .22 * FeetPerMile, y + ny * .22 * FeetPerMile);
                double right = SavedLandHeight(seed, x - nx * .22 * FeetPerMile, y - ny * .22 * FeetPerMile);
                double valley = center - (left + right) * .5;
                double score = valley * 1.5 + center * .025 + Math.Abs(offset - preferredOffset) * .0008;
                if (score < bestScore) { bestScore = score; bestOffset = offset; }
            }
            return bestOffset;
        }

        private static bool PathIntersects(List<(double X, double Y)> points, double left, double top, double right, double bottom)
        {
            if (points == null || points.Count == 0) return false;
            return points.Max(p => p.X) >= left && points.Min(p => p.X) <= right &&
                   points.Max(p => p.Y) >= top && points.Min(p => p.Y) <= bottom;
        }

        private static double SavedLandHeight(int seed, double x, double y)
        {
            double step = FeetPerMile, gx = x / step, gy = y / step;
            int ix = (int)Math.Floor(gx), iy = (int)Math.Floor(gy);
            double tx = gx - ix, ty = gy - iy;
            double a = GridTerrain(seed, ix, iy) * (1 - tx) + GridTerrain(seed, ix + 1, iy) * tx;
            double b = GridTerrain(seed, ix, iy + 1) * (1 - tx) + GridTerrain(seed, ix + 1, iy + 1) * tx;
            return a * (1 - ty) + b * ty;
        }

        private static double GridTerrain(int seed, int x, int y)
        {
            var key = (seed, x, y);
            if (!BaseTerrainCache.TryGetValue(key, out double value))
            {
                value = TerrainElevation(seed, x * FeetPerMile, y * FeetPerMile);
                BaseTerrainCache[key] = value;
            }
            return value;
        }

        private static double BasinAngle(int seed) => (10 + StableHash(seed, "basin-angle") % 25001 / 1000.0) * Math.PI / 180;

        private static (double X, double Y) Rotate(double x, double y, double angle) =>
            (x * Math.Cos(angle) + y * Math.Sin(angle), -x * Math.Sin(angle) + y * Math.Cos(angle));
        private static (double X, double Y) Unrotate(double x, double y, double angle) =>
            (x * Math.Cos(angle) - y * Math.Sin(angle), x * Math.Sin(angle) + y * Math.Cos(angle));

        private static double Bearing(int seed, int sx, int sy)
        {
            double baseDegrees = (StableHash(seed, "bearing-base") % 12001) / 100.0 - 60;
            ulong key = StableHash(seed, "bearing", sx, sy);
            int parity = (int)((ulong)(sx + sy) + StableHash(seed, "bearing-phase") % 2UL) % 2;
            double perturb = (parity == 0 ? 1 : -1) * (27 + (key % 3001) / 1000.0);
            return (baseDegrees + perturb) * Math.PI / 180;
        }

        private static (double X, double Y) Outlet(int seed, int cx, int cy)
        {
            double basin = BasinMiles * FeetPerMile, angle = BasinAngle(seed);
            ulong key = StableHash(seed, "basin", cx, cy);
            double jx = ((key % 10001) / 10000.0 - .5) * .2 * basin;
            double jy = (((key / 10001) % 10001) / 10000.0 - .5) * .2 * basin;
            double theta = Bearing(seed, (int)Math.Floor(cx / 4.0), (int)Math.Floor(cy / 4.0));
            var along = Rotate(Math.Sin(theta), Math.Cos(theta), angle);
            jx += along.X * .28 * basin; jy += along.Y * .28 * basin;
            return Unrotate(cx * basin + jx, cy * basin + jy, angle);
        }

        private static double RoutingPotential(int seed, double x, double y) => LandHeight(seed, x, y) + TerrainTrend(seed, x, y);

        private static double TerrainTrend(int seed, double x, double y)
        {
            double angle = BasinAngle(seed); var p = Rotate(x, y, angle);
            double span = 4 * BasinMiles * FeetPerMile, gx = p.X / span, gy = p.Y / span;
            int sx = (int)Math.Floor(gx), sy = (int)Math.Floor(gy); double tx = gx - sx, ty = gy - sy;
            var cells = new[] { (sx, sy, (1 - tx) * (1 - ty)), (sx + 1, sy, tx * (1 - ty)), (sx, sy + 1, (1 - tx) * ty), (sx + 1, sy + 1, tx * ty) };
            double ux = 0, uy = 0;
            foreach (var cell in cells) { double a = Bearing(seed, cell.Item1, cell.Item2), w = cell.Item3; ux += Math.Sin(a) * w; uy += Math.Cos(a) * w; }
            double norm = Math.Max(1e-9, Math.Sqrt(ux * ux + uy * uy)); ux /= norm; uy /= norm;
            return -(x * ux + y * uy) / FeetPerMile * SouthDropFeetPerMile;
        }

        private static (int X, int Y, (double X, double Y) Point) NextOutlet(int seed, int cx, int cy)
        {
            var candidates = new List<(double Potential, ulong Tie, int X, int Y, (double X, double Y) Point)>();
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = cx + dx, ny = cy + dy; var point = Outlet(seed, nx, ny);
                candidates.Add((RoutingPotential(seed, point.X, point.Y), StableHash(seed, "outlet-route", cx, cy, nx, ny), nx, ny, point));
            }
            var lowest = candidates.OrderBy(v => v.Potential).ThenBy(v => v.Tie).First();
            return (lowest.X, lowest.Y, lowest.Point);
        }

        private static List<(double X, double Y)> RouteCurve(int seed, int cx, int cy)
        {
            var key = (seed, cx, cy);
            if (RouteCache.TryGetValue(key, out var cached)) return cached;
            var raw = RouteCurveRaw(seed, cx, cy);
            var next = NextOutlet(seed, cx, cy);
            var downstream = RouteCurveRaw(seed, next.X, next.Y);
            var join = raw[raw.Count - 1]; var b = downstream[1]; var a = join;
            double vx = b.X - a.X, vy = b.Y - a.Y;
            var previous = raw[raw.Count - 2]; double oldX = join.X - previous.X, oldY = join.Y - previous.Y;
            double length = Math.Min(Distance(join, previous), 30);
            if (length > 0 && Math.Sqrt(vx * vx + vy * vy) > 0)
            {
                double side = vx * oldY - vy * oldX >= 0 ? 1 : -1;
                string ident = $"river:outlet:{cx}:{cy}";
                double u = (StableHash(seed, "junction-angle", ident) % 10001) / 10000.0;
                double direction = Math.Atan2(vy, vx) + side * (25 + 60 * u) * Math.PI / 180;
                raw[raw.Count - 2] = (join.X - length * Math.Cos(direction), join.Y - length * Math.Sin(direction));
            }
            RouteCache[key] = raw;
            return raw;
        }

        private static List<(double X, double Y)> RouteCurveRaw(int seed, int cx, int cy)
        {
            var start = Outlet(seed, cx, cy); var next = NextOutlet(seed, cx, cy);
            return EdgeCurve(seed, cx, cy, start, next.Point);
        }

        private static List<(double X, double Y)> EdgeCurve(int seed, int cx, int cy, (double X, double Y) a, (double X, double Y) b)
        {
            return BuildIrregularReach(seed, $"river:outlet:{cx}:{cy}", a, b, 3);
        }

        private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

        private static void AddCellContour(SortedDictionary<double, MapFeature> levels, double x, double y,
            double a, double b, double c, double d, double level, double? depth,
            double left, double top, double right, double bottom)
        {
            var hits = new List<(double X, double Y)>(4);
            double cell = ContourCellFeet;
            if ((a <= level && level < b) || (b <= level && level < a)) hits.Add(Edge(x, y, x + cell, y, a, b, level));
            if ((b <= level && level < c) || (c <= level && level < b)) hits.Add(Edge(x + cell, y, x + cell, y + cell, b, c, level));
            if ((c <= level && level < d) || (d <= level && level < c)) hits.Add(Edge(x + cell, y + cell, x, y + cell, c, d, level));
            if ((d <= level && level < a) || (a <= level && level < d)) hits.Add(Edge(x, y + cell, x, y, d, a, level));
            if (hits.Count < 2) return;
            if (!levels.TryGetValue(level, out var feature))
            {
                feature = new MapFeature { Height = level, DepthFt = depth, Waterline = level == SeaLevelFeet };
                levels.Add(level, feature);
            }
            for (int i = 0; i + 1 < hits.Count; i += 2)
            {
                if (!Clip(hits[i], hits[i + 1], left, top, right, bottom, out var p0, out var p1)) continue;
                feature.Points.Add(new MapPoint { X = p0.X / FeetPerMile, Y = p0.Y / FeetPerMile });
                feature.Points.Add(new MapPoint { X = p1.X / FeetPerMile, Y = p1.Y / FeetPerMile });
            }
        }

        private static bool Clip((double X, double Y) a, (double X, double Y) b, double left, double top, double right, double bottom, out (double X, double Y) p0, out (double X, double Y) p1)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, t0 = 0, t1 = 1;
            bool Test(double p, double q)
            {
                if (Math.Abs(p) < 1e-12) return q >= 0;
                double r = q / p;
                if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
                else { if (r < t0) return false; if (r < t1) t1 = r; }
                return true;
            }
            if (!Test(-dx, a.X - left) || !Test(dx, right - a.X) || !Test(-dy, a.Y - top) || !Test(dy, bottom - a.Y))
            { p0 = default; p1 = default; return false; }
            p0 = (a.X + t0 * dx, a.Y + t0 * dy); p1 = (a.X + t1 * dx, a.Y + t1 * dy);
            return t1 >= t0;
        }

        private static (double X, double Y) Edge(double x0, double y0, double x1, double y1, double h0, double h1, double level)
        {
            double t = (level - h0) / (h1 - h0);
            return (x0 + (x1 - x0) * t, y0 + (y1 - y0) * t);
        }

        private static double TerrainElevation(int seed, double x, double y)
        {
            double raw = LandHeight(seed, x, y);
            double elevation = raw;
            if (raw < SeaLevelFeet)
            {
                double below = SeaLevelFeet - raw;
                double depth = Math.Min(below, 40) * .25 + Math.Max(0, below - 40) * 1.5;
                double ridge = 1.5 * ValueNoise(seed, x, y, 2.5, 31) + .5 * ValueNoise(seed, x, y, .8, 32);
                elevation = Math.Min(SeaLevelFeet, SeaLevelFeet - depth + ridge);
            }
            return AddOccasionalLakeBasin(seed, x, y, elevation);
        }

        private static double WaterSamplingStep(int seed,double left,double bottom,double right,double top)
        {
            foreach(bool pond in new[]{false,true})
            {
                double cell=(pond?2:10)*FeetPerMile;
                int cx=(int)Math.Floor((left+right)*.5/cell),cy=(int)Math.Floor((bottom+top)*.5/cell);
                for(int y=cy-1;y<=cy+1;y++)for(int x=cx-1;x<=cx+1;x++)
                {
                    var b=GetSmallLakeBasin(seed,x,y,pond);
                    if(b.Active&&b.X+b.Radius*1.25>=left&&b.X-b.Radius*1.25<=right&&b.Y+b.Radius*1.25>=bottom&&b.Y-b.Radius*1.25<=top)return 132;
                }
            }
            return ContourCellFeet;
        }

        internal static double AddOccasionalLakeBasin(int seed, double x, double y, double elevation)
        {
            for(int scale=0;scale<2;scale++)
            {
                bool pond=scale==1;double cellMiles=pond?2.0:10.0;
                int cellX=(int)Math.Floor(x/(cellMiles*FeetPerMile)),cellY=(int)Math.Floor(y/(cellMiles*FeetPerMile));
                for(int cy=cellY-1;cy<=cellY+1;cy++)for(int cx=cellX-1;cx<=cellX+1;cx++)
                {
                    var basin=GetSmallLakeBasin(seed,cx,cy,pond);
                    if(!basin.Active)continue;
                    double dx=x-basin.X,dy=y-basin.Y;
                    if(Math.Abs(dx)>basin.Radius*1.25||Math.Abs(dy)>basin.Radius*1.25)continue;
                    double ca=Math.Cos(basin.Angle),sa=Math.Sin(basin.Angle);
                    double u=(dx*ca+dy*sa)/basin.Radius,v=(-dx*sa+dy*ca)/(basin.Radius*basin.Aspect);
                    double angle=Math.Atan2(v,u);
                    double shore=1+.13*Math.Sin(3*angle+basin.Phase)+.07*Math.Sin(5*angle-basin.Phase);
                    double q=Math.Sqrt(u*u+v*v)/shore;
                    if(q>=1)continue;
                    // Carve actual terrain, keeping contours, water fill and parcel checks in agreement.
                    elevation=Math.Min(elevation,basin.Floor+(elevation-basin.Floor)*q*q*q);
                }
            }
            return elevation;
        }

        internal static LakeBasin GetSmallLakeBasin(int seed,int cellX,int cellY,bool pond)
        {
            var key=(seed,cellX,cellY,pond);
            if(LakeBasinCache.TryGetValue(key,out var cached))return cached;
            ulong hash=StableHash(seed,pond?"farm-pond":"small-lake",cellX,cellY);
            if(hash%(pond?7UL:3UL)!=0)return LakeBasinCache[key]=default;
            double cell=pond?2:10;
            double x=(cellX+.18+.64*(((hash>>16)%10001)/10000.0))*cell*FeetPerMile;
            double y=(cellY+.18+.64*(((hash>>29)%10001)/10000.0))*cell*FeetPerMile;
            if(LandHeight(seed,x,y)<SeaLevelFeet+20)return LakeBasinCache[key]=default;
            double radius=(pond?.09+.11*((hash>>42)%10001)/10000.0:.30+.55*((hash>>42)%10001)/10000.0)*FeetPerMile;
            double floor=SeaLevelFeet-(pond?5:15)-(pond?8.0:25.0)*((hash>>52)%1001)/1000.0;
            double aspect=.4+.6*((hash>>8)%1001)/1000.0,angle=(hash%6283)/1000.0;
            return LakeBasinCache[key]=new(true,x,y,radius,floor,aspect,angle,((hash>>37)%6283)/1000.0);
        }

        private static double LandHeight(int seed, double x, double y)
        {
            // A gently rolling plain with broad regional variation and smaller,
            // irregular relief. Domain warp breaks up the old repeated 4-mile
            // sine pattern while keeping the field deterministic across chunks.
            double mx = x / FeetPerMile, my = y / FeetPerMile;
            double warpX = ValueNoise(seed, x, y, 28, 41) * 1.8 * FeetPerMile;
            double warpY = ValueNoise(seed, x, y, 28, 42) * 1.8 * FeetPerMile;
            double wx = x + warpX, wy = y + warpY;
            return SeaLevelFeet + 65
                + 30 * ValueNoise(seed, x, y, 72, 1)
                + 22 * ValueNoise(seed, wx, wy, 24, 2)
                + 13 * ValueNoise(seed, wx, wy, 7, 3)
                + 4.5 * ValueNoise(seed, wx, wy, 2, 4)
                + 1.5 * ValueNoise(seed, mx * FeetPerMile, my * FeetPerMile, .65, 5);
        }

        private static double ValueNoise(int seed, double xFt, double yFt, double scaleMiles, int layer)
        {
            double scaleFt = scaleMiles * FeetPerMile;
            double gx = xFt / scaleFt, gy = yFt / scaleFt;
            long ix = (long)Math.Floor(gx), iy = (long)Math.Floor(gy);
            double tx = Fade(gx - ix), ty = Fade(gy - iy);
            double a = NoiseCorner(seed, ix, iy, layer), b = NoiseCorner(seed, ix + 1, iy, layer);
            double c = NoiseCorner(seed, ix, iy + 1, layer), d = NoiseCorner(seed, ix + 1, iy + 1, layer);
            double top = a + (b - a) * tx, bottom = c + (d - c) * tx;
            return top + (bottom - top) * ty;
        }

        private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

        private static double NoiseCorner(int seed, long x, long y, int layer)
        {
            unchecked
            {
                ulong h = (uint)seed + 0x9e3779b97f4a7c15UL;
                h ^= (ulong)x * 0xbf58476d1ce4e5b9UL;
                h = (h ^ (h >> 30)) * 0xbf58476d1ce4e5b9UL;
                h ^= (ulong)y * 0x94d049bb133111ebUL;
                h ^= (uint)layer * 0x632be59bd9b4e019UL;
                h = (h ^ (h >> 27)) * 0x94d049bb133111ebUL;
                h ^= h >> 31;
                return ((h >> 11) / (double)(1UL << 53)) * 2 - 1;
            }
        }

        private static double CarvedElevation(int seed, double x, double y, List<RiverPath> rivers)
        {
            double value = TerrainElevation(seed, x, y), weight = 0, valleyFloor = value;
            foreach (var path in rivers)
            {
            var river = path.Points;
            double radius = path.ValleyRadiusFeet;
            for (int i = 0; i + 1 < river.Count; i++)
            {
                var a = river[i]; var b = river[i + 1];
                if (x < Math.Min(a.X, b.X) - radius || x > Math.Max(a.X, b.X) + radius ||
                    y < Math.Min(a.Y, b.Y) - radius || y > Math.Max(a.Y, b.Y) + radius) continue;
                var nearest = ClosestPoint(x, y, a, b);
                double distance = nearest.Distance;
                if (distance >= radius) continue;
                double u = distance / radius, local = Math.Exp(-4.5 * u * u);
                if (local > weight)
                {
                    weight = local;
                    valleyFloor = Math.Max(SeaLevelFeet + .25, SavedLandHeight(seed, nearest.Point.X, nearest.Point.Y) - path.CutDepthFeet);
                }
            }
            }
            if (weight > 0 && value > valleyFloor) value -= (value - valleyFloor) * weight;
            return value;
        }

        private static ulong StableHash(params object[] parts)
        {
            var text = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i != 0) text.Append('|');
                text.Append(Convert.ToString(parts[i], CultureInfo.InvariantCulture));
            }
            byte[] digest = Blake2b.Hash(Encoding.UTF8.GetBytes(text.ToString()), 8);
            // Python int.from_bytes(digest, "big").
            ulong result = 0;
            for (int i = 0; i < digest.Length; i++) result = (result << 8) | digest[i];
            return result;
        }
    }

    // hashlib.blake2b(..., digest_size=8), used to preserve Python's keyed seed field.
    internal static class Blake2b
    {
        private static readonly ulong[] Iv = {
            0x6a09e667f3bcc908UL, 0xbb67ae8584caa73bUL, 0x3c6ef372fe94f82bUL, 0xa54ff53a5f1d36f1UL,
            0x510e527fade682d1UL, 0x9b05688c2b3e6c1fUL, 0x1f83d9abfb41bd6bUL, 0x5be0cd19137e2179UL };
        private static readonly byte[,] Sigma = {
            {0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15}, {14,10,4,8,9,15,13,6,1,12,0,2,11,7,5,3},
            {11,8,12,0,5,2,15,13,10,14,3,6,7,1,9,4}, {7,9,3,1,13,12,11,14,2,6,5,10,4,0,15,8},
            {9,0,5,7,2,4,10,15,14,1,11,12,6,8,3,13}, {2,12,6,10,0,11,8,3,4,13,7,5,15,14,1,9},
            {12,5,1,15,14,13,4,10,0,7,6,3,9,2,8,11}, {13,11,7,14,12,1,3,9,5,0,15,4,8,6,2,10},
            {6,15,14,9,11,3,0,8,12,2,13,7,1,4,10,5}, {10,2,8,4,7,6,1,5,15,11,9,14,3,12,13,0},
            {0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15}, {14,10,4,8,9,15,13,6,1,12,0,2,11,7,5,3} };

        public static byte[] Hash(byte[] input, int outputLength)
        {
            ulong[] h = (ulong[])Iv.Clone(); h[0] ^= 0x01010000UL ^ (uint)outputLength;
            byte[] block = new byte[128]; Array.Copy(input, block, input.Length);
            ulong[] m = new ulong[16];
            for (int i = 0; i < 16; i++) m[i] = BitConverter.ToUInt64(block, i * 8);
            ulong[] v = new ulong[16]; Array.Copy(h, v, 8); Array.Copy(Iv, 0, v, 8, 8);
            ulong count = (ulong)input.Length; v[12] ^= count; v[14] ^= ulong.MaxValue;
            for (int round = 0; round < 12; round++)
            {
                G(v, 0, 4, 8, 12, m[Sigma[round,0]], m[Sigma[round,1]]); G(v, 1, 5, 9, 13, m[Sigma[round,2]], m[Sigma[round,3]]);
                G(v, 2, 6, 10, 14, m[Sigma[round,4]], m[Sigma[round,5]]); G(v, 3, 7, 11, 15, m[Sigma[round,6]], m[Sigma[round,7]]);
                G(v, 0, 5, 10, 15, m[Sigma[round,8]], m[Sigma[round,9]]); G(v, 1, 6, 11, 12, m[Sigma[round,10]], m[Sigma[round,11]]);
                G(v, 2, 7, 8, 13, m[Sigma[round,12]], m[Sigma[round,13]]); G(v, 3, 4, 9, 14, m[Sigma[round,14]], m[Sigma[round,15]]);
            }
            for (int i = 0; i < 8; i++) h[i] ^= v[i] ^ v[i + 8];
            byte[] full = new byte[64]; for (int i = 0; i < 8; i++) Array.Copy(BitConverter.GetBytes(h[i]), 0, full, i * 8, 8);
            var result = new byte[outputLength]; Array.Copy(full, result, outputLength); return result;
        }

        private static void G(ulong[] v, int a, int b, int c, int d, ulong x, ulong y)
        {
            v[a] = unchecked(v[a] + v[b] + x); v[d] = Rotate(v[d] ^ v[a], 32);
            v[c] = unchecked(v[c] + v[d]); v[b] = Rotate(v[b] ^ v[c], 24);
            v[a] = unchecked(v[a] + v[b] + y); v[d] = Rotate(v[d] ^ v[a], 16);
            v[c] = unchecked(v[c] + v[d]); v[b] = Rotate(v[b] ^ v[c], 63);
        }
        private static ulong Rotate(ulong value, int n) => (value >> n) | (value << (64 - n));
    }
}
