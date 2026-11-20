using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CsharpSpawner
{
    // Adds the later section-road grid after population routes have been saved.
    public static class RoadSpawner
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { IncludeFields = true };

        /// <summary>In-process deterministic section-road generation for one chunk.</summary>
        public static TransportChunk GenerateChunk(int seed, TownInput input, IReadOnlyList<MapFeature> waterFeatures)
            => GenerateChunk(seed,input,waterFeatures,Array.Empty<MapFeature>());

        /// <summary>In-process section-road generation, joining orphaned main-road ends to the local network where safe.</summary>
        public static TransportChunk GenerateChunk(int seed, TownInput input, IReadOnlyList<MapFeature> waterFeatures,
            IReadOnlyList<MapFeature> mainRoadFeatures)
        {
            var chunk = new TransportChunk { ChunkX = input.ChunkX, ChunkY = input.ChunkY };
            AddSectionRoad(seed, chunk, "ew", input.ChunkY, input.ChunkX, waterFeatures, input.TerrainGrid);
            AddSectionRoad(seed, chunk, "ns", input.ChunkX, input.ChunkY, waterFeatures, input.TerrainGrid);
            EnforceRoadHierarchy(chunk, mainRoadFeatures);
            ConnectMainRoadEnds(chunk,seed,mainRoadFeatures,waterFeatures);
            return chunk;
        }

        // Remove lower-class routes that shadow a higher-class route within a
        // quarter mile, and direct their traffic onto the higher-class road.
        private static void EnforceRoadHierarchy(TransportChunk chunk, IReadOnlyList<MapFeature> mainRoads)
        {
            var mainSegments = (mainRoads ?? Array.Empty<MapFeature>())
                .Where(f => f.Kind == "main_road" && f.Points?.Count >= 2)
                .SelectMany(f => Enumerable.Range(0, f.Points.Count / 2)
                    .Select(i => (A: f.Points[i * 2], B: f.Points[i * 2 + 1])))
                .ToList();
            var countyRoads = chunk.Features.Where(f => f.Kind == "local_road" && f.Surface == "paved" &&
                f.RouteId?.StartsWith("section:", StringComparison.Ordinal) == true).ToList();

            foreach (var county in countyRoads)
                RedirectIfShadowing(chunk, county, mainSegments, "county-major-connector");

            var countySegments = chunk.Features.Where(f => f.Kind == "local_road" && f.Surface == "paved" &&
                f.RouteId?.StartsWith("section:", StringComparison.Ordinal) == true)
                .SelectMany(f => Enumerable.Range(0, f.Points.Count / 2)
                    .Select(i => (A: f.Points[i * 2], B: f.Points[i * 2 + 1])))
                .ToList();
            var higherThanGravel = mainSegments.Concat(countySegments).ToList();
            foreach (var gravel in chunk.Features.Where(f => f.Kind == "local_road" && f.Surface != "paved" &&
                f.RouteId?.StartsWith("section:", StringComparison.Ordinal) == true).ToList())
                RedirectIfShadowing(chunk, gravel, higherThanGravel, "gravel-higher-road-connector");
        }

        private static void RedirectIfShadowing(TransportChunk chunk, MapFeature road,
            IReadOnlyList<(MapPoint A, MapPoint B)> higherRoads, string connectorPrefix)
        {
            if (road.Points?.Count < 2 || higherRoads.Count == 0) return;
            var a = road.Points[0]; var b = road.Points[1];
            double dx = b.X - a.X, dy = b.Y - a.Y, length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-9) return;
            var midpoint = (X: (a.X + b.X) * .5, Y: (a.Y + b.Y) * .5);
            (MapPoint Point, double Distance, bool Parallel)? nearest = null;
            foreach (var candidate in higherRoads)
            {
                double hx = candidate.B.X - candidate.A.X, hy = candidate.B.Y - candidate.A.Y;
                double hLength = Math.Sqrt(hx * hx + hy * hy);
                if (hLength < 1e-9) continue;
                bool parallel = Math.Abs((dx * hx + dy * hy) / (length * hLength)) >= .88;
                // Parallel roads only fold into the higher route when the full
                // lower segment stays close. Other nearby roads keep their route
                // and receive a direct link to the higher road.
                if (parallel && (DistanceToSegment((a.X, a.Y), (candidate.A.X, candidate.A.Y), (candidate.B.X, candidate.B.Y)) > .25 ||
                    DistanceToSegment((b.X, b.Y), (candidate.A.X, candidate.A.Y), (candidate.B.X, candidate.B.Y)) > .25)) continue;
                double t = Math.Clamp(((midpoint.X - candidate.A.X) * hx + (midpoint.Y - candidate.A.Y) * hy) / (hLength * hLength), 0, 1);
                var point = new MapPoint { X = candidate.A.X + t * hx, Y = candidate.A.Y + t * hy };
                double distance = Distance(midpoint, (point.X, point.Y));
                if (distance < .0025 || distance > .25 || nearest != null && distance >= nearest.Value.Distance) continue;
                nearest = (point, distance, parallel);
            }
            if (nearest == null) return;

            if (nearest.Value.Parallel) chunk.Features.Remove(road);
            chunk.Features.Add(new MapFeature
            {
                Kind = "local_road", RouteId = $"{connectorPrefix}:{road.RouteId}", Carrier = road.RouteId,
                Surface = road.Surface, Width = road.Width,
                Points = new List<MapPoint> { new() { X = midpoint.X, Y = midpoint.Y }, nearest.Value.Point }
            });
        }

        public static IReadOnlyList<WorldFeature> GenerateWorldFeatures(int seed, TownInput input, IReadOnlyList<MapFeature> waterFeatures) =>
            LegacyMapFeatureConverter.ToWorldFeatures(GenerateChunk(seed,input,waterFeatures).Features);

        public static async Task<int> RunWorkerAsync()
        {
            try
            {
                var request = JsonSerializer.Deserialize<RoadRequest>(await Console.In.ReadToEndAsync().ConfigureAwait(false), JsonOptions)
                    ?? throw new InvalidOperationException("Missing road worker request.");
                using var stdout = new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                var waterIndex = new FeatureSpatialIndex(request.WaterFeatures);
                foreach (var input in request.Chunks ?? new List<TownInput>())
                {
                    var localWater = waterIndex.QueryChunk(input.ChunkX,input.ChunkY,5);
                    var chunk = GenerateChunk(request.Seed,input,localWater,request.MainRoadFeatures);
                    await stdout.WriteLineAsync(JsonSerializer.Serialize(chunk, JsonOptions)).ConfigureAwait(false);
                }
                return 0;
            }
            catch (Exception ex) { await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false); return 1; }
        }

        private static void ConnectMainRoadEnds(TransportChunk output,int seed,IReadOnlyList<MapFeature> mainRoads,
            IReadOnlyList<MapFeature> water)
        {
            var segments=(mainRoads??Array.Empty<MapFeature>()).Where(f=>f.Kind=="main_road"&&f.Points?.Count>=2)
                .SelectMany(f=>Enumerable.Range(0,f.Points.Count/2).Select(i=>(Feature:f,A:f.Points[i*2],B:f.Points[i*2+1])))
                .ToList();
            var degrees=new Dictionary<(long X,long Y),int>();
            (long X,long Y) Key(MapPoint p)=>((long)Math.Round(p.X*100000),(long)Math.Round(p.Y*100000));
            foreach(var segment in segments){var a=Key(segment.A);var b=Key(segment.B);degrees[a]=degrees.GetValueOrDefault(a)+1;degrees[b]=degrees.GetValueOrDefault(b)+1;}
            var endpoints=segments.SelectMany(s=>new[]{(Feature:s.Feature,Point:s.A),(Feature:s.Feature,Point:s.B)})
                .GroupBy(e=>Key(e.Point)).Select(g=>g.First()).Where(e=>degrees.GetValueOrDefault(Key(e.Point))==1)
                .Where(e=>(int)Math.Floor(e.Point.X)==output.ChunkX&&(int)Math.Floor(e.Point.Y)==output.ChunkY).ToList();
            foreach(var endpoint in endpoints)
            {
                var origin=(X:endpoint.Point.X,Y:endpoint.Point.Y);
                if(output.Features.Where(f=>f.Kind=="local_road"&&f.RouteId?.StartsWith("section:",StringComparison.Ordinal)==true)
                    .Any(road=>PolylineDistance(origin,road.Points)<=.0025))continue;
                (MapFeature Road,MapPoint Point,double Distance)? nearest=null;
                foreach(var road in output.Features.Where(f=>f.Kind=="local_road"&&f.RouteId?.StartsWith("section:",StringComparison.Ordinal)==true))
                for(int i=0;i+1<road.Points.Count;i+=2)
                {
                    var a=road.Points[i];var b=road.Points[i+1];double dx=b.X-a.X,dy=b.Y-a.Y;
                    double t=Math.Clamp(((origin.X-a.X)*dx+(origin.Y-a.Y)*dy)/Math.Max(1e-20,dx*dx+dy*dy),0,1);
                    var p=new MapPoint{X=a.X+t*dx,Y=a.Y+t*dy};double distance=Distance(origin,(p.X,p.Y));
                    if(distance<.02||distance>.35||nearest!=null&&distance>=nearest.Value.Distance)continue;
                    if((water??Array.Empty<MapFeature>()).Where(f=>f.Kind=="lakes"&&f.Points?.Count>=3).Any(lake=>
                        PointInPolygon(lake.Points,origin.X,origin.Y)||PointInPolygon(lake.Points,p.X,p.Y)))continue;
                    nearest=(road,p,distance);
                }
                if(nearest==null)continue;
                var target=nearest.Value;
                var connector=new MapFeature{Kind="local_road",RouteId=$"main-road-connector:{endpoint.Feature.RouteId}:{Key(endpoint.Point).X}:{Key(endpoint.Point).Y}",
                    Carrier=endpoint.Feature.RouteId,Surface="paved",Width=.0065,
                    Points=new List<MapPoint>{new(){X=origin.X,Y=origin.Y},target.Point}};
                output.Features.Add(connector);
                foreach(var hit in FindRiverCrossings(origin,(target.Point.X,target.Point.Y),water,false)
                    .Where(hit=>hit.Water.StreamOrder<2))
                    AddBridge(output,connector,hit.Water,hit.Point);
            }
        }

        private static double PolylineDistance((double X,double Y) point,IReadOnlyList<MapPoint> points)
        {double best=double.PositiveInfinity;if(points==null)return best;for(int i=0;i+1<points.Count;i+=2){var a=points[i];var b=points[i+1];double dx=b.X-a.X,dy=b.Y-a.Y;double t=Math.Clamp(((point.X-a.X)*dx+(point.Y-a.Y)*dy)/Math.Max(1e-20,dx*dx+dy*dy),0,1);best=Math.Min(best,Distance(point,(a.X+t*dx,a.Y+t*dy)));}return best;}
        private static bool PointInPolygon(IReadOnlyList<MapPoint> p,double x,double y)
        {bool inside=false;for(int i=0,j=p.Count-1;i<p.Count;j=i++)if((p[i].Y>y)!=(p[j].Y>y)&&x<(p[j].X-p[i].X)*(y-p[i].Y)/(p[j].Y-p[i].Y)+p[i].X)inside=!inside;return inside;}

        private static void AddSectionRoad(int seed, TransportChunk chunk, string axis, int line, int segment,
            IReadOnlyList<MapFeature> waterFeatures, ElevationGridData terrain)
        {
            bool horizontal = axis == "ew";
            bool county = IsCountyRoute(line);
            string surface = county ? "paved" : (Hash(seed, $"surface:{axis}:{line}:{segment}") % 100 < 5 ? "dirt" : "gravel");
            double x0 = horizontal ? segment : line;
            double y0 = horizontal ? line : segment;
            double x1 = horizontal ? segment + 1 : line;
            double y1 = horizontal ? line : segment + 1;

            // County routes keep their rectangular shape, but selected survey
            // lines sit slightly off the nominal mile grid for long distances.
            double countyOffset = county ? CountyRoadOffset(seed, axis, line) : 0;
            if (horizontal) { y0 += countyOffset; y1 += countyOffset; }
            else { x0 += countyOffset; x1 += countyOffset; }

            // Rare survey corrections gently move every N-S section line by a few
            // hundred feet at a 24-mile correction line, with a short connecting jog.
            if (!horizontal && !county)
            {
                long band = (long)Math.Floor(segment / 24.0);
                long nextBand = (long)Math.Floor((segment + 1) / 24.0);
                x0 += GridCorrection(seed, band);
                x1 += GridCorrection(seed, nextBand);
            }

            var start = (X: x0, Y: y0); var end = (X: x1, Y: y1);
            var majorHits = FindRiverCrossings(start, end, waterFeatures, majorOnly: true);
            // A major river interrupts the gravel grid. Only the paved county
            // routes get a bridge; small creek crossings remain bridgeable.
            if (!county && majorHits.Count > 0)
            {
                AddRiverBankLinks(chunk, seed, axis, line, segment, start, end, majorHits[0], waterFeatures);
                return;
            }
            // Terrain cutouts are data-driven. County routes tolerate steeper
            // ground; gravel/dirt sections stop at broad ridges and steep slopes.
            if (!county && IsRoughSegment(terrain, start, end)) return;

            var road = new MapFeature
            {
                Kind = "local_road", RouteId = $"section:{axis}:{line}:{segment}", Surface = surface,
                Width = surface == "paved" ? .0065 : .0042,
                Points = new List<MapPoint> { new MapPoint { X = x0, Y = y0 }, new MapPoint { X = x1, Y = y1 } }
            };
            chunk.Features.Add(road);
            foreach (var crossing in FindRiverCrossings(start, end, waterFeatures, majorOnly: false)
                .Where(hit => county || hit.Water.StreamOrder < 2))
                AddBridge(chunk, road, crossing.Water, crossing.Point);
        }

        private static bool IsCountyRoute(int line) => ((line % 12) + 12) % 12 == 1;

        /// <summary>Seeded, route-wide county survey offset in miles.</summary>
        public static double CountyRoadOffset(int seed, string axis, int line)
        {
            if (Hash(seed, $"county-offset:{axis}:{line}") % 100 >= 38) return 0;
            double magnitude = .10 + (Hash(seed, $"county-offset-size:{axis}:{line}") % 151) / 1000.0;
            return (Hash(seed, $"county-offset-side:{axis}:{line}") % 2 == 0 ? -1 : 1) * magnitude;
        }

        private static double GridCorrection(int seed, long band)
        {
            if (Hash(seed, $"rare-correction:{band}") % 16 != 0) return 0;
            return (Hash(seed, $"correction-side:{band}") % 2 == 0 ? -1 : 1) * .06;
        }

        private static bool IsRoughSegment(ElevationGridData terrain, (double X, double Y) a, (double X, double Y) b)
        {
            if (terrain?.Values == null || terrain.Values.Count < 2) return false;
            double min = double.PositiveInfinity, max = double.NegativeInfinity, cumulative = 0, previous = 0;
            for (int i = 0; i <= 8; i++)
            {
                double t = i / 8.0;
                double elevation = TransportSpawner.SampleElevation(terrain, a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
                if (!double.IsFinite(elevation)) continue;
                min = Math.Min(min, elevation); max = Math.Max(max, elevation);
                if (i > 0) cumulative += Math.Abs(elevation - previous);
                previous = elevation;
            }
            return max - min > 105 || cumulative > 150;
        }

        private static ulong Hash(int seed, string text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (char c in text) { hash ^= c; hash *= 1099511628211UL; }
                hash ^= (uint)seed; hash *= 1099511628211UL; hash ^= hash >> 32;
                return hash;
            }
        }

        private static List<(MapFeature Water, (double X, double Y) Point)> FindRiverCrossings(
            (double X, double Y) a, (double X, double Y) b, IReadOnlyList<MapFeature> waterFeatures, bool majorOnly)
        {
            var hits = new List<(MapFeature Water, (double X, double Y) Point)>();
            foreach (var water in waterFeatures ?? Array.Empty<MapFeature>())
            {
                if (water.Kind != "rivers" || water.Points == null || (majorOnly && water.StreamOrder < 2)) continue;
                for (int i = 0; i + 1 < water.Points.Count; i += 2)
                {
                    var c = (water.Points[i].X, water.Points[i].Y); var d = (water.Points[i + 1].X, water.Points[i + 1].Y);
                    if (!TryIntersection(a, b, c, d, out var crossing)) continue;
                    if (!hits.Any(hit => Math.Abs(hit.Point.X - crossing.X) < .025 && Math.Abs(hit.Point.Y - crossing.Y) < .025)) hits.Add((water, crossing));
                }
            }
            return hits;
        }

        private static void AddBridge(TransportChunk output, MapFeature road, MapFeature water, (double X, double Y) crossing)
        {
                    var a = (road.Points[0].X, road.Points[0].Y); var b = (road.Points[1].X, road.Points[1].Y);
                    double dx = b.X - a.X, dy = b.Y - a.Y, length = Math.Sqrt(dx * dx + dy * dy);
                    if (length < 1e-9) return;
                    double half = Math.Max(.035, water.Width * .5 + .02);
                    if (output.Features.Any(f => f.Kind == "bridges" && f.RouteId == road.RouteId && f.Points.Count > 0 &&
                        Math.Abs(f.Points[0].X - crossing.X) < .03 && Math.Abs(f.Points[0].Y - crossing.Y) < .03)) return;
                    output.Features.Add(new MapFeature
                    {
                        Kind = "bridges", RouteId = road.RouteId, Carrier = "local_road",
                        Points = new List<MapPoint>
                        {
                            new MapPoint { X = crossing.X - dx / length * half, Y = crossing.Y - dy / length * half },
                            new MapPoint { X = crossing.X + dx / length * half, Y = crossing.Y + dy / length * half }
                        }
                    });
        }

        private static void AddRiverBankLinks(TransportChunk output, int seed, string axis, int line, int segment,
            (double X, double Y) a, (double X, double Y) b,
            (MapFeature Water, (double X, double Y) Point) hit, IReadOnlyList<MapFeature> waterFeatures)
        {
            var water = hit.Water;
            if (water.Points == null || water.Points.Count < 2) return;
            // Find the nearest county-road crossing of this same major river
            // within six miles. The gravel approaches then run along the bank
            // and meet the paved bridge instead of stopping at the water.
            (double X, double Y)? target = null; double best = 6.01;
            for (int i = 0; i + 1 < water.Points.Count; i += 2)
            {
                var c = (water.Points[i].X, water.Points[i].Y); var d = (water.Points[i + 1].X, water.Points[i + 1].Y);
                foreach (int countyLine in CountyLinesNear(c.X, c.Y, d.X, d.Y))
                {
                    // Match the actual offset county-road centerlines: N-S roads
                    // shift in X and E-W roads shift in Y. The county-line
                    // selection rule itself remains unchanged.
                    double northSouthX=countyLine+CountyRoadOffset(seed,"ns",countyLine);
                    double eastWestY=countyLine+CountyRoadOffset(seed,"ew",countyLine);
                    var r0 = (X: northSouthX, Y: Math.Floor(Math.Min(c.Y, d.Y)) - 1);
                    var r1 = (X: northSouthX, Y: Math.Ceiling(Math.Max(c.Y, d.Y)) + 1);
                    var s0 = (X: Math.Floor(Math.Min(c.X, d.X)) - 1, Y: eastWestY);
                    var s1 = (X: Math.Ceiling(Math.Max(c.X, d.X)) + 1, Y: eastWestY);
                    if (TryIntersection(c, d, r0, r1, out var p) || TryIntersection(c, d, s0, s1, out p))
                    {
                        double distance = Distance(p, hit.Point);
                        if (distance > .08 && distance < best) { best = distance; target = p; }
                    }
                }
            }
            if (target == null) return;
            // Approximate the local river tangent and construct bank-parallel
            // approaches on either side of it. Stable IDs prevent duplicates.
            var segmentPair = Enumerable.Range(0, water.Points.Count / 2)
                .Select(i => (A: water.Points[i * 2], B: water.Points[i * 2 + 1]))
                .OrderBy(pair => DistanceToSegment(hit.Point, (pair.A.X, pair.A.Y), (pair.B.X, pair.B.Y))).First();
            double tx = segmentPair.B.X - segmentPair.A.X, ty = segmentPair.B.Y - segmentPair.A.Y;
            double length = Math.Sqrt(tx * tx + ty * ty); if (length < 1e-8) return;
            double nx = -ty / length, ny = tx / length, bank = Math.Max(.02, water.Width * .5 + .025);
            foreach (var endpoint in new[] { a, b })
            {
                double side = ((endpoint.X - hit.Point.X) * nx + (endpoint.Y - hit.Point.Y) * ny) >= 0 ? 1 : -1;
                var near = (X: hit.Point.X + nx * side * bank, Y: hit.Point.Y + ny * side * bank);
                var far = (X: target.Value.X + nx * side * bank, Y: target.Value.Y + ny * side * bank);
                output.Features.Add(new MapFeature
                {
                    Kind = "local_road", Surface = "gravel", RouteId = $"bank-link:{axis}:{line}:{segment}:{(side > 0 ? 1 : -1)}",
                    Width = .0042, Points = new List<MapPoint> { new MapPoint { X = endpoint.X, Y = endpoint.Y }, new MapPoint { X = near.X, Y = near.Y }, new MapPoint { X = far.X, Y = far.Y } }
                });
            }
        }

        private static IEnumerable<int> CountyLinesNear(double x0, double y0, double x1, double y1)
        {
            int lo = (int)Math.Floor(Math.Min(Math.Min(x0, x1), Math.Min(y0, y1))) - 2;
            int hi = (int)Math.Ceiling(Math.Max(Math.Max(x0, x1), Math.Max(y0, y1))) + 2;
            for (int n = lo; n <= hi; n++) if (IsCountyRoute(n)) yield return n;
        }

        private static double Distance((double X, double Y) a, (double X, double Y) b)
        { double dx = a.X - b.X, dy = a.Y - b.Y; return Math.Sqrt(dx * dx + dy * dy); }

        private static double DistanceToSegment((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, den = dx * dx + dy * dy;
            double t = den < 1e-12 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / den, 0, 1);
            return Distance(p, (a.X + t * dx, a.Y + t * dy));
        }

        private static bool TryIntersection((double X, double Y) a, (double X, double Y) b,
            (double X, double Y) c, (double X, double Y) d, out (double X, double Y) point)
        {
            double rx = b.X - a.X, ry = b.Y - a.Y, sx = d.X - c.X, sy = d.Y - c.Y;
            double denominator = rx * sy - ry * sx;
            if (Math.Abs(denominator) < 1e-12) { point = default; return false; }
            double qx = c.X - a.X, qy = c.Y - a.Y;
            double t = (qx * sy - qy * sx) / denominator, u = (qx * ry - qy * rx) / denominator;
            if (t < -1e-9 || t > 1 + 1e-9 || u < -1e-9 || u > 1 + 1e-9) { point = default; return false; }
            point = (a.X + t * rx, a.Y + t * ry);
            return true;
        }

        private static async Task StopWorkerAsync(Process process)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            try { await process.WaitForExitAsync().ConfigureAwait(false); } catch { }
        }
    }
}

