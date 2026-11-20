using System;
using CsharpSpawner;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace csharp_spawnerv0._1
{
    internal sealed class MapSurface : FrameworkElement
    {
        private const double MinViewMiles = .1;
        private const double MaxViewMiles = 100;
        private const int ChunkCacheMargin = 2;
        private readonly Dictionary<(int X, int Y), ViewerScene> _chunks = new Dictionary<(int, int), ViewerScene>();
        private readonly Dictionary<(int X, int Y), WaterChunk> _waterChunks = new Dictionary<(int, int), WaterChunk>();
        private readonly Dictionary<(int X, int Y), PopulationChunk> _populationChunks = new Dictionary<(int, int), PopulationChunk>();
        private readonly Dictionary<(int X, int Y), TransportChunk> _transportChunks = new Dictionary<(int, int), TransportChunk>();
        private readonly Dictionary<(int X, int Y), TransportChunk> _roadChunks = new Dictionary<(int, int), TransportChunk>();
        private readonly Dictionary<(int X, int Y), TransportChunk> _ruralChunks = new Dictionary<(int, int), TransportChunk>();
        private readonly Dictionary<(int X, int Y), TransportChunk> _townChunks = new Dictionary<(int, int), TransportChunk>();
        private readonly Dictionary<(int X, int Y), TreeChunk> _treeChunks = new Dictionary<(int, int), TreeChunk>();
        private readonly Dictionary<(int X, int Y), WallChunk> _wallChunks = new Dictionary<(int, int), WallChunk>();
        private readonly Dictionary<(int X, int Y), BitmapSource> _elevationBitmaps = new Dictionary<(int, int), BitmapSource>();
        private readonly Dictionary<(int X, int Y), (Geometry Regular, Geometry Waterline, Geometry Depth)> _contourGeometry = new();
        private readonly DispatcherTimer _renderBatchTimer;
        private double _centerX = .5, _centerY = .5, _viewSizeMiles = 5;
        private bool _initialTownFramed;
        private bool _dragging;
        private Point _dragStart;
        private double _dragCenterX, _dragCenterY;
        private long _renderCount;
        private double _lastRenderMilliseconds;
        private double _maxRenderMilliseconds;
        public long RenderCount => _renderCount;
        public double LastRenderMilliseconds => _lastRenderMilliseconds;
        public double MaxRenderMilliseconds => _maxRenderMilliseconds;

        public event EventHandler VisibleChunksChanged;
        public double ViewSizeMiles => _viewSizeMiles;
        public double ViewWidthMiles => _viewSizeMiles;
        public double ViewHeightMiles => RenderSize.Width <= 0 ? _viewSizeMiles : _viewSizeMiles * RenderSize.Height / RenderSize.Width;
        public MapSurface()
        {
            Focusable = true; ClipToBounds = true; Cursor = Cursors.Hand;
            _renderBatchTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
            _renderBatchTimer.Tick += (_, __) => { _renderBatchTimer.Stop(); InvalidateVisual(); };
        }

        private void RequestRender()
        {
            if (!_renderBatchTimer.IsEnabled) _renderBatchTimer.Start();
        }

        public void SetChunk(ViewerScene chunk)
        {
            var key = (chunk.ChunkX, chunk.ChunkY);
            _chunks[key] = chunk;
            _contourGeometry.Remove(key);
            _elevationBitmaps.Remove(key);
            RequestRender();
        }

        public void SetChunks(IReadOnlyList<ViewerScene> chunks)
        {
            foreach (var chunk in chunks)
            {
                var key = (chunk.ChunkX, chunk.ChunkY);
                _chunks[key] = chunk;
                _contourGeometry.Remove(key);
                _elevationBitmaps.Remove(key);
                if (!chunk.MayHaveWater) _waterChunks[key] = new WaterChunk { ChunkX = chunk.ChunkX, ChunkY = chunk.ChunkY };
                else _waterChunks.Remove(key);
            }
            if (chunks.Count > 0) RequestRender();
        }

        public void SetWaterChunk(WaterChunk chunk)
        {
            _waterChunks[(chunk.ChunkX, chunk.ChunkY)] = chunk;
            RequestRender();
        }

        public void SetWaterChunks(IReadOnlyList<WaterChunk> chunks)
        {
            foreach (var chunk in chunks) _waterChunks[(chunk.ChunkX, chunk.ChunkY)] = chunk;
            if (chunks.Count > 0) RequestRender();
        }

        public void SetTransportChunks(IReadOnlyList<TransportChunk> chunks)
        {
            foreach (var chunk in chunks) _transportChunks[(chunk.ChunkX, chunk.ChunkY)] = chunk;
            if (chunks.Count > 0) RequestRender();
        }

        public void SetPopulationChunks(IReadOnlyList<PopulationChunk> chunks)
        {
            foreach (var chunk in chunks) _populationChunks[(chunk.ChunkX, chunk.ChunkY)] = chunk;
            if (chunks.Count > 0) RequestRender();
        }

        public void SetRoadChunks(IReadOnlyList<TransportChunk> chunks)
        {
            foreach (var chunk in chunks) _roadChunks[(chunk.ChunkX, chunk.ChunkY)] = chunk;
            if (chunks.Count > 0) RequestRender();
        }

        public void SetTownChunks(IReadOnlyList<TransportChunk> chunks)
        {
            var removed = new HashSet<string>(chunks.SelectMany(chunk => (chunk.RemovedRuralLotIds ?? new List<string>())
                .Concat(chunk.RemovedRuralFeatureIds ?? new List<string>())), StringComparer.Ordinal);
            if (removed.Count > 0)
            {
                foreach (var ruralChunk in _ruralChunks.Values)
                    ruralChunk.Features.RemoveAll(feature => removed.Contains(feature.RouteId ?? "") || removed.Contains(feature.Carrier ?? ""));
            }
            foreach (var chunk in chunks) _townChunks[(chunk.ChunkX, chunk.ChunkY)] = chunk;
            bool framed=TryFrameFirstTown();
            if (chunks.Count > 0) RequestRender();
            if (framed) VisibleChunksChanged?.Invoke(this,EventArgs.Empty);
        }

        private bool TryFrameFirstTown()
        {
            if(_initialTownFramed)return false;
            var town=_townChunks.Values.SelectMany(chunk=>chunk.Features??new List<MapFeature>())
                .Where(feature=>feature.Kind=="town"&&feature.Points?.Count>=4)
                .OrderBy(feature=>feature.RouteId,StringComparer.Ordinal).FirstOrDefault();
            if(town==null)return false;
            double minX=town.Points.Min(point=>point.X),maxX=town.Points.Max(point=>point.X);
            double minY=town.Points.Min(point=>point.Y),maxY=town.Points.Max(point=>point.Y);
            _centerX=(minX+maxX)*.5;_centerY=(minY+maxY)*.5;
            double aspect=RenderSize.Height>0?RenderSize.Width/RenderSize.Height:1.6;
            _viewSizeMiles=Math.Clamp(Math.Max((maxX-minX)*1.25,(maxY-minY)*aspect*1.25),MinViewMiles,MaxViewMiles);
            _initialTownFramed=true;
            return true;
        }
        public void SetTreeChunks(IReadOnlyList<TreeChunk> chunks)
        {
            foreach (var chunk in chunks) _treeChunks[(chunk.ChunkX, chunk.ChunkY)] = chunk;
            if (chunks.Count > 0) RequestRender();
        }

        public void SetWallChunks(IReadOnlyList<WallChunk> chunks)
        {
            foreach (var chunk in chunks) _wallChunks[(chunk.ChunkX, chunk.ChunkY)] = chunk;
            if (chunks.Count > 0) RequestRender();
        }

        public void SetRuralChunks(IReadOnlyList<TransportChunk> chunks)
        {
            var removed = new HashSet<string>(_townChunks.Values.SelectMany(chunk => (chunk.RemovedRuralLotIds ?? new List<string>())
                .Concat(chunk.RemovedRuralFeatureIds ?? new List<string>())), StringComparer.Ordinal);
            foreach (var chunk in chunks)
            {
                if (removed.Count > 0)
                    chunk.Features.RemoveAll(feature => removed.Contains(feature.RouteId ?? "") || removed.Contains(feature.Carrier ?? ""));
                _ruralChunks[(chunk.ChunkX, chunk.ChunkY)] = chunk;
            }
            if (chunks.Count > 0) RequestRender();
        }

        public void ClearChunks()
        {
            _chunks.Clear();
            _waterChunks.Clear();
            _populationChunks.Clear();
            _transportChunks.Clear();
            _roadChunks.Clear();
            _ruralChunks.Clear();
            _townChunks.Clear();
            _treeChunks.Clear();
            _wallChunks.Clear();
            _elevationBitmaps.Clear();
            _contourGeometry.Clear();
            RequestRender();
            VisibleChunksChanged?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<ChunkCoordinate> GetMissingChunks()
        {
            var visible = GetVisibleChunkCoordinates();
            return visible.Where(key => !_chunks.ContainsKey((key.X, key.Y))).ToList();
        }

        public IReadOnlyList<ViewerScene> GetChunksNeedingWater()
        {
            var scenes = new List<ViewerScene>();
            foreach (var coordinate in GetVisibleChunkCoordinates())
                if (_chunks.TryGetValue((coordinate.X, coordinate.Y), out var scene) && scene.MayHaveWater &&
                    !_waterChunks.ContainsKey((coordinate.X, coordinate.Y))) scenes.Add(scene);
            return scenes;
        }

        public IReadOnlyList<TransportInput> GetChunksNeedingTransport(out IReadOnlyList<MapFeature> savedWaterFeatures,
            out IReadOnlyList<MapFeature> railTowns, out IReadOnlyList<MapFeature> mainRoads)
        {
            var inputs = new List<TransportInput>();
            savedWaterFeatures = _waterChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>()).ToList();
            railTowns = _populationChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>())
                .Where(feature => feature.Kind == "settlement").ToList();
            mainRoads = _populationChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>())
                .Where(feature => feature.Kind == "main_road").ToList();
            foreach (var coordinate in GetVisibleChunkCoordinates())
                if (_chunks.TryGetValue((coordinate.X, coordinate.Y), out var scene) &&
                    _waterChunks.ContainsKey((coordinate.X, coordinate.Y)) &&
                    _populationChunks.ContainsKey((coordinate.X, coordinate.Y)) &&
                    !_transportChunks.ContainsKey((coordinate.X, coordinate.Y)))
                    inputs.Add(new TransportInput { ChunkX = scene.ChunkX, ChunkY = scene.ChunkY, TerrainGrid = scene.TerrainGrid });
            return inputs;
        }

        public IReadOnlyList<PopulationInput> GetChunksNeedingPopulation(out IReadOnlyList<MapFeature> savedWaterFeatures,
            out IReadOnlyList<MapFeature> existingRoads)
        {
            var inputs = new List<PopulationInput>();
            savedWaterFeatures = _waterChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>()).ToList();
            existingRoads = _populationChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>())
                .Concat(_roadChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>()))
                .Where(feature => feature.Kind == "main_road" || feature.Kind == "local_road").ToList();
            foreach (var coordinate in GetVisibleChunkCoordinates())
                if (_chunks.TryGetValue((coordinate.X, coordinate.Y), out var scene) &&
                    _waterChunks.ContainsKey((coordinate.X, coordinate.Y)) &&
                    !_populationChunks.ContainsKey((coordinate.X, coordinate.Y)))
                    inputs.Add(new PopulationInput { ChunkX = scene.ChunkX, ChunkY = scene.ChunkY, TerrainGrid = scene.TerrainGrid });
            return inputs;
        }

        public IReadOnlyList<TownInput> GetChunksNeedingRoads(out IReadOnlyList<MapFeature> waterFeatures,out IReadOnlyList<MapFeature> mainRoadFeatures)
        {
            var result = new List<TownInput>();
            waterFeatures = _waterChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>()).ToList();
            mainRoadFeatures=_populationChunks.Values.SelectMany(chunk=>chunk.Features??new List<MapFeature>()).Where(f=>f.Kind=="main_road").ToList();
            foreach (var coordinate in GetVisibleChunkCoordinates())
                if (_chunks.TryGetValue((coordinate.X, coordinate.Y), out var scene) &&
                    _populationChunks.ContainsKey((coordinate.X, coordinate.Y)) &&
                    _waterChunks.ContainsKey((coordinate.X, coordinate.Y)) &&
                    !_roadChunks.ContainsKey((coordinate.X, coordinate.Y)))
                    result.Add(new TownInput { ChunkX = scene.ChunkX, ChunkY = scene.ChunkY, TerrainGrid = scene.TerrainGrid });
            return result;
        }

        public IReadOnlyList<TownInput> GetChunksNeedingTowns(out IReadOnlyList<MapFeature> settlements,
            out IReadOnlyList<MapFeature> railFeatures, out IReadOnlyList<MapFeature> waterFeatures)
        {
            var inputs = new List<TownInput>();
            settlements = _populationChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>())
                .Where(feature => feature.Kind == "settlement").ToList();
            railFeatures = _transportChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>())
                .Where(feature => feature.Kind == "rail").ToList();
            waterFeatures = _waterChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>()).ToList();
            foreach (var coordinate in GetVisibleChunkCoordinates())
                if (_chunks.TryGetValue((coordinate.X, coordinate.Y), out var scene) &&
                    _waterChunks.ContainsKey((coordinate.X, coordinate.Y)) &&
                    _populationChunks.ContainsKey((coordinate.X, coordinate.Y)) &&
                    !_townChunks.ContainsKey((coordinate.X, coordinate.Y)))
                    inputs.Add(new TownInput { ChunkX = scene.ChunkX, ChunkY = scene.ChunkY, TerrainGrid = scene.TerrainGrid });
            return inputs;
        }

        public IReadOnlyList<TownInput> GetChunksNeedingRural(out IReadOnlyList<MapFeature> roads,
            out IReadOnlyList<MapFeature> waterFeatures,out IReadOnlyList<MapFeature> railFeatures,out IReadOnlyList<MapFeature> settlements)
        {
            var inputs = new List<TownInput>();
            if (_viewSizeMiles > 12)
            {
                roads = waterFeatures = railFeatures = settlements = Array.Empty<MapFeature>();
                return inputs;
            }
            roads = _populationChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>())
                .Concat(_roadChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>()))
                .Where(feature => feature.Kind == "local_road" || feature.Kind == "main_road").ToList();
            var ruralRoadCandidates = new HashSet<(int X, int Y)>();
            foreach (var feature in roads.Where(feature => feature.Kind == "local_road" && feature.Points?.Count > 0))
            {
                int minX = (int)Math.Floor(feature.Points.Min(p => p.X) - .01), maxX = (int)Math.Floor(feature.Points.Max(p => p.X) + .01);
                int minY = (int)Math.Floor(feature.Points.Min(p => p.Y) - .01), maxY = (int)Math.Floor(feature.Points.Max(p => p.Y) + .01);
                for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++) ruralRoadCandidates.Add((x, y));
            }
            waterFeatures = _waterChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>()).ToList();
            railFeatures=_transportChunks.Values.SelectMany(chunk=>chunk.Features??new List<MapFeature>()).Where(feature=>feature.Kind=="rail").ToList();
            settlements=_populationChunks.Values.SelectMany(chunk=>chunk.Features??new List<MapFeature>()).Where(feature=>feature.Kind=="settlement").ToList();
            foreach (var coordinate in GetVisibleChunkCoordinates())
                if (_chunks.TryGetValue((coordinate.X, coordinate.Y), out var scene) &&
                    _populationChunks.ContainsKey((coordinate.X, coordinate.Y)) &&
                    _waterChunks.ContainsKey((coordinate.X, coordinate.Y)) &&
                    _roadChunks.ContainsKey((coordinate.X, coordinate.Y)) &&
                    !_ruralChunks.ContainsKey((coordinate.X, coordinate.Y)))
                {
                    bool hasLocalRoad = ruralRoadCandidates.Contains((coordinate.X, coordinate.Y));
                    if (hasLocalRoad)
                        inputs.Add(new TownInput { ChunkX = scene.ChunkX, ChunkY = scene.ChunkY, TerrainGrid = scene.TerrainGrid });
                    else
                        _ruralChunks[(coordinate.X, coordinate.Y)] = new TransportChunk { ChunkX = coordinate.X, ChunkY = coordinate.Y };
                }
            return inputs;
        }

        public IReadOnlyList<TownInput> GetChunksNeedingTowns(bool requireRural,
            out IReadOnlyList<MapFeature> settlements, out IReadOnlyList<MapFeature> railFeatures,
            out IReadOnlyList<MapFeature> roadFeatures, out IReadOnlyList<MapFeature> waterFeatures,
            out IReadOnlyList<MapFeature> ruralFeatures)
        {
            var inputs = new List<TownInput>();
            requireRural = requireRural && _viewSizeMiles <= 12;
            settlements = _populationChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>())
                .Where(feature => feature.Kind == "settlement").ToList();
            railFeatures = _transportChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>())
                .Where(feature => feature.Kind == "rail").ToList();
            roadFeatures = _populationChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>())
                .Concat(_roadChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>()))
                .Where(feature => feature.Kind == "main_road" || feature.Kind == "local_road").ToList();
            waterFeatures = _waterChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>()).ToList();
            ruralFeatures = _ruralChunks.Values.SelectMany(chunk => chunk.Features ?? new List<MapFeature>()).ToList();
            foreach (var coordinate in GetVisibleChunkCoordinates())
                if (_chunks.TryGetValue((coordinate.X, coordinate.Y), out var scene) &&
                    _waterChunks.ContainsKey((coordinate.X, coordinate.Y)) &&
                    _populationChunks.ContainsKey((coordinate.X, coordinate.Y)) &&
                    _roadChunks.ContainsKey((coordinate.X, coordinate.Y)) &&
                    (!requireRural || _ruralChunks.ContainsKey((coordinate.X, coordinate.Y))) &&
                    !_townChunks.ContainsKey((coordinate.X, coordinate.Y)))
                    inputs.Add(new TownInput { ChunkX = scene.ChunkX, ChunkY = scene.ChunkY, TerrainGrid = scene.TerrainGrid });
            return inputs;
        }

        public IReadOnlyList<Settler> GetAllSettlers() => _populationChunks.Values
            .SelectMany(chunk => chunk.Settlers ?? new List<Settler>()).ToList();

        public IReadOnlyList<TreeInput> GetChunksNeedingTrees(out IReadOnlyList<MapFeature> rural,
            out IReadOnlyList<MapFeature> towns, out IReadOnlyList<MapFeature> roads,
            out IReadOnlyList<MapFeature> rails, out IReadOnlyList<MapFeature> water)
        {
            var inputs = new List<TreeInput>();
            if (_viewSizeMiles > 1.5)
            {
                rural = towns = roads = rails = water = Array.Empty<MapFeature>();
                return inputs;
            }
            rural = _ruralChunks.Values.SelectMany(c => c.Features ?? new List<MapFeature>()).ToList();
            towns = _townChunks.Values.SelectMany(c => c.Features ?? new List<MapFeature>()).ToList();
            roads = _populationChunks.Values.SelectMany(c => c.Features ?? new List<MapFeature>())
                .Concat(_roadChunks.Values.SelectMany(c => c.Features ?? new List<MapFeature>())).ToList();
            rails = _transportChunks.Values.SelectMany(c => c.Features ?? new List<MapFeature>()).ToList();
            water = _waterChunks.Values.SelectMany(c => c.Features ?? new List<MapFeature>()).ToList();
            foreach (var coordinate in GetVisibleChunkCoordinates())
                if (_chunks.ContainsKey((coordinate.X, coordinate.Y)) && _townChunks.ContainsKey((coordinate.X, coordinate.Y)) &&
                    !_treeChunks.ContainsKey((coordinate.X, coordinate.Y)))
                    inputs.Add(new TreeInput { ChunkX = coordinate.X, ChunkY = coordinate.Y });
            return inputs;
        }

        public IReadOnlyList<WallInput> GetChunksNeedingWallPlans(out IReadOnlyList<MapFeature> townFeatures)
        {
            townFeatures = _townChunks.Values.SelectMany(c => c.Features ?? new List<MapFeature>()).ToList();
            if (_viewSizeMiles >= 1.5) return Array.Empty<WallInput>();
            return GetVisibleChunkCoordinates().Where(c => _townChunks.ContainsKey((c.X, c.Y)) &&
                _treeChunks.ContainsKey((c.X, c.Y)) && !_wallChunks.ContainsKey((c.X, c.Y)))
                .Select(c => new WallInput { ChunkX = c.X, ChunkY = c.Y }).ToList();
        }

        protected override void OnRender(DrawingContext dc)
        {
            var renderWatch = Stopwatch.StartNew();
            base.OnRender(dc);
            dc.DrawRectangle(Brush("#f5f4ec"), null, new Rect(RenderSize));
            if (RenderSize.Width <= 0 || RenderSize.Height <= 0) return;
            Rect mapRect = ViewportRect();
            dc.PushClip(new RectangleGeometry(mapRect));
            double scale = mapRect.Width / _viewSizeMiles;
            double halfY = ViewHeightMiles / 2;
            foreach (var chunk in _chunks.Values)
            {
                if (!IsChunkVisible(chunk.ChunkX, chunk.ChunkY, halfY) || chunk.WaterGrid == null) continue;
                var key = (chunk.ChunkX, chunk.ChunkY);
                if (!_elevationBitmaps.TryGetValue(key, out var relief))
                    _elevationBitmaps[key] = relief = CreateElevationBitmap(chunk.WaterGrid, chunk.SeaLevelFt);
                dc.DrawImage(relief, new Rect(Map(chunk.ChunkX, chunk.ChunkY, scale, mapRect),
                    Map(chunk.ChunkX + 1, chunk.ChunkY + 1, scale, mapRect)));
            }
            var lakes = new StreamGeometry();
            using (var lakeContext = lakes.Open())
            foreach (var chunk in _waterChunks.Values)
            {
                if (!IsChunkVisible(chunk.ChunkX, chunk.ChunkY, halfY)) continue;
                foreach (var feature in chunk.Features)
                {
                    if (feature.Kind == "lakes") AddLake(lakeContext, feature, scale, mapRect);
                }
            }
            lakes.Freeze();
            dc.DrawGeometry(Brush("#9bcbd3"), null, lakes);

            double contourWidth = _viewSizeMiles > 48 ? .125 : _viewSizeMiles > 12 ? .225 : .375;
            // The cached world-coordinate geometry is drawn under a mile-to-pixel
            // transform, which scales pen thickness too. Divide by that scale so
            // contour strokes remain thin screen-space lines at every zoom.
            var contourRegularPen = CreatePen("#a7afa7", contourWidth / Math.Max(scale, 1e-6));
            var contourWaterPen = CreatePen("#91bfc5", (contourWidth + .125) / Math.Max(scale, 1e-6));
            var contourDepthPen = CreateDepthPen(contourWidth / Math.Max(scale, 1e-6));
            {
                var transform = new MatrixTransform(new Matrix(scale, 0, 0, scale,
                    mapRect.Left + mapRect.Width / 2 - _centerX * scale,
                    mapRect.Top + mapRect.Height / 2 - _centerY * scale));
                foreach (var chunk in _chunks.Values)
                {
                    if (!IsChunkVisible(chunk.ChunkX, chunk.ChunkY, halfY)) continue;
                    var key = (chunk.ChunkX, chunk.ChunkY);
                    if (!_contourGeometry.TryGetValue(key, out var contours))
                        _contourGeometry[key] = contours = BuildContourGeometry(chunk);
                    dc.PushTransform(transform);
                    dc.DrawGeometry(null, contourRegularPen, contours.Regular);
                    dc.DrawGeometry(null, contourWaterPen, contours.Waterline);
                    dc.DrawGeometry(null, contourDepthPen, contours.Depth);
                    dc.Pop();
                }
            }
            var roads = new StreamGeometry();
            var localRoads = new StreamGeometry();
            var dirtRoads = new StreamGeometry();
            var countyRoads = new StreamGeometry();
            var rails = new StreamGeometry();
            var townResidentIds=_townChunks.Values.SelectMany(chunk=>chunk.Features??new List<MapFeature>())
                .Where(feature=>feature.Kind=="town_person").Select(feature=>feature.RouteId).ToHashSet(StringComparer.Ordinal);
            using (var roadContext = roads.Open())
            using (var localRoadContext = localRoads.Open())
            using (var dirtRoadContext = dirtRoads.Open())
            using (var countyRoadContext = countyRoads.Open())
            using (var railContext = rails.Open())
            {
            foreach (var chunk in _populationChunks.Values)
            {
                if (!IsChunkVisible(chunk.ChunkX, chunk.ChunkY, halfY)) continue;
                foreach (var feature in chunk.Features)
                {
                    if (feature.Points == null || feature.Points.Count < 2) continue;
                    if(UseTrueScaleRoadWidths)
                    {
                        DrawRoadFeature(dc,feature,scale,mapRect,feature.Kind=="local_road"?"#c5b894":"#b56e35",.0065);
                        continue;
                    }
                    var context = feature.Kind == "local_road" ? localRoadContext : roadContext;
                    for (int i = 0; i + 1 < feature.Points.Count; i += 2)
                    {
                        context.BeginFigure(Map(feature.Points[i].X, feature.Points[i].Y, scale, mapRect), false, false);
                        context.LineTo(Map(feature.Points[i + 1].X, feature.Points[i + 1].Y, scale, mapRect), true, false);
                    }
                }
            }
            foreach (var chunk in _roadChunks.Values.Concat(_transportChunks.Values))
            {
                if (!IsChunkVisible(chunk.ChunkX, chunk.ChunkY, halfY)) continue;
                foreach (var feature in chunk.Features)
                {
                    if (feature.Points == null || feature.Points.Count < 2) continue;
                    if (feature.Kind == "local_road" && feature.Surface != "paved" && _viewSizeMiles > 48) continue;
                    if(UseTrueScaleRoadWidths&&feature.Kind!="rail")
                    {
                        var color=feature.Surface=="paved"?"#777a72":feature.Surface=="dirt"?"#9e805a":"#c5b894";
                        DrawRoadFeature(dc,feature,scale,mapRect,color,feature.Surface=="paved" ? .02 : feature.Surface=="dirt" ? .0042 : .0065);
                        continue;
                    }
                    var context = feature.Kind == "rail" ? railContext : feature.Surface == "paved" ? countyRoadContext : feature.Surface == "dirt" ? dirtRoadContext : localRoadContext;
                    for (int i = 0; i + 1 < feature.Points.Count; i += 2)
                    {
                        context.BeginFigure(Map(feature.Points[i].X, feature.Points[i].Y, scale, mapRect), false, false);
                        context.LineTo(Map(feature.Points[i + 1].X, feature.Points[i + 1].Y, scale, mapRect), true, false);
                    }
                }
            }
            }
            roads.Freeze(); localRoads.Freeze(); dirtRoads.Freeze(); countyRoads.Freeze(); rails.Freeze();
            dc.DrawGeometry(null, RoadPen("#c5b894", .5), localRoads);
            dc.DrawGeometry(null, RoadPen("#9e805a", .45), dirtRoads);
            dc.DrawGeometry(null, RoadPen("#777a72", 1.4), countyRoads);
            dc.DrawGeometry(null, RoadPen("#b56e35", 1.05), roads);
            dc.DrawGeometry(null, RailUnderlayPen, rails);
            dc.DrawGeometry(null, RailPen, rails);
            DrawRuralLots(dc, scale, mapRect, halfY);
            DrawTowns(dc, scale, mapRect, halfY);
            DrawTrees(dc, scale, mapRect, halfY);
            DrawWallPlans(dc, scale, mapRect, halfY);
            DrawRailwayOverlay(dc, scale, mapRect, halfY);
            foreach (var chunk in _waterChunks.Values)
            {
                if (!IsChunkVisible(chunk.ChunkX, chunk.ChunkY, halfY)) continue;
                foreach (var feature in chunk.Features)
                    if (feature.Kind == "rivers" && (_viewSizeMiles <= 12 || feature.StreamOrder >= 2)) DrawRiver(dc, feature, scale, mapRect);
            }
            foreach (var chunk in _populationChunks.Values.Select(c => new TransportChunk { ChunkX = c.ChunkX, ChunkY = c.ChunkY, Features = c.Features })
                .Concat(_roadChunks.Values).Concat(_transportChunks.Values))
            {
                if (!IsChunkVisible(chunk.ChunkX, chunk.ChunkY, halfY)) continue;
                foreach (var feature in chunk.Features)
                {
                    if (feature.Kind != "bridges" || feature.Points == null || feature.Points.Count < 2) continue;
                    var pen = feature.Carrier == "rail" ? RailBridgePen : RoadBridgePen;
                    for (int i = 0; i + 1 < feature.Points.Count; i += 2)
                        dc.DrawLine(pen, Map(feature.Points[i].X, feature.Points[i].Y, scale, mapRect),
                            Map(feature.Points[i + 1].X, feature.Points[i + 1].Y, scale, mapRect));
                }
            }
            foreach (var chunk in _populationChunks.Values)
            {
                if (!IsChunkVisible(chunk.ChunkX, chunk.ChunkY, halfY)) continue;
                foreach (var settlement in chunk.Features.Where(feature => feature.Kind == "settlement" && feature.Points != null))
                    foreach (var point in settlement.Points)
                        dc.DrawEllipse(Brush(settlement.IsMajorTown ? "#7c3f24" : "#b97948"), new Pen(Brush("#fff9e9"), 1),
                            Map(point.X, point.Y, scale, mapRect), settlement.IsMajorTown ? 6.5 : 4.5, settlement.IsMajorTown ? 6.5 : 4.5);
                foreach (var person in chunk.Settlers ?? new List<Settler>())
                {
                    if(townResidentIds.Contains(person.Id))continue;
                    Point point = Map(person.X, person.Y, scale, mapRect);
                    dc.DrawEllipse(Brush("#9b5b34"), null, point, 2.3, 2.3);
                    if (_viewSizeMiles <= 1.0)
                    {
                        var label = new FormattedText(person.Name, System.Globalization.CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Brush("#503d32"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        dc.DrawText(label, new Point(point.X + 4, point.Y - 7));
                    }
                }
            }
            // Town residents are drawn last so their dots sit over their assigned homes.
            foreach(var chunk in _townChunks.Values)
            {
                if(!IsChunkVisible(chunk.ChunkX,chunk.ChunkY,halfY))continue;
                foreach(var person in (chunk.Features??new List<MapFeature>()).Where(feature=>feature.Kind=="town_person"&&feature.Points?.Count>0))
                {
                    Point point=Map(person.Points[0].X,person.Points[0].Y,scale,mapRect);
                    dc.DrawEllipse(Brush("#9b5b34"),new Pen(Brush("#fff9e9"),.65),point,2.8,2.8);
                    if(_viewSizeMiles<=1.0&&!string.IsNullOrEmpty(person.Name))
                    {
                        var label=new FormattedText(person.Name,System.Globalization.CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,Brush("#503d32"),VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        dc.DrawText(label,new Point(point.X+4,point.Y-7));
                    }
                }
            }
            dc.Pop();
            renderWatch.Stop();
            _lastRenderMilliseconds = renderWatch.Elapsed.TotalMilliseconds;
            _maxRenderMilliseconds = Math.Max(_maxRenderMilliseconds, _lastRenderMilliseconds);
            _renderCount++;
        }

        private bool IsChunkVisible(int x, int y, double halfY) =>
            !(x + 1 < _centerX - _viewSizeMiles / 2 || x > _centerX + _viewSizeMiles / 2 ||
              y + 1 < _centerY - halfY || y > _centerY + halfY);

        private static (Geometry Regular, Geometry Waterline, Geometry Depth) BuildContourGeometry(ViewerScene chunk)
        {
            var regular = new StreamGeometry();
            var waterline = new StreamGeometry();
            var depth = new StreamGeometry();
            using (var regularContext = regular.Open())
            using (var waterContext = waterline.Open())
            using (var depthContext = depth.Open())
            foreach (var feature in chunk.Features ?? new List<MapFeature>())
            {
                if (feature.Points == null || feature.Points.Count < 2) continue;
                var context = feature.Waterline ? waterContext : feature.DepthFt.HasValue ? depthContext : regularContext;
                for (int i = 0; i + 1 < feature.Points.Count; i += 2)
                {
                    var a = feature.Points[i]; var b = feature.Points[i + 1];
                    context.BeginFigure(new Point(a.X, a.Y), false, false);
                    context.LineTo(new Point(b.X, b.Y), true, false);
                }
            }
            regular.Freeze(); waterline.Freeze(); depth.Freeze();
            return (regular, waterline, depth);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            VisibleChunksChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            Focus();
            Rect rect = ViewportRect();
            Point cursor = e.GetPosition(this);
            double oldScale = rect.Width / _viewSizeMiles;
            double anchorX = _centerX + (cursor.X - rect.Left - rect.Width / 2) / oldScale;
            double anchorY = _centerY + (cursor.Y - rect.Top - rect.Height / 2) / oldScale;
            _viewSizeMiles = Math.Max(MinViewMiles, Math.Min(MaxViewMiles, _viewSizeMiles / Math.Pow(1.2, e.Delta / 120.0)));
            rect = ViewportRect();
            double newScale = rect.Width / _viewSizeMiles;
            _centerX = anchorX - (cursor.X - rect.Left - rect.Width / 2) / newScale;
            _centerY = anchorY - (cursor.Y - rect.Top - rect.Height / 2) / newScale;
            PruneCache(); RequestRender();
            VisibleChunksChanged?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ChangedButton != MouseButton.Left) return;
            Focus(); _dragging = true; _dragStart = e.GetPosition(this);
            _dragCenterX = _centerX; _dragCenterY = _centerY;
            CaptureMouse(); Cursor = Cursors.SizeAll; e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging || e.LeftButton != MouseButtonState.Pressed) return;
            Point current = e.GetPosition(this); Rect rect = ViewportRect();
            double scale = rect.Width / _viewSizeMiles;
            _centerX = _dragCenterX - (current.X - _dragStart.X) / scale;
            _centerY = _dragCenterY - (current.Y - _dragStart.Y) / scale;
            RequestRender();
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.ChangedButton != MouseButton.Left || !_dragging) return;
            _dragging = false; ReleaseMouseCapture(); Cursor = Cursors.Hand;
            PruneCache(); RequestRender(); VisibleChunksChanged?.Invoke(this, EventArgs.Empty); e.Handled = true;
        }

        private List<ChunkCoordinate> GetVisibleChunkCoordinates()
        {
            double halfX = _viewSizeMiles / 2, halfY = ViewHeightMiles / 2;
            int minX = (int)Math.Floor(_centerX - halfX), minY = (int)Math.Floor(_centerY - halfY);
            int maxX = (int)Math.Ceiling(_centerX + halfX) - 1, maxY = (int)Math.Ceiling(_centerY + halfY) - 1;
            var list = new List<ChunkCoordinate>(Math.Max(0, (maxX - minX + 1) * (maxY - minY + 1)));
            for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++) list.Add(new ChunkCoordinate(x, y));
            return list;
        }

        private void PruneCache()
        {
            double halfX = _viewSizeMiles / 2 + ChunkCacheMargin, halfY = ViewHeightMiles / 2 + ChunkCacheMargin;
            foreach (var key in _chunks.Keys.ToArray())
                if (key.X + 1 < _centerX - halfX || key.X > _centerX + halfX || key.Y + 1 < _centerY - halfY || key.Y > _centerY + halfY)
                { _chunks.Remove(key); _waterChunks.Remove(key); _populationChunks.Remove(key); _transportChunks.Remove(key); _roadChunks.Remove(key); _ruralChunks.Remove(key); _townChunks.Remove(key); _treeChunks.Remove(key); _wallChunks.Remove(key); _elevationBitmaps.Remove(key); _contourGeometry.Remove(key); }
        }

        private void DrawRuralLots(DrawingContext dc, double scale, Rect mapRect, double halfY)
        {
            if (_viewSizeMiles > 12) return;
            foreach (var chunk in _ruralChunks.Values)
            {
                if (!IsChunkVisible(chunk.ChunkX, chunk.ChunkY, halfY)) continue;
                foreach (var feature in chunk.Features ?? new List<MapFeature>())
                {
                    if (feature.Points == null || feature.Points.Count == 0) continue;
                    if (feature.Kind == "rural_lot" && feature.Points.Count >= 4 && _viewSizeMiles <= 12)
                    {
                        var geometry = new StreamGeometry();
                        using (var context = geometry.Open())
                        {
                            context.BeginFigure(Map(feature.Points[0].X, feature.Points[0].Y, scale, mapRect), true, true);
                            for (int i = 1; i < feature.Points.Count; i++) context.LineTo(Map(feature.Points[i].X, feature.Points[i].Y, scale, mapRect), true, false);
                        }
                        geometry.Freeze();
                        dc.DrawGeometry(Brush("#eee9dc"), RuralLotPen, geometry);
                        if (_viewSizeMiles <= .28 && !string.IsNullOrEmpty(feature.Name))
                            DrawSmallLabel(dc, feature.Name, Map(feature.Points.Average(p => p.X), feature.Points.Average(p => p.Y), scale, mapRect), 8, "#50463d");
                    }
                    else if (feature.Kind == "rural_yard" && feature.Points.Count >= 4 && _viewSizeMiles <= 12)
                    {
                        DrawFeaturePolygon(dc, feature.Points, scale, mapRect, Brush("#d6cbb1"), null);
                    }
                    else if (feature.Kind == "rural_driveway" && feature.Points.Count >= 2 && _viewSizeMiles <= 12)
                    {
                        for(int i=0;i+1<feature.Points.Count;i+=2)
                            dc.DrawLine(RuralDrivewayPen,Map(feature.Points[i].X,feature.Points[i].Y,scale,mapRect),
                                Map(feature.Points[i+1].X,feature.Points[i+1].Y,scale,mapRect));
                    }
                    else if(feature.Kind=="rural_path"&&feature.Points.Count>=2&&_viewSizeMiles<=2)
                    {
                        var pathPen=new Pen(Brush(feature.Surface=="concrete"?"#b7b3a8":"#aa9778"),Math.Max(1.0,feature.Width*scale));
                        pathPen.Freeze();
                        for(int i=0;i+1<feature.Points.Count;i+=2)
                            dc.DrawLine(pathPen,Map(feature.Points[i].X,feature.Points[i].Y,scale,mapRect),Map(feature.Points[i+1].X,feature.Points[i+1].Y,scale,mapRect));
                    }
                    else if(feature.Kind=="field_entrance"&&feature.Points.Count>=2&&_viewSizeMiles<=12)
                    {
                        var pen=new Pen(Brush("#9e917c"),Math.Max(1.0,feature.Width*scale));pen.Freeze();
                        for(int i=0;i+1<feature.Points.Count;i+=2)
                            dc.DrawLine(pen,Map(feature.Points[i].X,feature.Points[i].Y,scale,mapRect),Map(feature.Points[i+1].X,feature.Points[i+1].Y,scale,mapRect));
                    }
                    else if(feature.Kind=="cattle_yard"&&feature.Points.Count>=4&&_viewSizeMiles<=8)
                    {
                        DrawFeaturePolygon(dc,feature.Points,scale,mapRect,Brush("#d4c3a2"),new Pen(Brush("#988566"),1));
                    }
                    else if(feature.Kind=="cattle_pen"&&feature.Points.Count>=4&&_viewSizeMiles<=4)
                    {
                        DrawFeaturePolygon(dc,feature.Points,scale,mapRect,Brush("#b9a27e"),new Pen(Brush("#897657"),1));
                    }
                    else if(feature.Kind=="cattle_feed_road"&&feature.Points.Count>=2&&_viewSizeMiles<=4)
                    {
                        var pen=new Pen(Brush("#aa9778"),Math.Max(1.0,feature.Width*scale));pen.Freeze();
                        for(int i=0;i+1<feature.Points.Count;i+=2)
                            dc.DrawLine(pen,Map(feature.Points[i].X,feature.Points[i].Y,scale,mapRect),Map(feature.Points[i+1].X,feature.Points[i+1].Y,scale,mapRect));
                    }
                    else if((feature.Kind=="cattle_office_shed"||feature.Kind=="rural_coop_elevator"||feature.Kind=="rural_coop_bin")&&feature.Points.Count>=4&&_viewSizeMiles<=3)
                    {
                        var color=feature.Kind=="rural_coop_elevator"?"#a76d43":feature.Kind=="rural_coop_bin"?"#c8b89e":"#8c8172";
                        DrawFeaturePolygon(dc,feature.Points,scale,mapRect,Brush(color),null);
                        if(_viewSizeMiles<=.8&&!string.IsNullOrEmpty(feature.Name))
                            DrawSmallLabel(dc,feature.Name,Map(feature.Points.Average(p=>p.X),feature.Points.Average(p=>p.Y),scale,mapRect),8,"#55351f");
                    }
                    else if (feature.Kind == "rural_building" && _viewSizeMiles <= 12)
                    {
                        Point p = Map(feature.Points.Average(q => q.X), feature.Points.Average(q => q.Y), scale, mapRect);
                        string color = feature.Name == "Farmhouse" ? "#8f7254" : feature.Name == "Grain Bin" ? "#9c9a8e" : feature.Name == "Existing gambrel barn" ? "#75634d" : "#8c8172";
                        if (feature.Points.Count >= 4 && _viewSizeMiles <= 12)
                            DrawFeaturePolygon(dc, feature.Points, scale, mapRect, Brush(color), null);
                        else
                        {
                            double radius = _viewSizeMiles <= .5 ? 3.0 : 2.0;
                            dc.DrawRectangle(Brush(color), null, new Rect(p.X - radius, p.Y - radius, radius * 2, radius * 2));
                        }
                        if (_viewSizeMiles <= .35 && !string.IsNullOrEmpty(feature.Name))
                        {
                            string buildingLabel=feature.Name=="Existing gambrel barn"?"Existing gambrel barn (pre-1990)":feature.Name;
                            var label = new FormattedText(buildingLabel, System.Globalization.CultureInfo.InvariantCulture,
                                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 9, Brush("#50463a"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
                            dc.DrawText(label, new Point(p.X + 4, p.Y - label.Height));
                        }
                    }
                }
            }
        }

        private void DrawTowns(DrawingContext dc, double scale, Rect mapRect, double halfY)
        {
            foreach (var chunk in _townChunks.Values)
            {
                if (!IsChunkVisible(chunk.ChunkX, chunk.ChunkY, halfY)) continue;
                foreach (var feature in chunk.Features ?? new List<MapFeature>())
                {
                    if (feature.Points == null || feature.Points.Count == 0) continue;
                    if (feature.Kind == "town" && feature.Points.Count >= 4)
                    {
                        // The generated town outline is used for placement and
                        // clearance only; drawing it made the town look boxed in.
                        if (_viewSizeMiles <= .3 && !string.IsNullOrEmpty(feature.Name))
                        {
                            double cx = feature.Points.Average(p => p.X), cy = feature.Points.Average(p => p.Y);
                            var label = new FormattedText(feature.Name, System.Globalization.CultureInfo.InvariantCulture,
                                FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 12, Brush("#513e32"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
                            Point center = Map(cx, cy, scale, mapRect);
                            dc.DrawText(label, new Point(center.X - label.Width / 2, center.Y - label.Height / 2 - 9));
                        }
                    }
                    else if (feature.Kind == "town_lot" && feature.Points.Count >= 4 && _viewSizeMiles <= 12)
                    {
                        var points = feature.Points.Select(p => Map(p.X, p.Y, scale, mapRect)).ToList();
                        var geometry = new StreamGeometry();
                        using (var context = geometry.Open())
                        {
                            context.BeginFigure(points[0], true, true);
                            for (int i = 1; i < points.Count; i++) context.LineTo(points[i], true, false);
                        }
                        geometry.Freeze();
                        dc.DrawGeometry(Brush("#e8e3d4"), TownLotPen, geometry);
                        if (_viewSizeMiles <= .22 && !string.IsNullOrEmpty(feature.Name))
                            DrawSmallLabel(dc, feature.Name, Map(feature.Points.Average(p => p.X), feature.Points.Average(p => p.Y), scale, mapRect), 8, "#50483d");
                    }
                    else if ((feature.Kind == "town_business_lot" || feature.Kind == "town_civic_lot") && feature.Points.Count >= 4 && _viewSizeMiles <= 12)
                    {
                        var points = feature.Points.Select(p => Map(p.X, p.Y, scale, mapRect)).ToList();
                        var geometry = new StreamGeometry();
                        using (var context = geometry.Open())
                        {
                            context.BeginFigure(points[0], true, true);
                            for (int i = 1; i < points.Count; i++) context.LineTo(points[i], true, false);
                        }
                        geometry.Freeze();
                        dc.DrawGeometry(Brush(feature.Kind=="town_civic_lot"?"#d8d8c8":"#e4c89d"), TownBusinessLotPen, geometry);
                    }
                    else if ((feature.Kind == "town_building" && _viewSizeMiles >= 1.5 ||
                              feature.Kind is "town_garage" or "town_business_aux" or "town_civic_building") && _viewSizeMiles <= 12)
                    {
                        Point p = Map(feature.Points.Average(q => q.X), feature.Points.Average(q => q.Y), scale, mapRect);
                        string color = feature.Kind == "town_garage" ? "#aaa094" : feature.Kind == "town_business_aux" ? "#b28a55" : feature.Kind == "town_civic_building" ? "#7a806e" : "#8a7c68";
                        if (feature.Points.Count >= 4)
                            DrawFeaturePolygon(dc, feature.Points, scale, mapRect, Brush(color), null);
                        else dc.DrawRectangle(Brush(color), null, new Rect(p.X - 2, p.Y - 2, 4, 4));
                    }
                    else if(feature.Kind=="town_civic_ground"&&feature.Points.Count>=4&&_viewSizeMiles<=12)
                    {
                        string color=feature.Name switch{"Ball Field"=>"#9ebc82","Cemetery"=>"#c6cdb3","Golf Course"=>"#b7cd9e","Fairgrounds"=>"#d5c5a0","Airstrip"=>"#c9c5b6","Central Square"=>"#a8c487",_=>"#d2cba9"};
                        DrawFeaturePolygon(dc,feature.Points,scale,mapRect,Brush(color),new Pen(Brush("#8e907b"),.7));
                    }
                    else if(feature.Kind=="town_civic_marker"&&feature.Points.Count>0)
                    {
                        Point p=Map(feature.Points[0].X,feature.Points[0].Y,scale,mapRect);
                        dc.DrawEllipse(Brush("#746d56"),null,p,3.2,3.2);
                        if(_viewSizeMiles<=.30)DrawSmallLabel(dc,feature.Name,p,8,"#514c3d");
                    }
                    else if(feature.Kind=="town_court_bulb"&&feature.Points.Count>0&&_viewSizeMiles<=3)
                    {
                        Point p=Map(feature.Points[0].X,feature.Points[0].Y,scale,mapRect);double radius=Math.Max(2,feature.Width*scale*.5);
                        dc.DrawEllipse(Brush("#f0e6d3"),TownStreetPen,p,radius,radius);
                    }
                    else if(feature.Kind=="town_median_tree"&&feature.Points.Count>0&&_viewSizeMiles<=2)
                    {
                        Point p=Map(feature.Points[0].X,feature.Points[0].Y,scale,mapRect);dc.DrawEllipse(Brush("#5d8052"),null,p,2.2,2.2);
                    }
                    else if (feature.Kind == "town_business" && _viewSizeMiles <= 12)
                    {
                        Point p = Map(feature.Points.Average(q => q.X), feature.Points.Average(q => q.Y), scale, mapRect);
                        if (feature.Points.Count >= 4)
                            DrawFeaturePolygon(dc, feature.Points, scale, mapRect, Brush("#a76d43"), null);
                        else dc.DrawRectangle(Brush("#9b5a32"), null, new Rect(p.X - 3, p.Y - 3, 6, 6));
                        if (_viewSizeMiles <= .20 && !string.IsNullOrEmpty(feature.Name)) DrawSmallLabel(dc, feature.Name, p, 9, "#55351f");
                    }
                    else if (feature.Kind == "town_driveway" && feature.Points.Count >= 2 && _viewSizeMiles <= 12)
                    {
                        for(int i=0;i+1<feature.Points.Count;i+=2)
                            dc.DrawLine(RuralDrivewayPen,Map(feature.Points[i].X,feature.Points[i].Y,scale,mapRect),Map(feature.Points[i+1].X,feature.Points[i+1].Y,scale,mapRect));
                    }
                    else if((feature.Kind=="town_sidewalk"||feature.Kind=="town_door_walk"||feature.Kind=="town_alley")&&feature.Points.Count>=2&&_viewSizeMiles<=12)
                    {
                        Pen pen=feature.Kind=="town_alley"?TownAlleyPen:feature.Kind=="town_sidewalk"?TownSidewalkPen:TownDoorWalkPen;
                        for(int i=0;i+1<feature.Points.Count;i+=2)dc.DrawLine(pen,Map(feature.Points[i].X,feature.Points[i].Y,scale,mapRect),Map(feature.Points[i+1].X,feature.Points[i+1].Y,scale,mapRect));
                    }
                    else if (feature.Kind == "town_depot")
                    {
                        Point p = Map(feature.Points[0].X, feature.Points[0].Y, scale, mapRect);
                        dc.DrawEllipse(Brush("#453c35"), new Pen(Brush("#fff9e9"), 1), p, 4.2, 4.2);
                        if (_viewSizeMiles <= 1.5) DrawSmallLabel(dc, "Depot", p, 9, "#453c35");
                    }
                    else if (feature.Kind == "town_rail_ground" && feature.Points.Count >= 4 && _viewSizeMiles <= 12)
                    {
                        DrawFeaturePolygon(dc, feature.Points, scale, mapRect, Brush("#d2c7ae"), new Pen(Brush("#9b8d73"), 1));
                    }
                    else if ((feature.Kind == "town_rail_elevator" || feature.Kind == "town_rail_bin") && _viewSizeMiles <= 1.5)
                    {
                        Point p = Map(feature.Points.Average(q => q.X), feature.Points.Average(q => q.Y), scale, mapRect);
                        if (feature.Points.Count >= 4)
                            DrawFeaturePolygon(dc, feature.Points, scale, mapRect, Brush(feature.Kind == "town_rail_elevator" ? "#a76d43" : "#c8b89e"), null);
                        else dc.DrawRectangle(Brush("#a76d43"), null, new Rect(p.X - 3, p.Y - 3, 6, 6));
                        if (feature.Kind == "town_rail_elevator" && _viewSizeMiles <= .8) DrawSmallLabel(dc, "Elevator", p, 9, "#55351f");
                    }
                    else if (feature.Kind == "town_street" && feature.Points.Count >= 2 && _viewSizeMiles <= 12)
                    {
                        if(UseTrueScaleRoadWidths)DrawRoadFeature(dc,feature,scale,mapRect,feature.Name=="Main Street"?"#b56e35":"#777a72",feature.Name=="Main Street" ? .014 : (feature.Width>0?feature.Width:.008));
                        else
                        {
                            var townRoadPen=feature.Name=="Main Street"?RoadPen("#b56e35",1.05):RoadPen("#777a72",.8);
                            for (int i = 0; i + 1 < feature.Points.Count; i += 2)
                                dc.DrawLine(townRoadPen, Map(feature.Points[i].X, feature.Points[i].Y, scale, mapRect),
                                    Map(feature.Points[i + 1].X, feature.Points[i + 1].Y, scale, mapRect));
                        }
                    }
                }
            }
            // Main Street overlays stay visible above the parcels and buildings,
            // while the saved through-route itself remains continuous.
            foreach (var chunk in _townChunks.Values.Where(chunk => IsChunkVisible(chunk.ChunkX, chunk.ChunkY, halfY)))
            foreach (var feature in (chunk.Features ?? new List<MapFeature>()).Where(feature =>
                feature.Kind == "town_street" && feature.Name == "Main Street" && feature.Points?.Count >= 2))
                for (int i = 0; i + 1 < feature.Points.Count; i += 2)
                    dc.DrawLine(RoadPen("#b56e35",1.05), Map(feature.Points[i].X, feature.Points[i].Y, scale, mapRect),
                        Map(feature.Points[i + 1].X, feature.Points[i + 1].Y, scale, mapRect));
            foreach (var chunk in _townChunks.Values.Where(chunk => IsChunkVisible(chunk.ChunkX, chunk.ChunkY, halfY)))
            foreach (var feature in (chunk.Features ?? new List<MapFeature>()).Where(feature =>
                (feature.Kind is "town_alley" or "town_sidewalk" or "town_door_walk") && feature.Points?.Count >= 2))
            {
                Pen pen=feature.Kind=="town_alley"?TownAlleyPen:feature.Kind=="town_sidewalk"?TownSidewalkPen:TownDoorWalkPen;
                for(int i=0;i+1<feature.Points.Count;i+=2)
                    dc.DrawLine(pen,Map(feature.Points[i].X,feature.Points[i].Y,scale,mapRect),Map(feature.Points[i+1].X,feature.Points[i+1].Y,scale,mapRect));
            }
        }

        private void DrawFeaturePolygon(DrawingContext dc, IReadOnlyList<MapPoint> points, double scale, Rect mapRect,
            Brush fill, Pen pen)
        {
            if (points == null || points.Count < 3) return;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(Map(points[0].X, points[0].Y, scale, mapRect), fill != null, true);
                for (int i = 1; i < points.Count; i++) context.LineTo(Map(points[i].X, points[i].Y, scale, mapRect), true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(fill, pen, geometry);
        }

        private void DrawTrees(DrawingContext dc, double scale, Rect mapRect, double halfY)
        {
            if (_viewSizeMiles > 1.5) return;
            foreach (var chunk in _treeChunks.Values.Where(c => IsChunkVisible(c.ChunkX, c.ChunkY, halfY)))
            foreach (var feature in chunk.Features ?? new List<MapFeature>())
            {
                if (feature.Kind != "tree_marker" || feature.Points?.Count == 0) continue;
                var p = Map(feature.Points[0].X, feature.Points[0].Y, scale, mapRect);
                double radius = Math.Max(.75, Math.Min(2.0, feature.Width * scale * .5));
                dc.DrawEllipse(TreeTrunkBrush, null, p, radius, radius);
            }
        }

        private void DrawWallPlans(DrawingContext dc, double scale, Rect mapRect, double halfY)
        {
            if (_viewSizeMiles >= 1.5) return;
            foreach (var chunk in _wallChunks.Values.Where(c => IsChunkVisible(c.ChunkX, c.ChunkY, halfY)))
            foreach (var plan in chunk.Plans ?? new List<WorldWallPlan>())
            foreach (var wall in plan.Walls ?? Array.Empty<WorldWall>())
            {
                var lengthFeet = Math.Sqrt(Math.Pow(wall.End.X-wall.Start.X, 2) + Math.Pow(wall.End.Z-wall.Start.Z, 2));
                if (lengthFeet < 1) continue;
                double cursor = 0;
                foreach (var opening in (wall.Openings ?? Array.Empty<WorldWallOpening>()).OrderBy(o => o.OffsetFeet))
                {
                    double start = Math.Clamp(opening.OffsetFeet-opening.WidthFeet*.5, cursor, lengthFeet);
                    if (start > cursor) DrawWallSpan(cursor, start);
                    cursor = Math.Clamp(opening.OffsetFeet+opening.WidthFeet*.5, cursor, lengthFeet);
                }
                if (cursor < lengthFeet) DrawWallSpan(cursor, lengthFeet);
                void DrawWallSpan(double from, double to)
                {
                    double a=from/lengthFeet,b=to/lengthFeet;
                    var p=new MapPoint{X=(wall.Start.X+(wall.End.X-wall.Start.X)*a)/5280.0,Y=(wall.Start.Z+(wall.End.Z-wall.Start.Z)*a)/5280.0};
                    var q=new MapPoint{X=(wall.Start.X+(wall.End.X-wall.Start.X)*b)/5280.0,Y=(wall.Start.Z+(wall.End.Z-wall.Start.Z)*b)/5280.0};
                    dc.DrawLine(HouseWallPen,Map(p.X,p.Y,scale,mapRect),Map(q.X,q.Y,scale,mapRect));
                }
            }
        }

        private void DrawRailwayOverlay(DrawingContext dc, double scale, Rect mapRect, double halfY)
        {
            foreach (var chunk in _transportChunks.Values.Where(chunk => IsChunkVisible(chunk.ChunkX, chunk.ChunkY, halfY)))
            foreach (var rail in chunk.Features.Where(feature => feature.Kind == "rail" && feature.Points?.Count >= 2))
            for (int i = 0; i + 1 < rail.Points.Count; i += 2)
            {
                Point a = Map(rail.Points[i].X, rail.Points[i].Y, scale, mapRect);
                Point b = Map(rail.Points[i + 1].X, rail.Points[i + 1].Y, scale, mapRect);
                dc.DrawLine(RailUnderlayPen, a, b);
                dc.DrawLine(RailPen, a, b);
                double dx = b.X - a.X, dy = b.Y - a.Y, length = Math.Sqrt(dx * dx + dy * dy);
                if (_viewSizeMiles > 24 || length < 3) continue;
                double halfTie = Math.Max(2.5, Math.Min(6, scale * .035));
                double px = -dy / length * halfTie, py = dx / length * halfTie;
                Point mid = new((a.X + b.X) * .5, (a.Y + b.Y) * .5);
                dc.DrawLine(RailTiePen, new Point(mid.X - px, mid.Y - py), new Point(mid.X + px, mid.Y + py));
            }
        }

        private void DrawSmallLabel(DrawingContext dc, string text, Point point, double size, string color)
        {
            var label = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), size, Brush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(label, new Point(point.X + 4, point.Y - label.Height - 2));
        }

        private static BitmapSource CreateElevationBitmap(ElevationGridData grid, double seaLevel)
        {
            const int size = 32;
            double pixelFt = 5280.0 / size;
            var heights = new double[size, size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                heights[y, x] = SampleGrid(grid, grid.OriginXFt + (x + .5) * pixelFt, grid.OriginYFt + (y + .5) * pixelFt);

            byte[] pixels = new byte[size * size * 4];
            var lights = new[] { (X: -.5, Y: -.5), (X: .5, Y: -.5), (X: -.5, Y: .5), (X: .5, Y: .5) };
            var low = (R: 176.0, G: 209.0, B: 172.0);
            var high = (R: 197.0, G: 217.0, B: 189.0);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                double left = heights[y, Math.Max(0, x - 1)], right = heights[y, Math.Min(size - 1, x + 1)];
                double top = heights[Math.Max(0, y - 1), x], bottom = heights[Math.Min(size - 1, y + 1), x];
                double gx = (right - left) / (2 * pixelFt) * 10;
                double gy = (bottom - top) / (2 * pixelFt) * 10;
                double normalLength = Math.Sqrt(gx * gx + gy * gy + 1);
                double illumination = 0;
                foreach (var light in lights)
                    illumination += Math.Max(0, (-gx * light.X - gy * light.Y + 1) / (normalLength * Math.Sqrt(2)));
                illumination /= lights.Length;
                double shade = .72 + .48 * illumination;
                double tint = Math.Max(0, Math.Min(1, (heights[y, x] - seaLevel - 35) / 125));
                double r = (low.R + (high.R - low.R) * tint) * shade;
                double g = (low.G + (high.G - low.G) * tint) * shade;
                double b = (low.B + (high.B - low.B) * tint) * shade;
                int index = (y * size + x) * 4;
                pixels[index] = (byte)Math.Max(0, Math.Min(255, b));
                pixels[index + 1] = (byte)Math.Max(0, Math.Min(255, g));
                pixels[index + 2] = (byte)Math.Max(0, Math.Min(255, r));
                pixels[index + 3] = 255;
            }
            var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
            bitmap.Freeze();
            return bitmap;
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

        private Rect ViewportRect()
        {
            return new Rect(RenderSize);
        }
        private Point Map(double x, double y, double scale, Rect rect) =>
            new Point(rect.Left + rect.Width / 2 + (x - _centerX) * scale, rect.Top + rect.Height / 2 + (y - _centerY) * scale);

        // Typed library coordinates are feet (X east, Z north); the viewport is
        // intentionally miles-based, so convert only at this drawing boundary.
        private Point Map(WorldPosition position, double scale, Rect rect) =>
            Map(position.X / 5280.0, position.Z / 5280.0, scale, rect);

        private void AddLake(StreamGeometryContext context, MapFeature feature, double scale, Rect rect)
        {
            if (feature.Points == null || feature.Points.Count < 3) return;
            context.BeginFigure(Map(feature.Points[0].X, feature.Points[0].Y, scale, rect), true, true);
            for (int i = 1; i < feature.Points.Count; i++) context.LineTo(Map(feature.Points[i].X, feature.Points[i].Y, scale, rect), true, false);
        }

        private void DrawRiver(DrawingContext dc, MapFeature feature, double scale, Rect rect)
        {
            if (feature.Points == null || feature.Points.Count < 2) return;
            var pen = new Pen(Brush("#72b6c5"), Math.Max(1.1, feature.Width * scale))
                { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            pen.Freeze();
            for (int i = 0; i + 1 < feature.Points.Count; i += 2)
                dc.DrawLine(pen, Map(feature.Points[i].X, feature.Points[i].Y, scale, rect), Map(feature.Points[i + 1].X, feature.Points[i + 1].Y, scale, rect));
        }

        private static readonly Pen MainRoadPen = CreatePen("#b56e35", 1.05);
        private static readonly Pen LocalRoadPen = CreatePen("#c5b894", .5);
        private static readonly Pen DirtRoadPen = CreatePen("#9e805a", .45);
        private static readonly Pen CountyRoadPen = CreatePen("#777a72", 1.4);
        private static readonly Pen TownOutlinePen = CreatePen("#b7aa8f", .9);
        private static readonly Pen TownLotPen = CreatePen("#c4bca9", .55);
        private static readonly Pen TownBusinessLotPen = CreatePen("#b68b5d", .8);
        private static readonly Pen MainStreetPen = CreatePen("#a87550", .8);
        private static readonly Pen RuralLotPen = CreatePen("#bcb69f", .45);
        private static readonly Pen RuralDrivewayPen = CreatePen("#aa9a7f", .275);
        private static readonly Pen TownStreetPen = CreatePen("#b88f66", .55);
        private static readonly Pen TownAlleyPen = CreatePen("#9d9078", .8);
        private static readonly Pen TownSidewalkPen = CreatePen("#d1c8b7", .75);
        private static readonly Pen TownDoorWalkPen = CreatePen("#c9c0b0", .65);
        private static readonly Pen RailUnderlayPen = CreatePen("#f4ead7", 2.2);
        private static readonly Pen RailPen = CreateRailPen();
        private static readonly Pen RailTiePen = CreatePen("#3a2b2b", 2.0);
        private static readonly Pen RoadBridgePen = CreatePen("#77563f", 1.7);
        private static readonly Pen RailBridgePen = CreatePen("#332f2c", 2.2);
        private static readonly Pen HouseWallPen = CreatePen("#604d3b", 1.15);
        private static readonly Brush TreeTrunkBrush = Brush("#425b37");
        private static Pen CreatePen(string color, double width)
        { var pen = new Pen(Brush(color), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; pen.Freeze(); return pen; }
        private Pen RoadPen(string color,double baseWidth)
        {
            // Geometry is in screen coordinates, so vary stroke width directly
            // with zoom. Visibility thresholds remain at each layer's call site.
            double factor=_viewSizeMiles>48?.70:_viewSizeMiles>12?.82:_viewSizeMiles>3?1.0:
                _viewSizeMiles>1.5?1.15:_viewSizeMiles>.5?1.35:1.6;
            return CreatePen(color,baseWidth*factor);
        }
        // Keep physical road widths through the Near and Mid map range.
        private bool UseTrueScaleRoadWidths => _viewSizeMiles <= 12;
        private void DrawRoadFeature(DrawingContext dc,MapFeature feature,double scale,Rect mapRect,string color,double fallbackWidth)
        {
            double roadWidth=feature.Width>0?feature.Width:fallbackWidth;
            var pen=CreatePen(color,roadWidth*scale);
            for(int i=0;i+1<feature.Points.Count;i+=2)
                dc.DrawLine(pen,Map(feature.Points[i].X,feature.Points[i].Y,scale,mapRect),Map(feature.Points[i+1].X,feature.Points[i+1].Y,scale,mapRect));
        }
        private static Pen CreateDepthPen(double width = .75)
        { var pen = new Pen(Brush("#cfdbd9"), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, DashStyle = DashStyles.Dash }; pen.Freeze(); return pen; }
        private static Pen CreateRailPen()
        { var pen = new Pen(Brush("#9b2747"), 2.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; pen.Freeze(); return pen; }
        private static SolidColorBrush Brush(string value)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            brush.Freeze(); return brush;
        }
    }
}


