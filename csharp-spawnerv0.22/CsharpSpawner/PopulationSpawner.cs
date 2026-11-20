using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CsharpSpawner
{
    // Deterministically grows people and their first east-west roads from saved terrain and water.
    public static class PopulationSpawner
    {
        private const double FeetPerMile = 5280.0;
        private static readonly string[] GivenNames =
        {
            "Jan", "Willem", "Hendrik", "Pieter", "Cornelius", "Isaac", "Jacob", "Anna", "Maria", "Johanna",
            "Elizabeth", "John", "William", "Thomas", "Mary", "Sarah", "Ellen", "Henry", "Abraham", "Martha",
            "Dirk", "Klaas", "Gerrit", "Adriaan", "Margaret", "Rebecca", "Emily", "George", "Samuel", "Catherine"
        };
        private static readonly string[] Surnames =
        {
            "van der Meer", "de Vries", "van Houten", "Dijkstra", "Jansen", "Bakker", "Bos", "Visser", "Miller", "Baker",
            "Smith", "Johnson", "Thompson", "Cooper", "Carter", "Brown", "Wilson", "Davis", "Clark", "Taylor",
            "de Jong", "Mulder", "de Boer", "Smit", "Anderson", "Moore", "Martin", "Jackson", "White", "Harris"
        };
        // 80 generic place-name stems. The second list supplies 40 generic
        // geographic endings; these are assigned at the settlement stage.
        private static readonly string[] SettlementFirstStems = {
            "Amber","Ash","Aspen","Autumn","Bay","Birch","Blue","Briar","Bright","Brook",
            "Canyon","Cedar","Clear","Clover","Copper","Cotton","Cricket","Dawn","Deer","Dove",
            "Eagle","East","Elm","Evergreen","Fair","Fern","Field","Forest","Fox","Garden",
            "Golden","Grand","Green","Harmony","Hickory","Hidden","Iron","Ivory","Juniper","Lark",
            "Laurel","Little","Maple","Meadow","Mill","Misty","Morning","New","North","Oak",
            "Olive","Pine","Prairie","Red","River","Rolling","Rose","Sage","Silver","South",
            "Spring","Stone","Sunny","Timber","Union","Valley","West","White","Wild","Willow",
            "Wind","Wood","Yellow","Alder","Cypress","Glacier","Harvest","Shady","Twin","Winter"
        };
        private static readonly string[] SettlementSecondStems = {
            "Acre","Bend","Bluff","Bridge","Brook","Center","Crest","Crossing","Creek","Dale",
            "Falls","Field","Fork","Glen","Grove","Harbor","Heights","Hollow","Junction","Lake",
            "Landing","Meadow","Park","Pass","Point","Prairie","Ridge","River","Run","Shore",
            "Springs","Station","Terrace","Vale","Valley","View","Village","Wells","Woods","Yard"
        };
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { IncludeFields = true };

        /// <summary>In-process deterministic population and settlement generation.</summary>
        public static IReadOnlyList<PopulationChunk> Generate(PopulationRequest request)
        {
            var settings = request.Settings ?? new WorldGenerationSettings();
            var water = request.WaterFeatures ?? new List<MapFeature>();
            var inputs = (request.Chunks ?? new List<PopulationInput>()).OrderBy(c => c.ChunkY).ThenBy(c => c.ChunkX).ToList();
            var waterIndex=new WaterFeatureIndex(water);
            var roads = inputs.ToDictionary(c => (c.ChunkX,c.ChunkY), c => TransportSpawner.PlanMainRoadsForChunk(request.Seed,
                new TransportInput { ChunkX=c.ChunkX, ChunkY=c.ChunkY, TerrainGrid=c.TerrainGrid }, waterIndex.Nearby(c.ChunkX,c.ChunkY,5)));
            var outputs = inputs.ToDictionary(c => (c.ChunkX,c.ChunkY), c => new PopulationChunk { ChunkX=c.ChunkX, ChunkY=c.ChunkY });
            foreach(var input in inputs) outputs[(input.ChunkX,input.ChunkY)].Features.AddRange(roads[(input.ChunkX,input.ChunkY)].Features);
            var riverSegments=BuildRiverSegments(water);
            var riverIndex=new RiverSegmentIndex(riverSegments);
            var sites=FindSettlementSites(request.Seed,inputs,roads,riverIndex,settings);
            sites.AddRange(FindCountyRoadSites(request.Seed,inputs,riverSegments,settings,sites));
            foreach(var site in sites.OrderBy(s=>s.RouteId,StringComparer.Ordinal).ThenBy(s=>s.Station))
            {
                if(!outputs.TryGetValue((site.ChunkX,site.ChunkY),out var output)) continue;
                string clusterId=$"{site.RouteId}:settlement:{site.Station}";
                output.Features.Add(new MapFeature { Kind="settlement", RouteId=clusterId,
                    Name=SettlementName(request.Seed,clusterId),
                    IsMajorTown=!site.RouteId.StartsWith("county:",StringComparison.Ordinal) &&
                        Hash(request.Seed,clusterId+":major-town")%(ulong)Math.Max(1,settings.MajorTownChanceDenominator)==0, Population=site.PersonCount,
                    SiteSuitability=site.Score, Points=new List<MapPoint>{new MapPoint{X=site.X,Y=site.Y}} });
                for(int person=0;person<site.PersonCount;person++)
                {
                    ulong hash=Hash(request.Seed,clusterId+":person:"+person); double angle=(hash%1000000)/1000000.0*Math.PI*2;
                    double radius=.018+((hash>>20)%10000)/10000.0*.075; double x=site.X+Math.Cos(angle)*radius, y=site.Y+Math.Sin(angle)*radius;
                    double startX=site.X+3.5+((hash>>36)%1000)/1000.0*.5; string id=$"{clusterId}:person:{person}";
                    output.Settlers.Add(new Settler { Id=id, Name=PersonName(request.Seed,id), ClusterId=clusterId, StartX=startX, StartY=y, X=x, Y=y });
                }
            }
            return outputs.Values.OrderBy(c=>c.ChunkY).ThenBy(c=>c.ChunkX).ToList();
        }

        public static IReadOnlyList<WorldFeature> GenerateWorldFeatures(PopulationRequest request) =>
            LegacyMapFeatureConverter.ToWorldFeatures(Generate(request).SelectMany(chunk => chunk.Features));

        private sealed class RiverSegment
        {
            public (double X, double Y) A;
            public (double X, double Y) B;
            public double Width;
        }

        private sealed class RiverSegmentIndex
        {
            private readonly Dictionary<(int X,int Y),List<RiverSegment>> _cells=new();
            public RiverSegmentIndex(IEnumerable<RiverSegment> segments)
            {
                foreach(var segment in segments)
                {
                    int minX=(int)Math.Floor(Math.Min(segment.A.X,segment.B.X));
                    int maxX=(int)Math.Floor(Math.Max(segment.A.X,segment.B.X));
                    int minY=(int)Math.Floor(Math.Min(segment.A.Y,segment.B.Y));
                    int maxY=(int)Math.Floor(Math.Max(segment.A.Y,segment.B.Y));
                    for(int x=minX;x<=maxX;x++)for(int y=minY;y<=maxY;y++)
                    {if(!_cells.TryGetValue((x,y),out var cell))_cells[(x,y)]=cell=new List<RiverSegment>();cell.Add(segment);}
                }
            }
            public List<RiverSegment> Nearby(int chunkX,int chunkY,int radius=2)
            {
                var result=new HashSet<RiverSegment>();
                for(int x=chunkX-radius;x<=chunkX+radius;x++)for(int y=chunkY-radius;y<=chunkY+radius;y++)
                    if(_cells.TryGetValue((x,y),out var cell))foreach(var segment in cell)result.Add(segment);
                return result.ToList();
            }
        }

        private sealed class WaterFeatureIndex
        {
            private readonly Dictionary<(int X,int Y),List<MapFeature>> _cells=new();
            public WaterFeatureIndex(IEnumerable<MapFeature> features)
            {
                foreach(var feature in features??Array.Empty<MapFeature>())
                {
                    if(feature.Points==null||feature.Points.Count==0)continue;
                    int minX=(int)Math.Floor(feature.Points.Min(p=>p.X)),maxX=(int)Math.Floor(feature.Points.Max(p=>p.X));
                    int minY=(int)Math.Floor(feature.Points.Min(p=>p.Y)),maxY=(int)Math.Floor(feature.Points.Max(p=>p.Y));
                    for(int x=minX;x<=maxX;x++)for(int y=minY;y<=maxY;y++)
                    {if(!_cells.TryGetValue((x,y),out var cell))_cells[(x,y)]=cell=new List<MapFeature>();cell.Add(feature);}
                }
            }
            public List<MapFeature> Nearby(int chunkX,int chunkY,int radius)
            {
                var result=new HashSet<MapFeature>();
                for(int x=chunkX-radius;x<=chunkX+radius;x++)for(int y=chunkY-radius;y<=chunkY+radius;y++)
                    if(_cells.TryGetValue((x,y),out var cell))foreach(var feature in cell)result.Add(feature);
                return result.ToList();
            }
        }

        private sealed class SiteCandidate
        {
            public string RouteId;
            public long Station;
            public double X;
            public double Y;
            public double Score;
            public int ChunkX;
            public int ChunkY;
            public int PersonCount;
        }

        public static async Task<int> RunWorkerAsync()
        {
            try
            {
                var request = JsonSerializer.Deserialize<PopulationRequest>(await Console.In.ReadToEndAsync().ConfigureAwait(false), JsonOptions)
                    ?? throw new InvalidOperationException("Missing population worker request.");
                using var stdout = new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                foreach (var output in Generate(request))
                    await stdout.WriteLineAsync(JsonSerializer.Serialize(output, JsonOptions)).ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex) { await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false); return 1; }
        }

        private static List<SiteCandidate> FindSettlementSites(int seed, IReadOnlyList<PopulationInput> inputs,
            IReadOnlyDictionary<(int X, int Y), TransportChunk> roads, RiverSegmentIndex riverIndex, WorldGenerationSettings settings)
        {
            var candidates = new Dictionary<(string Route, long Station), SiteCandidate>();
            foreach (var input in inputs)
            {
                if (input.TerrainGrid == null) continue;
                var rivers=riverIndex.Nearby(input.ChunkX,input.ChunkY);
                foreach (var road in roads[(input.ChunkX, input.ChunkY)].Features)
                {
                    if (road.Kind != "main_road" || road.RouteId == null || !road.RouteId.Contains(":ew:", StringComparison.Ordinal)) continue;
                    for (int i = 0; i + 1 < road.Points.Count; i += 2)
                    {
                        var a = (road.Points[i].X, road.Points[i].Y);
                        var b = (road.Points[i + 1].X, road.Points[i + 1].Y);
                        foreach (var river in rivers)
                        {
                            if (!TryIntersection(a, b, river.A, river.B, out var crossing)) continue;
                            long station = (long)Math.Floor((crossing.X - .5) / settings.SettlementSpacingMiles);
                            double stationX = station * settings.SettlementSpacingMiles + .5;
                            if (Math.Abs(crossing.X - stationX) > settings.SettlementSpacingMiles * .5) continue;
                            if (!TryChooseSettlementPoint(input.TerrainGrid, crossing, river, a, b, out var x, out var y, out double landScore)) continue;
                            double score = Math.Max(.001, river.Width) * 50 + Math.Abs(crossing.X - stationX) + landScore;
                            var key = (road.RouteId, station);
                            if (!candidates.TryGetValue(key, out var old) || score < old.Score)
                            {
                                candidates[key] = new SiteCandidate
                                {
                                    RouteId = road.RouteId, Station = station, X = x, Y = y, Score = score,
                                    ChunkX = input.ChunkX, ChunkY = input.ChunkY,
                                    PersonCount = settings.PeopleAtNewSiteBase + (int)(Hash(seed, road.RouteId + ":people:" + station) % (ulong)(settings.PeopleAtNewSiteRollMax + 1))
                                };
                            }
                        }
                    }
                }
            }

            // If a station interval has no river crossing, use a suitable point
            // on the westward corridor that is still close to saved water.
            foreach (var input in inputs)
            {
                if (input.TerrainGrid == null) continue;
                var rivers=riverIndex.Nearby(input.ChunkX,input.ChunkY);
                foreach (var road in roads[(input.ChunkX, input.ChunkY)].Features)
                {
                    if (road.Kind != "main_road" || road.RouteId == null || !road.RouteId.Contains(":ew:", StringComparison.Ordinal)) continue;
                    foreach (var pair in road.Points.Select((p, i) => (p, i)).Where(v => v.i % 2 == 0))
                    {
                        var point = (pair.p.X, pair.p.Y);
                        long station = (long)Math.Floor((point.X - .5) / settings.SettlementSpacingMiles);
                        double stationX = station * settings.SettlementSpacingMiles + .5;
                        if (Math.Abs(point.X - stationX) > .2) continue;
                        var key = (road.RouteId, station);
                        if (candidates.ContainsKey(key)) continue;
                        double waterDistance = rivers.Count == 0 ? double.PositiveInfinity : rivers.Min(r => SegmentDistance(point, r.A, r.B));
                        if (waterDistance > settings.WaterSearchMiles) continue;
                        if (!SuitableLand(input.TerrainGrid, point.X, point.Y, out double landScore)) continue;
                        candidates[key] = new SiteCandidate
                        {
                            RouteId = road.RouteId, Station = station, X = point.X, Y = point.Y,
                            Score = waterDistance * 10 + landScore,
                            ChunkX = input.ChunkX, ChunkY = input.ChunkY,
                            PersonCount = settings.PeopleAtNewSiteBase + (int)(Hash(seed, road.RouteId + ":people:" + station) % (ulong)(settings.PeopleAtNewSiteRollMax + 1))
                        };
                    }
                }
            }
            var accepted = new List<SiteCandidate>();
            foreach (var candidate in candidates.Values
                .OrderBy(site => site.Score)
                .ThenBy(site => site.RouteId, StringComparer.Ordinal)
                .ThenBy(site => site.Station))
            {
                bool tooClose = accepted.Any(site =>
                {
                    double dx = site.X - candidate.X, dy = site.Y - candidate.Y;
                    return dx * dx + dy * dy < settings.MinimumSettlementSeparationMiles * settings.MinimumSettlementSeparationMiles;
                });
                if (!tooClose) accepted.Add(candidate);
            }
            return accepted;
        }

        private static List<SiteCandidate> FindCountyRoadSites(int seed, IReadOnlyList<PopulationInput> inputs,
            IReadOnlyList<RiverSegment> rivers, WorldGenerationSettings settings, IReadOnlyList<SiteCandidate> existing)
        {
            var result = new List<SiteCandidate>();
            foreach (var input in inputs)
            {
                if (input.TerrainGrid == null) continue;
                for (int countyY = input.ChunkY - 2; countyY <= input.ChunkY + 2; countyY++)
                for (int countyX = input.ChunkX - 2; countyX <= input.ChunkX + 2; countyX++)
                {
                    if (!IsCountyRoute(countyX) || !IsCountyRoute(countyY)) continue;
                    string intersectionId = $"county:intersection:{countyX}:{countyY}";
                    if (Hash(seed, intersectionId + ":small-town") % 100 >= 65) continue;
                    ulong placementHash=Hash(seed,intersectionId+":small-town-site");
                    string arm=(placementHash&1)==0?"ew":"ns";
                    int step=(int)((placementHash>>8)%9)-4;
                    double offset=step*.5;
                    double countyRoadX=countyX+RoadSpawner.CountyRoadOffset(seed,"ns",countyX);
                    double countyRoadY=countyY+RoadSpawner.CountyRoadOffset(seed,"ew",countyY);
                    double x=arm=="ew"?countyRoadX+offset:countyRoadX;
                    double y=arm=="ew"?countyRoadY:countyRoadY+offset;
                    if((int)Math.Floor(x)!=input.ChunkX||(int)Math.Floor(y)!=input.ChunkY)continue;
                    double waterDistance=rivers.Count==0?double.PositiveInfinity:rivers.Min(r=>SegmentDistance((x,y),r.A,r.B));
                    if(!SuitableLand(input.TerrainGrid,x,y,out double landScore))continue;
                    string routeId=$"{intersectionId}:site:{arm}:{step}";
                    bool tooClose = existing.Concat(result).Any(site =>
                    {
                        double dx = site.X - x, dy = site.Y - y;
                        return dx * dx + dy * dy < 2.0 * 2.0;
                    });
                    if (tooClose) continue;
                    int people = Math.Max(12, settings.PeopleAtNewSiteBase / 2) +
                        (int)(Hash(seed,routeId+":people")%(ulong)(Math.Max(1,settings.PeopleAtNewSiteRollMax/2+1)));
                    double waterPreference=double.IsFinite(waterDistance)?Math.Min(100,waterDistance*5):100;
                    result.Add(new SiteCandidate { RouteId=routeId,Station=0,X=x,Y=y,Score=waterPreference+landScore,
                        ChunkX=input.ChunkX,ChunkY=input.ChunkY,PersonCount=people });
                }
            }
            return result;
        }

        private static bool IsCountyRoute(int line) => ((line % 12) + 12) % 12 == 1;

        private static bool TryChooseSettlementPoint(ElevationGridData grid, (double X, double Y) crossing,
            RiverSegment river, (double X, double Y) a, (double X, double Y) b,
            out double x, out double y, out double score)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            x = y = score = 0;
            if (length < 1e-9) return false;
            double ux = dx / length, uy = dy / length;
            bool found = false;
            double best = double.PositiveInfinity;
            foreach (double along in new[] { .12, .28, .44 })
            foreach (double side in new[] { -1.0, 1.0 })
            {
                double offset = Math.Max(along, river.Width * .5 + .08);
                double cx = crossing.X + ux * offset * side;
                double cy = crossing.Y + uy * offset * side;
                if (!SuitableLand(grid, cx, cy, out double land)) continue;
                double candidate = land + Math.Abs(offset - .28) * 3;
                if (candidate >= best) continue;
                found = true; best = candidate; x = cx; y = cy; score = land;
            }
            return found;
        }

        private static bool SuitableLand(ElevationGridData grid, double x, double y, out double score)
        {
            double h = TransportSpawner.SampleElevation(grid, x, y);
            var samples = new List<double>(8);
            foreach (var offset in new[] { (-.28, 0.0), (.28, 0.0), (0.0, -.28), (0.0, .28),
                                           (-.20, -.20), (.20, -.20), (-.20, .20), (.20, .20) })
                samples.Add(TransportSpawner.SampleElevation(grid, x + offset.Item1, y + offset.Item2));
            double slope = samples.Max(sample => Math.Abs(sample - h));
            double roughness = Math.Max(h, samples.Max()) - Math.Min(h, samples.Min());
            score = slope * .12 + roughness * .07 + Math.Max(0, 25 - (h + 50)) * .08;
            return h > -35 && slope < 65 && roughness < 105;
        }

        private static List<RiverSegment> BuildRiverSegments(IReadOnlyList<MapFeature> features)
        {
            var result = new List<RiverSegment>();
            foreach (var feature in features ?? Array.Empty<MapFeature>())
            {
                if (feature.Kind != "rivers" || feature.Points == null) continue;
                for (int i = 0; i + 1 < feature.Points.Count; i += 2)
                    result.Add(new RiverSegment
                    {
                        A = (feature.Points[i].X, feature.Points[i].Y),
                        B = (feature.Points[i + 1].X, feature.Points[i + 1].Y),
                        Width = Math.Max(0, feature.Width)
                    });
            }
            return result;
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

        private static double SegmentDistance((double X, double Y) point, (double X, double Y) a, (double X, double Y) b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, length = dx * dx + dy * dy;
            double t = length <= 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / length));
            return Math.Sqrt(Math.Pow(point.X - a.X - t * dx, 2) + Math.Pow(point.Y - a.Y - t * dy, 2));
        }

        public static string SurnameFromPersonName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            return Surnames.OrderByDescending(s => s.Length)
                .FirstOrDefault(s => name.EndsWith(" " + s, StringComparison.OrdinalIgnoreCase))
                ?? name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
        }

        private static string PersonName(int seed, string id)
        {
            ulong hash = Hash(seed, id + ":name");
            return GivenNames[hash % (ulong)GivenNames.Length] + " " + Surnames[(hash >> 32) % (ulong)Surnames.Length];
        }

        private static string SettlementName(int seed, string settlementId)
        {
            ulong hash = Hash(seed, "settlement-name:" + settlementId);
            return SettlementFirstStems[hash % (ulong)SettlementFirstStems.Length] + " " +
                SettlementSecondStems[(hash >> 32) % (ulong)SettlementSecondStems.Length];
        }

        private static ulong Hash(int seed, string text)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                foreach (char c in text) { h ^= c; h *= 1099511628211UL; }
                h ^= (uint)seed; h *= 1099511628211UL;
                h ^= h >> 30; h *= 0xbf58476d1ce4e5b9UL;
                h ^= h >> 27; h *= 0x94d049bb133111ebUL;
                return h ^ (h >> 31);
            }
        }

        private static async Task StopWorkerAsync(Process process)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            try { await process.WaitForExitAsync().ConfigureAwait(false); } catch { }
        }
    }
}

