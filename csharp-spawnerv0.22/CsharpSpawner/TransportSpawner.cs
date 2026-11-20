using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CsharpSpawner
{
    // Plans regional rail and main-road corridors from saved elevation grids.
    // It deliberately does not call the elevation generator.
    public static class TransportSpawner
    {
        private const double FeetPerMile = 5280.0;
        private const double Tau = Math.PI * 2;
        private const double RouteSampleMiles = .25;
        private const double WaterAvoidanceCost = 50000;
        private const double CommonCrossingRangeMiles = 1.0;
        private const double SharedCrossingBaseCost = 2500;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { IncludeFields = true };

        private sealed class WaterObstacles
        {
            public readonly List<((double X, double Y) A, (double X, double Y) B, double Width)> Rivers = new List<((double, double), (double, double), double)>();
            public readonly List<List<(double X, double Y)>> Lakes = new List<List<(double, double)>>();
        }
        private sealed class CommonCrossing
        {
            public double X { get; set; }
            public double Y { get; set; }
        }
        private sealed class RoadCrossingIndex
        {
            private const double CellsPerMile = 4;
            private readonly List<((double X,double Y) A,(double X,double Y) B)> _segments = new();
            private readonly Dictionary<(int X,int Y),List<int>> _cells = new();
            public RoadCrossingIndex(IReadOnlyList<MapFeature> roads)
            {
                foreach(var road in roads??Array.Empty<MapFeature>())
                {
                    if(road.Points==null)continue;
                    for(int i=0;i+1<road.Points.Count;i+=2)
                    {
                        var a=(road.Points[i].X,road.Points[i].Y);var b=(road.Points[i+1].X,road.Points[i+1].Y);
                        int id=_segments.Count;_segments.Add((a,b));
                        int minX=(int)Math.Floor(Math.Min(a.X,b.X)*CellsPerMile),maxX=(int)Math.Floor(Math.Max(a.X,b.X)*CellsPerMile);
                        int minY=(int)Math.Floor(Math.Min(a.Y,b.Y)*CellsPerMile),maxY=(int)Math.Floor(Math.Max(a.Y,b.Y)*CellsPerMile);
                        for(int x=minX;x<=maxX;x++)for(int y=minY;y<=maxY;y++)
                        {if(!_cells.TryGetValue((x,y),out var ids))_cells[(x,y)]=ids=new List<int>();ids.Add(id);}
                    }
                }
            }
            public double Cost((double X,double Y) a,(double X,double Y) b)
            {
                double rdx=b.X-a.X,rdy=b.Y-a.Y,railLength=Math.Sqrt(rdx*rdx+rdy*rdy);
                if(railLength<1e-9)return 0;
                int minX=(int)Math.Floor(Math.Min(a.X,b.X)*CellsPerMile),maxX=(int)Math.Floor(Math.Max(a.X,b.X)*CellsPerMile);
                int minY=(int)Math.Floor(Math.Min(a.Y,b.Y)*CellsPerMile),maxY=(int)Math.Floor(Math.Max(a.Y,b.Y)*CellsPerMile);
                double cost=0;
                for(int x=minX;x<=maxX;x++)for(int y=minY;y<=maxY;y++)
                {
                    if(!_cells.TryGetValue((x,y),out var ids))continue;
                    foreach(int id in ids)
                    {
                        var road=_segments[id];var c=road.A;var d=road.B;
                        if(Math.Max(Math.Min(a.X,b.X),Math.Min(c.X,d.X))>Math.Min(Math.Max(a.X,b.X),Math.Max(c.X,d.X))||
                           Math.Max(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y))>Math.Min(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y)))continue;
                        if(!TryIntersection(a,b,c,d,out _))continue;
                        double dx=d.X-c.X,dy=d.Y-c.Y,roadLength=Math.Sqrt(dx*dx+dy*dy);
                        if(roadLength<1e-9)continue;
                        double sine=Math.Abs(rdx*dy-rdy*dx)/(railLength*roadLength);
                        if(sine<Math.Sin(Math.PI/4))return 100000;
                        cost+=Math.Pow(1-sine,2)*500;
                    }
                }
                return cost;
            }
        }
        /// <summary>One shared main line and complete, connected town branches.</summary>
        public static TransportChunk GenerateRailChunk(int seed,TransportInput input,IReadOnlyList<MapFeature> water,
            IReadOnlyList<MapFeature> majorTowns,IReadOnlyList<MapFeature> mainRoads)
            => GenerateRailChunks(seed,new[]{input},water,majorTowns,mainRoads)[0];

        public static IReadOnlyList<TransportChunk> GenerateRailChunks(int seed,IReadOnlyList<TransportInput> inputs,
            IReadOnlyList<MapFeature> water,IReadOnlyList<MapFeature> majorTowns,IReadOnlyList<MapFeature> roadCenters)
        {
            var branches=RailNetworkPlanner.BuildBranches(seed,majorTowns,roadCenters);
            var waterIndex=new FeatureSpatialIndex(water);
            return (inputs??Array.Empty<TransportInput>()).Select(input=>
            {
                var output=new TransportChunk{ChunkX=input.ChunkX,ChunkY=input.ChunkY};
                var obstacles=BuildWaterObstacles(waterIndex.QueryChunk(input.ChunkX,input.ChunkY,1));
                Emit("rail:main:ew","Main rail","mainline",RailNetworkPlanner.MainPoints(seed,input.ChunkX));
                foreach(var branch in branches)
                {
                    Emit(branch.Id,branch.Name,"branch",branch.Points);
                    var terminal=branch.Points[^1];
                    if((int)Math.Floor(terminal.X)==input.ChunkX&&(int)Math.Floor(terminal.Y)==input.ChunkY)
                        output.Features.Add(new MapFeature{Kind="rail_terminal",RouteId=branch.Id+":terminal",Carrier=branch.Id,Name=branch.Name,
                            Points=new List<MapPoint>{new(){X=terminal.X,Y=terminal.Y}}});
                }
                return output;

                void Emit(string id,string name,string role,IReadOnlyList<(double X,double Y)> points)
                {
                    if(points.Max(p=>p.X)<input.ChunkX||points.Min(p=>p.X)>input.ChunkX+1||
                       points.Max(p=>p.Y)<input.ChunkY||points.Min(p=>p.Y)>input.ChunkY+1)return;
                    var feature=new MapFeature{Kind="rail",RouteId=id,Name=name,Carrier=role,Width=.010,Disjoint=true};
                    for(int i=0;i+1<points.Count;i++)
                    {
                        if(!Clip(points[i],points[i+1],input.ChunkX,input.ChunkY,input.ChunkX+1,input.ChunkY+1,out var a,out var b)||Distance(a,b)<1e-9)continue;
                        feature.Points.Add(new MapPoint{X=a.X,Y=a.Y});feature.Points.Add(new MapPoint{X=b.X,Y=b.Y});
                        AddBridgeCrossings(output,id,"rail",a,b,obstacles);
                    }
                    if(feature.Points.Count>0)output.Features.Add(feature);
                }
            }).ToList();
        }
        public static IReadOnlyList<WorldFeature> GenerateWorldRail(int seed,TransportInput input,IReadOnlyList<MapFeature> water,
            IReadOnlyList<MapFeature> majorTowns,IReadOnlyList<MapFeature> mainRoads)
            => LegacyMapFeatureConverter.ToWorldFeatures(GenerateRailChunk(seed,input,water,majorTowns,mainRoads).Features);

        public static async Task<int> RunWorkerAsync()
        {
            try
            {
                var request=JsonSerializer.Deserialize<TransportRequest>(await Console.In.ReadToEndAsync().ConfigureAwait(false),JsonOptions)
                    ?? throw new InvalidOperationException("Missing transport worker request.");
                var chunks=GenerateRailChunks(request.Seed,request.Chunks,request.WaterFeatures,request.MajorTowns,request.MainRoads);
                using var stdout=new System.IO.StreamWriter(Console.OpenStandardOutput()){AutoFlush=true};
                foreach(var chunk in chunks)await stdout.WriteLineAsync(JsonSerializer.Serialize(chunk,JsonOptions)).ConfigureAwait(false);
                return 0;
            }
            catch(Exception ex){await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false);return 1;}
        }

        private static List<MapFeature> WaterFeaturesNear(List<MapFeature> features, int chunkX, int chunkY, double margin)
        {
            double left = chunkX - margin, right = chunkX + 1 + margin;
            double top = chunkY - margin, bottom = chunkY + 1 + margin;
            return features.Where(f => f.Points != null && f.Points.Count > 0 &&
                f.Points.Max(p => p.X) >= left && f.Points.Min(p => p.X) <= right &&
                f.Points.Max(p => p.Y) >= top && f.Points.Min(p => p.Y) <= bottom).ToList();
        }

        internal static List<(double X,double Y)> SmoothRailAlignment(IReadOnlyList<(double X,double Y)> source,double tangentLength)
        {
            var points=new List<(double X,double Y)>();
            foreach(var p in source)if(points.Count==0||Distance(points[^1],p)>1e-8)points.Add(p);
            if(points.Count<3)return points;
            var result=new List<(double X,double Y)>{points[0]};
            for(int i=1;i+1<points.Count;i++)
            {
                var a=points[i-1];var b=points[i];var c=points[i+1];
                double al=Distance(a,b),bl=Distance(b,c),trim=Math.Min(tangentLength,Math.Min(al,bl)*.42);
                var entry=(X:b.X+(a.X-b.X)*trim/al,Y:b.Y+(a.Y-b.Y)*trim/al);
                var exit=(X:b.X+(c.X-b.X)*trim/bl,Y:b.Y+(c.Y-b.Y)*trim/bl);
                result.Add(entry);
                int samples=Math.Max(3,(int)Math.Ceiling(trim/.015));
                for(int j=1;j<=samples;j++)
                {
                    double t=j/(double)samples,u=1-t;
                    result.Add((u*u*entry.X+2*u*t*b.X+t*t*exit.X,u*u*entry.Y+2*u*t*b.Y+t*t*exit.Y));
                }
            }
            result.Add(points[^1]);return result;
        }

        private static double Distance((double X,double Y) a,(double X,double Y) b)
        {double dx=a.X-b.X,dy=a.Y-b.Y;return Math.Sqrt(dx*dx+dy*dy);}

        internal static bool HasShallowRoadCrossing(IReadOnlyList<(double X, double Y)> rail,
            IReadOnlyList<MapFeature> roads)
        {
            foreach (var road in roads ?? Array.Empty<MapFeature>())
            {
                if (road.Points == null) continue;
                for (int i = 0; i + 1 < rail.Count; i++)
                for (int j = 0; j + 1 < road.Points.Count; j += 2)
                {
                    var a = rail[i]; var b = rail[i + 1];
                    var c = (road.Points[j].X, road.Points[j].Y); var d = (road.Points[j + 1].X, road.Points[j + 1].Y);
                    if (!TryIntersection(a, b, c, d, out _)) continue;
                    double rx = b.X - a.X, ry = b.Y - a.Y, sx = d.Item1 - c.Item1, sy = d.Item2 - c.Item2;
                    double denominator = Math.Sqrt((rx * rx + ry * ry) * (sx * sx + sy * sy));
                    if (denominator < 1e-12) continue;
                    double sine = Math.Abs(rx * sy - ry * sx) / denominator;
                    if (sine < Math.Sin(Math.PI / 4) - 1e-9) return true;
                }
            }
            return false;
        }

        private static TransportChunk PlanTile(int seed,TransportInput input,IReadOnlyList<CommonCrossing> commonCrossings)
        {
            var result=new TransportChunk{ChunkX=input.ChunkX,ChunkY=input.ChunkY};
            var water=BuildWaterObstacles(input.WaterFeatures);
            AddParallelRoutes(result,input.TerrainGrid,water,seed,"main_road",true,input.ChunkY-1,input.ChunkY+3,
                40,10,.173,8,.125,1,.55,40,commonCrossings);
            AddParallelRoutes(result,input.TerrainGrid,water,seed,"main_road",false,input.ChunkX-1,input.ChunkX+3,
                40,10,.191,8,.125,1,.55,40,commonCrossings);
            return result;
        }

        internal static TransportChunk PlanMainRoadsForChunk(int seed, TransportInput input,
            IReadOnlyList<MapFeature> waterFeatures)
        {
            input.WaterFeatures = WaterFeaturesNear((waterFeatures ?? Array.Empty<MapFeature>()).ToList(), input.ChunkX, input.ChunkY, 5);
            return PlanTile(seed,input,Array.Empty<CommonCrossing>());
        }

        internal static double SampleElevation(ElevationGridData grid, double xMiles, double yMiles) =>
            Sample(grid, xMiles * FeetPerMile, yMiles * FeetPerMile);

        private static void AddParallelRoutes(TransportChunk output, ElevationGridData terrain, WaterObstacles water, int seed, string kind,
            bool eastWest, double min, double max, double meanGap, double gapAmplitude, double frequency,
            int reachSteps, double offsetStep, int maxOffsetDelta, double elevationWeight, double lateralPenalty,
            IReadOnlyList<CommonCrossing> commonCrossings, RoadCrossingIndex roadCrossingRules = null)
        {
            double average = meanGap;
            long approx = (long)Math.Floor(((min + max) * .5) / average);
            for (long index = approx - 4; index <= approx + 4; index++)
            {
                double corridor = LinePosition(seed, kind, eastWest, index, meanGap, gapAmplitude, frequency);
                if (corridor < min || corridor > max) continue;
                var route = FindLowChangeRoute(terrain, water, seed, kind, eastWest, index, output.ChunkX, output.ChunkY,
                    corridor, reachSteps, offsetStep, maxOffsetDelta, elevationWeight, lateralPenalty,
                    kind == "rail" ? .04 : .025, commonCrossings, roadCrossingRules);
                if (route.Count < 2) continue;
                if(kind=="rail")
                {
                    var smooth=SmoothRailAlignment(route,.08);
                    if(Enumerable.Range(0,smooth.Count-1).All(i=>(roadCrossingRules?.Cost(smooth[i],smooth[i+1])??0)<100000))route=smooth;
                }
                var feature = new MapFeature
                {
                    Kind = kind,
                    RouteId = $"{kind}:{(eastWest ? "ew" : "ns")}:{index}",
                    Disjoint = true,
                    Width = kind == "rail" ? .010 : .020
                };
                for (int i = 0; i + 1 < route.Count; i++)
                {
                    AddBridgeCrossings(output, feature.RouteId, kind, route[i], route[i + 1], water);
                    if (!Clip(route[i], route[i + 1], output.ChunkX, output.ChunkY,
                        output.ChunkX + 1, output.ChunkY + 1, out var a, out var b)) continue;
                    feature.Points.Add(new MapPoint { X = a.X, Y = a.Y });
                    feature.Points.Add(new MapPoint { X = b.X, Y = b.Y });
                }
                if (feature.Points.Count > 0) output.Features.Add(feature);
            }
        }

        private static void AddBridgeCrossings(TransportChunk output, string routeId, string carrier,
            (double X, double Y) a, (double X, double Y) b, WaterObstacles water)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double routeLength = Math.Sqrt(dx * dx + dy * dy);
            if (routeLength <= 1e-9) return;
            foreach (var river in water.Rivers)
            {
                if (!TryIntersection(a, b, river.A, river.B, out var crossing)) continue;
                if (crossing.X < output.ChunkX || crossing.X > output.ChunkX + 1 ||
                    crossing.Y < output.ChunkY || crossing.Y > output.ChunkY + 1) continue;
                if (output.Features.Any(feature => feature.Kind == "bridges" && feature.RouteId == routeId &&
                    feature.Points.Count > 0 && Math.Sqrt(Math.Pow(feature.Points[0].X - crossing.X, 2) +
                    Math.Pow(feature.Points[0].Y - crossing.Y, 2)) < .05)) continue;
                double halfLength = Math.Max(.035, river.Width * .5 + .02);
                double ux = dx / routeLength, uy = dy / routeLength;
                output.Features.Add(new MapFeature
                {
                    Kind = "bridges",
                    RouteId = routeId,
                    Carrier = carrier,
                    Points = new List<MapPoint>
                    {
                        new MapPoint { X = crossing.X - ux * halfLength, Y = crossing.Y - uy * halfLength },
                        new MapPoint { X = crossing.X + ux * halfLength, Y = crossing.Y + uy * halfLength }
                    }
                });
            }
        }

        private static List<(double X, double Y)> FindLowChangeRoute(ElevationGridData grid, WaterObstacles water, int seed,
            string kind, bool eastWest, long lineIndex, int chunkX, int chunkY, double corridor, int reachSteps,
            double offsetStep, int maxOffsetDelta, double elevationWeight, double lateralPenalty,
            double waterClearance, IReadOnlyList<CommonCrossing> commonCrossings, RoadCrossingIndex roadCrossingRules)
        {
            const int samples = 4;
            int stateCount = reachSteps * 2 + 1;
            var costs = new double[samples + 1, stateCount];
            var previous = new int[samples + 1, stateCount];
            for (int s = 0; s <= samples; s++) for (int k = 0; k < stateCount; k++)
            { costs[s, k] = double.PositiveInfinity; previous[s, k] = -1; }

            double ElevationAt(int sample, int offset)
            {
                double along = (eastWest ? chunkX : chunkY) + sample * RouteSampleMiles;
                double cross = corridor + offset * offsetStep;
                double x = (eastWest ? along : cross) * FeetPerMile;
                double y = (eastWest ? cross : along) * FeetPerMile;
                return Sample(grid, x, y);
            }

            // Pin chunk edges to the same slow, world-aligned curve. This lets
            // major roads follow broad terrain without snapping straight at each
            // mile boundary. Rail routes retain their stricter straight trunk.
            int alongChunk = eastWest ? chunkX : chunkY;
            int startOffset = kind == "main_road" ? MainRoadBoundaryOffset(seed, eastWest, lineIndex, alongChunk, offsetStep, reachSteps) :
                kind == "rail" ? RailBoundaryOffset(seed,eastWest,lineIndex,alongChunk) : 0;
            int endOffset = kind == "main_road" ? MainRoadBoundaryOffset(seed, eastWest, lineIndex, alongChunk + 1, offsetStep, reachSteps) :
                kind == "rail" ? RailBoundaryOffset(seed,eastWest,lineIndex,alongChunk+1) : 0;

            var elevations = new double[samples + 1, stateCount];
            for (int s = 0; s <= samples; s++)
            for (int offset = -reachSteps; offset <= reachSteps; offset++)
                elevations[s, offset + reachSteps] = ElevationAt(s, offset);

            for (int s = 0; s <= samples; s++)
            {
                int low = s == 0 ? startOffset : s == samples ? endOffset : -reachSteps;
                int high = s == 0 ? startOffset : s == samples ? endOffset : reachSteps;
                for (int offset = low; offset <= high; offset++)
                {
                    int k = offset + reachSteps;
                    double elevation = elevations[s, k];
                    if (s == 0) { costs[s, k] = 0; continue; }
                    for (int priorOffset = -reachSteps; priorOffset <= reachSteps; priorOffset++)
                    {
                        int offsetDelta = Math.Abs(offset - priorOffset);
                        if (offsetDelta > maxOffsetDelta) continue;
                        int pk = priorOffset + reachSteps;
                        if (double.IsPositiveInfinity(costs[s - 1, pk])) continue;
                        double priorElevation = elevations[s - 1, pk];
                        double grade = Math.Abs(elevation - priorElevation) * elevationWeight;
                        if(kind=="rail")
                        {
                            double rise=Math.Abs(elevation-priorElevation)/(RouteSampleMiles*FeetPerMile);
                            grade+=Math.Pow(Math.Max(0,rise-.015)*100,2)*80;
                        }
                        var a = RoutePoint(s - 1, priorOffset);
                        var b = RoutePoint(s, offset);
                        double bend = offsetDelta * offsetDelta * offsetStep * lateralPenalty;
                        double drift = Math.Abs(offset * offsetStep) * lateralPenalty * .08;
                        double waterCost = WaterTransitionCost(a, b, water, waterClearance,
                            kind == "rail", commonCrossings);
                        double roadCrossingCost = kind == "rail" ? roadCrossingRules?.Cost(a,b) ?? 0 : 0;
                        int tieChunk = eastWest ? chunkX : chunkY;
                        double tie = (Hash(seed, $"{kind}:{(eastWest ? 'h' : 'v')}:{lineIndex}:{tieChunk}:{s}:{offset}") % 997) * .00001;
                        double candidate = costs[s - 1, pk] + grade + bend + drift + waterCost + roadCrossingCost + tie;
                        if (candidate < costs[s, k]) { costs[s, k] = candidate; previous[s, k] = pk; }
                    }
                }
            }

            int state = endOffset + reachSteps;
            var offsets = new int[samples + 1];
            for (int s = samples; s >= 0; s--)
            {
                offsets[s] = state - reachSteps;
                if (s > 0) state = previous[s, state];
                if (state < 0) return new List<(double, double)>();
            }
            var points = new List<(double X, double Y)>(samples + 1);
            for (int s = 0; s <= samples; s++)
            {
                points.Add(RoutePoint(s, offsets[s]));
            }
            return points;

            (double X, double Y) RoutePoint(int sample, int offset)
            {
                double along = (eastWest ? chunkX : chunkY) + sample * RouteSampleMiles;
                double cross = corridor + offset * offsetStep;
                return eastWest ? (along, cross) : (cross, along);
            }
        }

        private static int MainRoadBoundaryOffset(int seed, bool eastWest, long lineIndex, int along, double step, int reach)
        {
            double phase = (Hash(seed, $"major-road-wave:{(eastWest ? 'h' : 'v')}:{lineIndex}") % 1000000) / 1000000.0 * Tau;
            double wave = Math.Sin(along * .12 + phase) * 2.4 + Math.Sin(along * .047 + phase * .71) * 1.6;
            return Math.Max(-reach, Math.Min(reach, (int)Math.Round(wave)));
        }

        private static int RailBoundaryOffset(int seed,bool eastWest,long lineIndex,int along)
        {
            double phase=(Hash(seed,$"rail-wave:{(eastWest?'h':'v')}:{lineIndex}")%1000000)/1000000.0*Tau;
            return (int)Math.Round(Math.Sin(along*.10+phase)*3+Math.Sin(along*.035+phase*.63)*1.5);
        }

        private static double Sample(ElevationGridData grid, double x, double y)
        {
            double gx = (x - grid.OriginXFt) / grid.StepFt, gy = (y - grid.OriginYFt) / grid.StepFt;
            int ix = (int)Math.Floor(gx), iy = (int)Math.Floor(gy);
            ix = Math.Max(0, Math.Min(grid.Values[0].Count - 2, ix));
            iy = Math.Max(0, Math.Min(grid.Values.Count - 2, iy));
            double tx = Math.Max(0, Math.Min(1, gx - ix)), ty = Math.Max(0, Math.Min(1, gy - iy));
            double a = grid.Values[iy][ix] * (1 - tx) + grid.Values[iy][ix + 1] * tx;
            double b = grid.Values[iy + 1][ix] * (1 - tx) + grid.Values[iy + 1][ix + 1] * tx;
            return a * (1 - ty) + b * ty;
        }

        private static WaterObstacles BuildWaterObstacles(IReadOnlyList<MapFeature> features)
        {
            var obstacles = new WaterObstacles();
            foreach (var feature in features ?? Array.Empty<MapFeature>())
            {
                if (feature.Points == null) continue;
                if (feature.Kind == "lakes" && feature.Points.Count >= 3)
                {
                    obstacles.Lakes.Add(feature.Points.Select(p => (p.X, p.Y)).ToList());
                }
                else if (feature.Kind == "rivers")
                {
                    for (int i = 0; i + 1 < feature.Points.Count; i += 2)
                        obstacles.Rivers.Add(((feature.Points[i].X, feature.Points[i].Y),
                            (feature.Points[i + 1].X, feature.Points[i + 1].Y), Math.Max(0, feature.Width)));
                }
            }
            return obstacles;
        }

        private static double WaterTransitionCost((double X, double Y) a, (double X, double Y) b,
            WaterObstacles water, double clearance, bool rail, IReadOnlyList<CommonCrossing> commonCrossings)
        {
            double cost = 0;
            foreach (var river in water.Rivers)
            {
                if (SegmentDistance(a, b, river.A, river.B) > river.Width * .5 + clearance) continue;
                double crossingCost = WaterAvoidanceCost;
                if (TryIntersection(a, b, river.A, river.B, out var intersection))
                {
                    double routeLength = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
                    double riverLength = Math.Sqrt(Math.Pow(river.B.X - river.A.X, 2) + Math.Pow(river.B.Y - river.A.Y, 2));
                    double sine = routeLength <= 1e-9 || riverLength <= 1e-9 ? 1 :
                        Math.Abs((b.X - a.X) * (river.B.Y - river.A.Y) - (b.Y - a.Y) * (river.B.X - river.A.X)) / (routeLength * riverLength);
                    crossingCost += (1 - Math.Min(1, sine)) * (rail ? 18000 : 5000);
                    if (!rail)
                    {
                        double nearest = CommonCrossingRangeMiles;
                        foreach (var crossing in commonCrossings ?? Array.Empty<CommonCrossing>())
                            nearest = Math.Min(nearest, Math.Sqrt(Math.Pow(crossing.X - intersection.X, 2) + Math.Pow(crossing.Y - intersection.Y, 2)));
                        if (nearest < CommonCrossingRangeMiles)
                            crossingCost = Math.Min(crossingCost, SharedCrossingBaseCost + nearest * 18000 + (1 - Math.Min(1, sine)) * 5000);
                    }
                }
                cost = Math.Max(cost, crossingCost);
            }
            foreach (var lake in water.Lakes)
            {
                if (PointInPolygon(a, lake) || PointInPolygon(b, lake)) return Math.Max(cost, WaterAvoidanceCost);
                for (int i = 0; i < lake.Count; i++)
                    if (SegmentDistance(a, b, lake[i], lake[(i + 1) % lake.Count]) <= clearance)
                        cost = Math.Max(cost, WaterAvoidanceCost);
            }
            return cost;
        }

        private static bool PointInPolygon((double X, double Y) point, List<(double X, double Y)> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i]; var b = polygon[j];
                if ((a.Y > point.Y) != (b.Y > point.Y) &&
                    point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            }
            return inside;
        }

        private static double SegmentDistance((double X, double Y) a, (double X, double Y) b,
            (double X, double Y) c, (double X, double Y) d)
        {
            if (SegmentsIntersect(a, b, c, d)) return 0;
            return Math.Min(Math.Min(PointToSegmentDistance(a, c, d), PointToSegmentDistance(b, c, d)),
                Math.Min(PointToSegmentDistance(c, a, b), PointToSegmentDistance(d, a, b)));
        }

        private static bool TryIntersection((double X, double Y) a, (double X, double Y) b,
            (double X, double Y) c, (double X, double Y) d, out (double X, double Y) point)
        {
            double rx = b.X - a.X, ry = b.Y - a.Y, sx = d.X - c.X, sy = d.Y - c.Y;
            double denominator = rx * sy - ry * sx;
            if (Math.Abs(denominator) < 1e-12) { point = default; return false; }
            double qx = c.X - a.X, qy = c.Y - a.Y;
            double t = (qx * sy - qy * sx) / denominator;
            double u = (qx * ry - qy * rx) / denominator;
            if (t < -1e-9 || t > 1 + 1e-9 || u < -1e-9 || u > 1 + 1e-9) { point = default; return false; }
            point = (a.X + t * rx, a.Y + t * ry);
            return true;
        }

        private static double PointToSegmentDistance((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double denominator = dx * dx + dy * dy;
            double t = denominator == 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / denominator));
            return Math.Sqrt(Math.Pow(p.X - a.X - t * dx, 2) + Math.Pow(p.Y - a.Y - t * dy, 2));
        }

        private static bool SegmentsIntersect((double X, double Y) a, (double X, double Y) b,
            (double X, double Y) c, (double X, double Y) d)
        {
            static double Cross((double X, double Y) p, (double X, double Y) q, (double X, double Y) r) =>
                (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
            double abC = Cross(a, b, c), abD = Cross(a, b, d), cdA = Cross(c, d, a), cdB = Cross(c, d, b);
            return abC * abD <= 0 && cdA * cdB <= 0 &&
                Math.Max(Math.Min(a.X, b.X), Math.Min(c.X, d.X)) <= Math.Min(Math.Max(a.X, b.X), Math.Max(c.X, d.X)) + 1e-12 &&
                Math.Max(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)) <= Math.Min(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)) + 1e-12;
        }

        private static double LinePosition(int seed, string kind, bool eastWest, long index,
            double meanGap, double amplitude, double frequency)
        {
            string axis = kind + (eastWest ? "-ew" : "-ns");
            double phase = (Hash(seed, axis) % 1000000) / 1000000.0 * Tau;
            // The legacy road and rail lattices were offset one mile from the
            // world axes. Keeping that offset puts the reference corridor in
            // the default 5 x 5 mile view instead of on its outer edge.
            // Keep the regional rail trunk on its own corridor instead of
            // accidentally sharing the first major-road/county-road alignment.
            double origin=kind=="rail"?2.5:1;
            return origin + SumGap(index, meanGap, amplitude, frequency, phase);
        }

        private static double SumGap(long index, double mean, double amplitude, double frequency, double phase)
        {
            if (index == 0) return 0;
            long start = index > 0 ? 0 : index, count = Math.Abs(index);
            double angle = phase + frequency * start;
            double sineSum = Math.Sin(angle + (count - 1) * frequency * .5) * Math.Sin(count * frequency * .5) / Math.Sin(frequency * .5);
            double total = count * mean + amplitude * sineSum;
            return index > 0 ? total : -total;
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

        private static bool Clip((double X, double Y) a, (double X, double Y) b,
            double left, double top, double right, double bottom, out (double X, double Y) p, out (double X, double Y) q)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, t0 = 0, t1 = 1;
            if (!ClipTest(-dx, a.X - left, ref t0, ref t1) || !ClipTest(dx, right - a.X, ref t0, ref t1) ||
                !ClipTest(-dy, a.Y - top, ref t0, ref t1) || !ClipTest(dy, bottom - a.Y, ref t0, ref t1))
            { p = default; q = default; return false; }
            p = (a.X + t0 * dx, a.Y + t0 * dy); q = (a.X + t1 * dx, a.Y + t1 * dy); return true;
        }

        private static bool ClipTest(double p, double q, ref double t0, ref double t1)
        {
            if (p == 0) return q >= 0;
            double r = q / p;
            if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }

        private static async Task StopWorkerAsync(Process process)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            try { await process.WaitForExitAsync().ConfigureAwait(false); } catch { }
        }
    }
}

