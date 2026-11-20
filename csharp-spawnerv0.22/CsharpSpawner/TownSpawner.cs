using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CsharpSpawner
{
    // Grows each saved population settlement into a deterministic town plan.
    public static class TownSpawner
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { IncludeFields = true };
        private static readonly string[] FamilySurnames = { "Bakker", "Brouwer", "De Jong", "De Vries", "Hofman", "Jansen", "Jorissen", "Klein", "Koster", "Meyer", "Mulder", "Schmidt", "Smit", "Van Dijk", "Van Wyk", "Visser", "Vogel", "Vos", "Wagner", "Wolfe", "Anderson", "Baker", "Brown", "Carter", "Clark", "Cooper", "Davis", "Harris", "Jackson", "Johnson", "Martin", "Miller", "Moore", "Robinson", "Smith", "Taylor", "Thomas", "Thompson", "White", "Wilson" };
        private sealed class RouteHit { public MapFeature Feature; public (double X, double Y) A, B, Point; public double Distance; }
        private sealed class PlannedLot
        {
            public int BlockRow,BlockCol;
            public string Id; public double LocalX, LocalY, Width, Depth, AxisX, AxisY, DepthX, DepthY, FrontX, FrontY, HouseFacingRadians;
            public (double X, double Y) Center, HouseCenter, GarageCenter;
            public List<MapPoint> Points, HousePoints, GaragePoints;
        }

        public static IReadOnlyList<string> FindTownLotClearanceViolations(IReadOnlyList<MapFeature> features)
        {
            var all=features??Array.Empty<MapFeature>();
            var lots=all.Where(f=>f.Kind=="town_lot"&&f.Points?.Count>=3).ToList();
            var lines=all.Where(f=>f.Kind is "main_road" or "local_road" or "town_street" or "rail" or "rivers").ToList();
            var areas=all.Where(f=>f.Kind is "lakes" or "town_civic_ground").ToList();
            var errors=new List<string>();
            for(int i=0;i<lots.Count;i++)
            {
                var lot=lots[i];
                if(lines.Any(line=>StrictLineHitsPolygon(line.Points,lot.Points))||areas.Any(area=>InteriorsOverlap(area.Points,lot.Points)))
                    errors.Add(lot.RouteId??$"lot:{i}:hits-infrastructure");
                for(int j=0;j<i;j++)if(InteriorsOverlap(lots[j].Points,lot.Points)){errors.Add(lot.RouteId??$"lot:{i}:overlaps-lot");break;}
            }
            return errors;
        }

        /// <summary>In-process town generation for one chunk, using already-saved upstream stages.</summary>
        public static TransportChunk GenerateChunk(int seed, TownInput input, IReadOnlyList<MapFeature> settlements,
            IReadOnlyList<MapFeature> rails, IReadOnlyList<MapFeature> roads, IReadOnlyList<MapFeature> water, IReadOnlyList<MapFeature> rural, WorldGenerationSettings settings = null,
            IReadOnlyList<Settler> settlers = null)
        {
            settings ??= new WorldGenerationSettings();
            var output = new TransportChunk { ChunkX = input.ChunkX, ChunkY = input.ChunkY };
            foreach (var site in (settlements ?? Array.Empty<MapFeature>()).Where(f => f.Kind == "settlement" && f.Points?.Count > 0)
                .Where(f => (int)Math.Floor(f.Points[0].X) == input.ChunkX && (int)Math.Floor(f.Points[0].Y) == input.ChunkY)
                .OrderBy(f => f.RouteId, StringComparer.Ordinal))
                AddTownForSite(output,site,rails,roads,water,rural,input.TerrainGrid,seed,settings,settlers);
            RemoveOverlappingBuildings(output);
            return output;
        }
        public static IReadOnlyList<WorldFeature> GenerateWorldFeatures(int seed, TownInput input, IReadOnlyList<MapFeature> settlements,
            IReadOnlyList<MapFeature> rails, IReadOnlyList<MapFeature> roads, IReadOnlyList<MapFeature> water, IReadOnlyList<MapFeature> rural, WorldGenerationSettings settings = null) =>
            LegacyMapFeatureConverter.ToWorldFeatures(GenerateChunk(seed,input,settlements,rails,roads,water,rural,settings).Features);

        public static async Task<int> RunWorkerAsync()
        {
            try
            {
                var request = JsonSerializer.Deserialize<TownRequest>(await Console.In.ReadToEndAsync().ConfigureAwait(false), JsonOptions)
                    ?? throw new InvalidOperationException("Missing town worker request.");
                var settlements = request.Settlements ?? new List<MapFeature>();
                var rails = request.RailFeatures ?? new List<MapFeature>();
                var roads = request.RoadFeatures ?? new List<MapFeature>();
                var water = request.WaterFeatures ?? new List<MapFeature>();
                var rural = request.RuralFeatures ?? new List<MapFeature>();
                var settlers = request.Settlers ?? new List<Settler>();
                var settings = request.Settings ?? new WorldGenerationSettings();
                using var stdout = new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                foreach (var input in request.Chunks ?? new List<TownInput>())
                {
                    var output = GenerateChunk(request.Seed, input, settlements, rails, roads, water, rural, settings, settlers);
                    await stdout.WriteLineAsync(JsonSerializer.Serialize(output, JsonOptions)).ConfigureAwait(false);
                }
                return 0;
            }
            catch (Exception ex) { await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false); return 1; }
        }

        private static void RemoveOverlappingBuildings(TransportChunk output)
        {
            var buildings = output.Features.Where(f => f.Kind is "town_building" or "town_garage" or "town_business" or "town_business_aux")
                .OrderBy(f => f.Kind == "town_business" ? 0 : f.Kind == "town_building" ? 1 : 2)
                .ThenBy(f => f.RouteId, StringComparer.Ordinal).ToList();
            var keep = new List<MapFeature>();
            foreach (var building in buildings)
                if (building.Points?.Count >= 3 && !keep.Any(other => PolygonsOverlap(building.Points, other.Points))) keep.Add(building);
            var keepIds = keep.Select(f => f.RouteId).ToHashSet(StringComparer.Ordinal);
            output.Features.RemoveAll(f => f.Kind is "town_building" or "town_garage" or "town_business" or "town_business_aux" && !keepIds.Contains(f.RouteId));
        }

        private static bool PolygonsOverlap(IReadOnlyList<MapPoint> a, IReadOnlyList<MapPoint> b)
        {
            if (a == null || b == null || a.Count < 3 || b.Count < 3) return false;
            return a.Any(p => Contains(b, p)) || b.Any(p => Contains(a, p)) ||
                Enumerable.Range(0, a.Count).Any(i => Enumerable.Range(0, b.Count).Any(j =>
                    SegmentsCross(a[i], a[(i + 1) % a.Count], b[j], b[(j + 1) % b.Count])));
        }
        private static bool Contains(IReadOnlyList<MapPoint> polygon, MapPoint point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
                if ((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y) &&
                    point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X) inside = !inside;
            return inside;
        }
        private static bool SegmentsCross(MapPoint a, MapPoint b, MapPoint c, MapPoint d)
        {
            double Cross(MapPoint p, MapPoint q, MapPoint r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
            bool On(MapPoint p, MapPoint q, MapPoint r) => r.X >= Math.Min(p.X, q.X) - 1e-10 && r.X <= Math.Max(p.X, q.X) + 1e-10 &&
                r.Y >= Math.Min(p.Y, q.Y) - 1e-10 && r.Y <= Math.Max(p.Y, q.Y) + 1e-10;
            double x = Cross(a, b, c), y = Cross(a, b, d), u = Cross(c, d, a), v = Cross(c, d, b);
            if (((x > 0 && y < 0) || (x < 0 && y > 0)) && ((u > 0 && v < 0) || (u < 0 && v > 0))) return true;
            return Math.Abs(x) < 1e-10 && On(a, b, c) || Math.Abs(y) < 1e-10 && On(a, b, d) ||
                Math.Abs(u) < 1e-10 && On(c, d, a) || Math.Abs(v) < 1e-10 && On(c, d, b);
        }

        private static void AddTownForSite(TransportChunk output,MapFeature site,IReadOnlyList<MapFeature> rails,
            IReadOnlyList<MapFeature> roads,IReadOnlyList<MapFeature> water,IReadOnlyList<MapFeature> rural,
            ElevationGridData terrain,int seed,WorldGenerationSettings settings,IReadOnlyList<Settler> settlers)
        {
            var point=site.Points[0];
            var localRails=FeaturesNear(rails,point,2);var localRoads=FeaturesNear(roads,point,3);
            var localWater=FeaturesNear(water,point,1);var localRural=FeaturesNear(rural,point,1.5);
            var residents=(settlers??Array.Empty<Settler>()).Where(p=>p.ClusterId==site.RouteId).OrderBy(p=>p.Id,StringComparer.Ordinal).ToList();
            int minimumBlocks=4;
            TransportChunk plan=null;
            for(int attempt=0;attempt<8;attempt++)
            {
                plan=new TransportChunk{ChunkX=output.ChunkX,ChunkY=output.ChunkY};
                AddTownV2(plan,site,localRails,localRoads,localWater,localRural,terrain,seed,settings,residents,minimumBlocks);
                RemoveOverlappingBuildings(plan);
                int capacity=plan.Features.Where(f=>f.Kind=="town_building").Sum(f=>f.HousingSlots);
                int demand=residents.Count>0?residents.Count:Math.Max(24,site.Population);
                if(capacity>=demand)break;
                int builtBlocks=plan.Features.Where(f=>f.Kind=="town_lot").Select(f=>string.Join(":",f.RouteId.Split(':').SkipLast(2))).Distinct().Count();
                minimumBlocks=Math.Max(minimumBlocks+4,(int)Math.Ceiling(Math.Max(minimumBlocks,builtBlocks)*Math.Min(2.0,demand/(double)Math.Max(1,capacity))*1.15));
            }
            output.Features.AddRange(plan.Features);
            output.RemovedRuralLotIds.AddRange(plan.RemovedRuralLotIds);
            output.RemovedRuralFeatureIds.AddRange(plan.RemovedRuralFeatureIds);
        }

        private static List<MapFeature> FeaturesNear(IReadOnlyList<MapFeature> features,MapPoint point,double radius)
        {
            double left=point.X-radius,right=point.X+radius,bottom=point.Y-radius,top=point.Y+radius;
            return (features??Array.Empty<MapFeature>()).Where(feature=>feature.Points?.Count>0&&
                feature.Points.Max(p=>p.X)>=left&&feature.Points.Min(p=>p.X)<=right&&
                feature.Points.Max(p=>p.Y)>=bottom&&feature.Points.Min(p=>p.Y)<=top).ToList();
        }

        private static void AddTownV2(TransportChunk output, MapFeature site, IReadOnlyList<MapFeature> rails,
            IReadOnlyList<MapFeature> roads, IReadOnlyList<MapFeature> water, IReadOnlyList<MapFeature> rural,
            ElevationGridData terrain, int seed, WorldGenerationSettings settings,IReadOnlyList<Settler> residents,int minimumBlocks=4)
        {
            var anchor = site.Points[0]; string id = site.RouteId ?? $"{anchor.X:R}:{anchor.Y:R}";
            ulong hash = Hash(seed, id + ":town-plan");
            var rail = FindNearestSegment((anchor.X, anchor.Y), rails, f => f.Kind == "rail");
            var arterial = FindNearestSegment((anchor.X, anchor.Y), roads, f => f.Kind == "main_road" || (f.Kind == "local_road" && f.Surface == "paved"));
            bool nearRail = rail != null && rail.Distance <= 1.5;
            string railPassThrough = nearRail ? RailPassThrough(seed, id) : "none";
            bool cornerTown = false;
            if (!nearRail && !site.IsMajorTown && hash % 2 == 0 && TryNearbyCountyCorner((anchor.X, anchor.Y), roads, out var corner))
            { anchor = new MapPoint { X = corner.X, Y = corner.Y }; cornerTown = true; }

            // Align the connected block network to the existing through road.
            double ux = 1, uy = 0;
            if (!cornerTown && arterial != null && arterial.Distance <= 1.5) { ux = arterial.B.X - arterial.A.X; uy = arterial.B.Y - arterial.A.Y; }
            if(nearRail&&rail!=null&&(arterial==null||arterial.Distance>1.5))
            {
                // Follow the corridor's heading, quantized to avoid clipped-segment
                // noise rotating an entire town when a neighboring chunk loads.
                double heading=Math.Atan2(rail.B.Y-rail.A.Y,rail.B.X-rail.A.X);
                heading=Math.Round(heading/(Math.PI/12))*(Math.PI/12);
                ux=Math.Cos(heading);uy=Math.Sin(heading);
            }
            double norm = Math.Sqrt(ux * ux + uy * uy); if (norm > 1e-9) { ux /= norm; uy /= norm; }
            double vx = -uy, vy = ux;
            int population = Math.Max(24, site.Population);
            if (nearRail) population = (int)Math.Ceiling(population * 1.35);
            if (site.IsMajorTown) population = (int)Math.Ceiling(population * 1.25);
            int householdCapacity=Math.Max(1,settings.HomeOccupancyPercent);
            int peopleToHouse=residents!=null&&residents.Count>0?residents.Count:population;
            // Homes hold two or four people. Reserve a little room for civic and
            // commercial frontages and sites lost to terrain or clearance.
            int homeLots=(int)Math.Ceiling(peopleToHouse*100.0/(2.5*householdCapacity));
            int blocks=Math.Max(minimumBlocks,(int)Math.Ceiling(homeLots/10.0));
            var activeBlocks=BuildTownBlockGrowth(blocks,seed,id,out int columns,out int rows);
            
            var streetsU=TownStreetPositions(seed,id,columns,"u");
            var streetsV=TownStreetPositions(seed,id,rows,"v");
            double width=2*Math.Max(-streetsU[0],streetsU[^1]),height=2*Math.Max(-streetsV[0],streetsV[^1]);
            // The arterial is the towns through street. Put it on a block
            // edge so the grid crosses it at real intersections instead of
            // placing the entire town beside it or running it through lots.
            if(!cornerTown&&arterial!=null&&arterial.Distance<=1.5)
            {
                anchor=new MapPoint{X=arterial.Point.X,Y=arterial.Point.Y};
            }
            // Keep entire street blocks out of standing water. Rail and river
            // corridors instead trim individual rear parcels and receive crossings.
            activeBlocks.RemoveAll(cell=>
            {
                var center=World(anchor,ux,uy,vx,vy,(streetsU[cell.Col]+streetsU[cell.Col+1])*.5,(streetsV[cell.Row]+streetsV[cell.Row+1])*.5);
                var polygon=RectCorners(center.X,center.Y,ux,uy,vx,vy,streetsU[cell.Col+1]-streetsU[cell.Col],streetsV[cell.Row+1]-streetsV[cell.Row]);
                return (water??Array.Empty<MapFeature>()).Any(f=>f.Kind=="lakes"&&PolygonOverlap(polygon,f.Points));
            });
            string townName = site.Name ?? string.Empty;
            bool railCrossing=nearRail&&arterial!=null&&RoutesIntersectNear(rail,arterial,(anchor.X,anchor.Y),Math.Max(width,height)*.75);
            string plan = cornerTown ? "county-corner" : railCrossing ? "rail-crossing" : nearRail ? "rail-cardinal" : "main-street";
            const double exteriorRoadOffset = 170.0 / 5280.0;
            var outline = BuildTownOutline(anchor,ux,uy,vx,vy,width+2*exteriorRoadOffset,height+2*exteriorRoadOffset,
                rails,roads,water,hash);
            var townFeature = new MapFeature { Kind = "town", RouteId = site.RouteId, Carrier = plan, Name = townName,
                Population = population, IsMajorTown = site.IsMajorTown || nearRail, RailPassThrough = railPassThrough,
                Width = width + 2 * exteriorRoadOffset, Height = height + 2 * exteriorRoadOffset, Closed=true,Points = outline };
            output.Features.Add(townFeature);
            var commerce=PickBusinesses(seed,id,population,settings);
            if(nearRail)commerce.Remove("Grain Elevator");

            foreach (var ruralLot in (rural ?? Array.Empty<MapFeature>()).Where(f => f.Kind == "rural_lot" && f.Points?.Count >= 3))
                if (PolygonOverlap(outline, ruralLot.Points) && !string.IsNullOrEmpty(ruralLot.RouteId)) output.RemovedRuralLotIds.Add(ruralLot.RouteId);
            foreach(var entrance in (rural??Array.Empty<MapFeature>()).Where(f=>f.Kind=="field_entrance"&&f.Points?.Count>=2))
                if(PolylineIntersectsPolygon(entrance.Points,outline))output.RemovedRuralFeatureIds.Add(entrance.RouteId);
            foreach(var yard in (rural??Array.Empty<MapFeature>()).Where(f=>f.Kind=="cattle_yard"&&f.Points?.Count>=3))
                if(PolygonOverlap(outline,yard.Points)&&!string.IsNullOrEmpty(yard.RouteId))output.RemovedRuralFeatureIds.Add(yard.RouteId);

            var streetEdges=new HashSet<string>(StringComparer.Ordinal);
            foreach(var cell in activeBlocks)
            {
                double left=streetsU[cell.Col],right=streetsU[cell.Col+1],bottom=streetsV[cell.Row],top=streetsV[cell.Row+1];
                void Edge(string key,(double X,double Y) a,(double X,double Y) b,string name=null,double roadWidth=.008)
                {
                    if(!streetEdges.Add(key))return;
                    AddPavedTownStreet(output,id+":street:"+key,a,b,name,roadWidth,roads);
                    foreach(var river in water??Array.Empty<MapFeature>())
                    for(int segment=0;river.Kind=="rivers"&&segment+1<river.Points.Count;segment+=2)
                        if(TryIntersection(a,b,(river.Points[segment].X,river.Points[segment].Y),(river.Points[segment+1].X,river.Points[segment+1].Y),out var crossing))
                        {
                            double length=Distance(a,b),half=Math.Max(river.Width*.75,20.0/5280);
                            double dx=(b.X-a.X)/length,dy=(b.Y-a.Y)/length;
                            output.Features.Add(new MapFeature{Kind="bridges",RouteId=id+":street:"+key+":bridge:"+segment,Carrier="town_street",Width=roadWidth,
                                Points=SegmentPoints(new[]{(crossing.X-dx*half,crossing.Y-dy*half),(crossing.X+dx*half,crossing.Y+dy*half)})});
                        }
                }
                Edge($"v:{cell.Col}:{cell.Row}",World(anchor,ux,uy,vx,vy,left,bottom),World(anchor,ux,uy,vx,vy,left,top),null,.008);
                Edge($"v:{cell.Col+1}:{cell.Row}",World(anchor,ux,uy,vx,vy,right,bottom),World(anchor,ux,uy,vx,vy,right,top),null,.008);
                Edge($"h:{cell.Row}:{cell.Col}",World(anchor,ux,uy,vx,vy,left,bottom),World(anchor,ux,uy,vx,vy,right,bottom),null,.008);
                Edge($"h:{cell.Row+1}:{cell.Col}",World(anchor,ux,uy,vx,vy,left,top),World(anchor,ux,uy,vx,vy,right,top),null,.008);
            }
            // The town boundary is a land-use edge, not a ring road. Streets
            // terminate at the last built block and only grow with new blocks.
            AddTownRoadConnections(output,id,roads,anchor,ux,uy,vx,vy);
            AddTownAlleys(output,id,anchor,ux,uy,vx,vy,columns,rows,seed,settings,streetsU,streetsV,activeBlocks);
            // Streets grow with blocks; speculative boulevards must not cut across parcels.
            if (nearRail)
            {
                var depot = ClosestPoint(rail.A, rail.B, (anchor.X, anchor.Y));
                output.Features.Add(new MapFeature { Kind = "town_depot", RouteId = id + ":depot", Name = "Depot",
                    Points = new List<MapPoint> { new MapPoint { X = depot.X, Y = depot.Y } } });
                if (railPassThrough == "industrial") AddRailIndustrialGround(output, id, depot, rail.A, rail.B, width, height, hash);
            }

            AddCivicGrounds(output,id,anchor,ux,uy,vx,vy,width,height,columns,rows,activeBlocks,population,seed,settings,streetsU,streetsV);
            output.Features.RemoveAll(f=>f.Kind=="town_alley"&&(
                (rails??Array.Empty<MapFeature>()).Concat(water??Array.Empty<MapFeature>()).Any(c=>c.Kind=="lakes"?PolylineIntersectsPolygon(f.Points,c.Points):
                    c.Kind is "rail" or "rivers"&&LineSegmentsCross(f.Points,c.Points))||
                output.Features.Any(c=>c.Kind=="town_civic_ground"&&PolylineIntersectsPolygon(f.Points,c.Points))));
            var extraRoads=output.Features.Where(f=>f.Kind=="town_street"&&f.RouteId?.StartsWith(id+":extra:",StringComparison.Ordinal)==true).ToList();
            var lots = BuildLots(seed, id, anchor, ux, uy, vx, vy, columns, rows, activeBlocks, terrain, water,outline,extraRoads,rails,streetsU,streetsV);

            var civicGrounds=output.Features.Where(f=>f.Kind=="town_civic_ground"&&f.RouteId?.StartsWith(id+":civic:",StringComparison.Ordinal)==true).ToList();
            lots.RemoveAll(lot=>civicGrounds.Any(ground=>PolygonOverlap(lot.Points,ground.Points)));
            var townStreets=output.Features.Where(f=>f.Kind=="town_street").ToList();
            lots=FilterClearLots(lots,roads,townStreets,water,rails,civicGrounds);

            // Reserve the most central road-frontage parcels for businesses
            // before residential lots are emitted.
            var businessNodes = FindBusinessNodes(roads,townStreets);
            var frontLots = lots.OrderBy(l => BusinessLotScore(l,roads,businessNodes))
                .ThenBy(l => Math.Round(l.LocalX*l.LocalX+l.LocalY*l.LocalY,9))
                .ThenBy(l => l.Id, StringComparer.Ordinal).ToList();
            var downtownCommerce=new List<string>(); var stripCommerce=new List<string>();
            var nearbyPavedRoad=FindNearestSegment((anchor.X,anchor.Y),roads,
                f=>f.Kind=="main_road"||(f.Kind=="local_road"&&f.Surface=="paved"));
            foreach(var type in commerce)
            {
                bool stripType=type is "Gas Station" or "Implement Dealer" or "Farm Supply" or "Lumberyard" or "Auto Repair" or "Motel";
                bool sendStrip=stripType&&nearbyPavedRoad!=null&&nearbyPavedRoad.Distance<=settings.TownPavedStripSearchMiles&&
                    Hash(seed,id+":strip:"+type)%100 >= (ulong)settings.TownDowntownBusinessPercent;
                if(sendStrip)stripCommerce.Add(type);else downtownCommerce.Add(type);
            }
            var pavedStripLots=BuildHighwayStripLots(seed,id,anchor,nearbyPavedRoad,stripCommerce.Count,terrain,water);
            if(pavedStripLots.Count<stripCommerce.Count)
            {downtownCommerce.AddRange(stripCommerce.Skip(pavedStripLots.Count));stripCommerce=stripCommerce.Take(pavedStripLots.Count).ToList();}
            var civicBusinessTypes=downtownCommerce.Where(IsCivicBusiness).ToList();
            downtownCommerce.RemoveAll(IsCivicBusiness);
            var businessPlacements=downtownCommerce.Select((type,index)=>(Type:type,Lot:frontLots.ElementAtOrDefault(index)))
                .Where(x=>x.Lot!=null).ToList();
            var stripPlacements=stripCommerce.Select((type,index)=>(Type:type,Lot:pavedStripLots.ElementAtOrDefault(index)))
                .Where(x=>x.Lot!=null).ToList();
            var usedCommercialLots=new HashSet<string>(businessPlacements.Concat(stripPlacements).Select(p=>p.Lot.Id),StringComparer.Ordinal);
            var civicLots=FindCivicFrontLots(lots,columns,rows,width,height)
                .Where(lot=>!usedCommercialLots.Contains(lot.Id)).ToList();
            var civicPlacements=civicBusinessTypes.Select((type,index)=>(Type:type,Lot:civicLots.ElementAtOrDefault(index)))
                .Where(x=>x.Lot!=null).ToList();
            foreach(var placement in businessPlacements.Concat(stripPlacements))
            {
                var lot=placement.Lot; string lotId=id+":business-lot:"+placement.Type+":"+lot.Id;
                output.Features.Add(new MapFeature { Kind = "town_business_lot", RouteId = lotId, Name = placement.Type, Points = lot.Points });
                AddCommercialBuildings(output, lotId, placement.Type, lot);
            }
            foreach(var placement in civicPlacements)
            {
                var lot=placement.Lot; string lotId=id+":civic-lot:"+placement.Type+":"+lot.Id;
                output.Features.Add(new MapFeature { Kind = "town_civic_lot", RouteId = lotId, Name = placement.Type, Points = lot.Points });
                AddCommercialBuildings(output, lotId, placement.Type, lot, civic:true);
            }
            var commercialIds = new HashSet<string>(businessPlacements.Concat(stripPlacements).Concat(civicPlacements).Select(p=>p.Lot.Id), StringComparer.Ordinal);
            var homeCandidates = lots.Where(l=>!commercialIds.Contains(l.Id))
                .GroupBy(lot=>lot.Id,StringComparer.Ordinal).Select(group=>group.First()).ToList();
            var residentHomes = new List<(PlannedLot Lot, int Slots)>();
            int assignedCapacity = 0;
            foreach (var lot in homeCandidates.OrderBy(lot=>Distance(lot.Center,(anchor.X,anchor.Y)) + (Hash(seed,lot.Id+":occupied")%1000)/1000.0*.035).ThenBy(lot=>lot.Id,StringComparer.Ordinal))
            {
                int slots = (Hash(seed, lot.Id + ":house-size") % 4 == 0) ? 4 : 2;
                residentHomes.Add((lot, slots));
                assignedCapacity += slots;
                if (assignedCapacity >= peopleToHouse) break;
            }
            var occupiedLotIds=residentHomes.Select(home=>home.Lot.Id).ToHashSet(StringComparer.Ordinal);
            var residentsByLot = new Dictionary<string,List<Settler>>(StringComparer.Ordinal);
            var slotCapacityByLot = new Dictionary<string,int>(StringComparer.Ordinal);
            int personIndex=0;
            foreach (var home in residentHomes)
            {
                var assigned = residents.Skip(personIndex).Take(home.Slots).ToList();
                personIndex += home.Slots;
                residentsByLot[home.Lot.Id] = assigned;
                slotCapacityByLot[home.Lot.Id] = home.Slots;
            }
            int surnameIndex=0;
            AddTownSidewalks(output,id,seed,settings,commerce.Contains("School"));
            int occupiedHomes=0;
            foreach (var lot in homeCandidates)
            {
                string family = residents.Count > 0 ? ResidentSurname(residents[surnameIndex++ % residents.Count].Name) : FamilyName(seed, lot.Id);
                output.Features.Add(new MapFeature { Kind = "town_lot", RouteId = lot.Id, Name = family, Carrier = "town-house", Points = lot.Points });
                if (occupiedLotIds.Contains(lot.Id))
                {
                    occupiedHomes++;
                    List<Settler> household=residentsByLot.TryGetValue(lot.Id,out var assignedResidents)?assignedResidents:new List<Settler>();
                    output.Features.Add(new MapFeature { Kind = "town_building", RouteId = lot.Id + ":house", Carrier = lot.Id, Name = family,
                        Points = lot.HousePoints, Width = lot.Width * .7, Height = lot.Depth * .34,
                        BuildingArchetype = HouseArchetype(lot.Id, seed), BuildingPlanSeed = HousePlanSeed(lot.Id, seed), HousingSlots = slotCapacityByLot.TryGetValue(lot.Id,out var capacity) ? capacity : 0,
                        FacingRadians = lot.HouseFacingRadians, IsOccupied = household.Count > 0 });
                    foreach(var person in household)
                        output.Features.Add(new MapFeature{Kind="town_person",RouteId=person.Id,Carrier=lot.Id,Name=person.Name,
                            Points=new List<MapPoint>{new(){X=lot.HouseCenter.X,Y=lot.HouseCenter.Y}}});
                    if (lot.GaragePoints != null && Hash(seed, lot.Id + ":garage") % 100 < (ulong)settings.GarageChancePercent)
                        output.Features.Add(new MapFeature { Kind = "town_garage", RouteId = lot.Id + ":garage", Carrier = lot.Id,
                            Points = lot.GaragePoints, Width = 20.0 / 5280.0, Height = 22.0 / 5280.0 });
                    AddTownDriveway(output,lot,roads);
                    AddDoorWalk(output,lot.Id,lot,seed,settings);
                }
            }
            townFeature.Population = peopleToHouse;
        }

        private static string RailPassThrough(int seed, string settlementId)
        {
            ulong roll = Hash(seed, settlementId + ":rail-pass-through") % 100;
            return roll < 70 ? "industrial" : roll < 95 ? "commercial" : "residential";
        }

        private static List<MapPoint> BuildTownOutline(MapPoint anchor,double ux,double uy,double vx,double vy,double width,double height,
            IReadOnlyList<MapFeature> rails,IReadOnlyList<MapFeature> roads,IReadOnlyList<MapFeature> water,ulong hash)
        {
            var rectangle=RectCorners(anchor.X,anchor.Y,ux,uy,vx,vy,width,height);
            bool disrupted=(rails??Array.Empty<MapFeature>()).Any(f=>f.Kind=="rail"&&PolylineIntersectsPolygon(f.Points,rectangle))||
                (roads??Array.Empty<MapFeature>()).Any(f=>(f.Kind=="main_road"||f.Kind=="local_road"&&f.Surface=="paved")&&PolylineIntersectsPolygon(f.Points,rectangle))||
                (water??Array.Empty<MapFeature>()).Any(f=>f.Kind=="rivers"&&PolylineIntersectsPolygon(f.Points,rectangle)||
                    f.Kind=="lakes"&&f.Points?.Count>=3&&PolygonOverlap(rectangle,f.Points));
            if(!disrupted)return rectangle;
            // Chamfered corners make a saved, non-rectangular outline where a
            // corridor enters the town footprint, leaving the edge streets to
            // be rebuilt along the altered boundary below.
            double trim=Math.Min(Math.Min(width,height)*.16,.045);
            var outline=new List<MapPoint>();
            for(int i=0;i<rectangle.Count;i++)
            {
                var prev=rectangle[(i+rectangle.Count-1)%rectangle.Count];var current=rectangle[i];var next=rectangle[(i+1)%rectangle.Count];
                double prevLength=Distance((current.X,current.Y),(prev.X,prev.Y));
                double nextLength=Distance((current.X,current.Y),(next.X,next.Y));
                double a=Math.Min(trim,prevLength*.28),b=Math.Min(trim,nextLength*.28);
                outline.Add(new MapPoint{X=current.X+(prev.X-current.X)*a/prevLength,Y=current.Y+(prev.Y-current.Y)*a/prevLength});
                outline.Add(new MapPoint{X=current.X+(next.X-current.X)*b/nextLength,Y=current.Y+(next.Y-current.Y)*b/nextLength});
            }
            return outline;
        }

        private static bool RoutesIntersectNear(RouteHit rail,RouteHit road,(double X,double Y) anchor,double radius)
        {
            if(rail==null||road==null||!TryIntersection(rail.A,rail.B,road.A,road.B,out var crossing))return false;
            return Distance(crossing,anchor)<=radius;
        }

        private static List<PlannedLot> BuildHighwayStripLots(int seed,string townId,MapPoint anchor,RouteHit road,int count,
            ElevationGridData terrain,IReadOnlyList<MapFeature> water)
        {
            var lots=new List<PlannedLot>();if(road==null||count<=0)return lots;
            double dx=road.B.X-road.A.X,dy=road.B.Y-road.A.Y,length=Math.Sqrt(dx*dx+dy*dy);if(length<1e-9)return lots;
            double ux=dx/length,uy=dy/length,vx=-uy,vy=ux,side=(Hash(seed,townId+":strip-side")&1)==0?1:-1;
            double frontage=68.0/5280.0,depth=180.0/5280.0;
            for(int i=0;i<count;i++)
            {
                double along=(i-(count-1)*.5)*(frontage+12.0/5280.0);
                var center=(X:road.Point.X+ux*along+vx*side*(.035+depth*.5),Y:road.Point.Y+uy*along+vy*side*(.035+depth*.5));
                if(!SuitableLot(terrain,water,center.X,center.Y))continue;
                var points=RectCorners(center.X,center.Y,ux,uy,vx,vy,frontage,depth);
                double houseWidth=frontage*.68,houseDepth=depth*.42;
                var house=(X:center.X-vx*side*depth*.08,Y:center.Y-vy*side*depth*.08);
                lots.Add(new PlannedLot{Id=$"{townId}:strip-lot:{i}",Center=center,Points=points,Width=frontage,Depth=depth,
                    AxisX=ux,AxisY=uy,DepthX=vx,DepthY=vy,FrontX=-vx*side,FrontY=-vy*side,
                    HouseCenter=house,HousePoints=RectCorners(house.X,house.Y,ux,uy,vx,vy,houseWidth,houseDepth)});
            }
            return lots;
        }

        private static void AddTownAlleys(TransportChunk output,string id,MapPoint anchor,double ux,double uy,double vx,double vy,
            int columns,int rows,int seed,WorldGenerationSettings settings,double[] streetsU,double[] streetsV,IReadOnlyList<(int Row,int Col)> activeBlocks)
        {
            foreach(var cell in activeBlocks)
            {
                int row=cell.Row,col=cell.Col;
                AddAlley(row,col);
            }
            void AddAlley(int row,int col)
            {
                if(Hash(seed,$"{id}:alley:{row}:{col}")%100>=(ulong)settings.TownAlleyChancePercent)return;
                double cx=(streetsU[col]+streetsU[col+1])*.5,cy=(streetsV[row]+streetsV[row+1])*.5;
                double halfU=(streetsU[col+1]-streetsU[col])*.5,halfV=(streetsV[row+1]-streetsV[row])*.5;
                bool alongU=(Hash(seed,$"{id}:parcel-axis:{row-rows/2}:{col-columns/2}")&1)==0;
                var a=alongU?World(anchor,ux,uy,vx,vy,cx-halfU,cy):World(anchor,ux,uy,vx,vy,cx,cy-halfV);
                var b=alongU?World(anchor,ux,uy,vx,vy,cx+halfU,cy):World(anchor,ux,uy,vx,vy,cx,cy+halfV);
                output.Features.Add(new MapFeature{Kind="town_alley",RouteId=$"{id}:alley:{row}:{col}",Name="Paved Alley",Surface="paved",
                    Width=16.0/5280.0,Points=SegmentPoints(new[]{a,b})});
            }
        }

        private static void AddExtraStreets(TransportChunk output,string id,MapPoint anchor,double ux,double uy,double vx,double vy,
            double width,double height,int columns,int rows,int seed,WorldGenerationSettings settings,IReadOnlyList<MapPoint> outline,IReadOnlyList<MapFeature> rails,IReadOnlyList<MapFeature> roads)
        {
            if(settings.TownMaxExtraStreets<=0||Hash(seed,id+":extras:none")%100<(ulong)settings.TownNoExtraStreetPercent)return;
            int count=1+(int)(Hash(seed,id+":extras:count")%(ulong)settings.TownMaxExtraStreets);
            int deadEndCap=columns*rows<=4?0:columns*rows<=6?1:columns*rows<=9?2:3,deadEnds=0;
            string[] types={"BOULEVARD"};
            for(int i=0;i<count;i++)
            {
                string type=types[Hash(seed,$"{id}:extra:type:{i}")%(ulong)types.Length];
                bool deadEnd=type is "COURT" or "PLAT EDGE" or "OUTLYING ROAD";
                if(deadEnd&&deadEnds>=deadEndCap)type="BOULEVARD";else if(deadEnd)deadEnds++;
                var route=new List<(double X,double Y)>();
                if(type=="OLD RAIL")
                {route.Add(World(anchor,ux,uy,vx,vy,-width*.5,-height*.5));route.Add(World(anchor,ux,uy,vx,vy,width*.5,height*.5));}
                else if(type=="BOULEVARD")
                {double y=(Hash(seed,id+":extra:y:"+i)&1)==0?0:height*.18;route.Add(World(anchor,ux,uy,vx,vy,-width*.5,y));route.Add(World(anchor,ux,uy,vx,vy,width*.5,y));}
                else if(type=="CRESCENT")
                {
                    double radius=Math.Min(width,height)*.28,cx=width*.5-radius,cy=height*.5-radius;
                    for(int n=0;n<=8;n++){double angle=(n/8.0)*Math.PI*.5;route.Add(World(anchor,ux,uy,vx,vy,cx+Math.Cos(angle)*radius,cy+Math.Sin(angle)*radius));}
                }
                else if(type=="BYPASSED HIGHWAY")
                {
                    double y=-height*.5-.075;
                    route.Add(World(anchor,ux,uy,vx,vy,-width*.5,-height*.5));
                    route.Add(World(anchor,ux,uy,vx,vy,-width*.5-.075,y));
                    route.Add(World(anchor,ux,uy,vx,vy,width*.5+.075,y));
                    route.Add(World(anchor,ux,uy,vx,vy,width*.5,-height*.5));
                }
                else
                {
                    int side=(int)(Hash(seed,id+":extra:side:"+i)%4);double t=((Hash(seed,id+":extra:position:"+i)%1001)/1000.0-.5)*.65;
                    (double X,double Y) attach=side switch {0=>World(anchor,ux,uy,vx,vy,t*width,-height*.5),1=>World(anchor,ux,uy,vx,vy,width*.5,t*height),2=>World(anchor,ux,uy,vx,vy,t*width,height*.5),_=>World(anchor,ux,uy,vx,vy,-width*.5,t*height)};
                    (double X,double Y) outward=side switch {0=>(-vx,-vy),1=>(ux,uy),2=>(vx,vy),_=>(-ux,-uy)};
                    var inside=World(anchor,ux,uy,vx,vy,side==1?width*.5-.03:side==3?-width*.5+.03:t*width,
                        side==0?-height*.5+.03:side==2?height*.5-.03:t*height);
                    route.Add(inside);route.Add(attach);
                    double extension=type=="OUTLYING ROAD"?.30:type=="COURT"?.12:type=="PLAT EDGE"?.04:.24;
                    route.Add((attach.X+outward.Item1*extension,attach.Y+outward.Item2*extension));
                    if(type=="COURT")output.Features.Add(new MapFeature{Kind="town_court_bulb",RouteId=$"{id}:extra:{i}:bulb",Width=settings.TownDeadEndBulbWidthFeet/5280.0,
                        Points=new List<MapPoint>{new(){X=route[^1].X,Y=route[^1].Y}}});

                }
                if(!HasSafeRailCrossings(route,rails))continue;
                foreach(var segment in Enumerable.Range(0,route.Count-1))
                    AddPavedTownStreet(output,$"{id}:extra:{i}:{segment}",route[segment],route[segment+1],type,type=="BOULEVARD"?.016:.008,roads);
                if(type=="BOULEVARD")
                    foreach(var p in route.Count>1?Enumerable.Range(0,7).Select(n=>(X:route[0].X+(route[^1].X-route[0].X)*n/6.0,Y:route[0].Y+(route[^1].Y-route[0].Y)*n/6.0)):Enumerable.Empty<(double X,double Y)>())
                        output.Features.Add(new MapFeature{Kind="town_median_tree",RouteId=$"{id}:extra:{i}:tree:{p.X:R}",Points=new List<MapPoint>{new(){X=p.X,Y=p.Y}}});
                if(type=="BYPASSED HIGHWAY")AddBypassedHighwayShops(output,id,i,route,seed);
            }
        }

        private static bool HasSafeRailCrossings(IReadOnlyList<(double X,double Y)> road,IReadOnlyList<MapFeature> rails)
        {
            if(road==null||road.Count<2)return true;
            for(int r=0;r+1<road.Count;r++)
            foreach(var rail in rails??Array.Empty<MapFeature>())
            {
                if(rail.Kind!="rail"||rail.Points==null)continue;
                for(int i=0;i+1<rail.Points.Count;i+=2)
                {
                    var a=(rail.Points[i].X,rail.Points[i].Y);var b=(rail.Points[i+1].X,rail.Points[i+1].Y);
                    if(!TryIntersection(road[r],road[r+1],a,b,out _))continue;
                    double rx=road[r+1].X-road[r].X,ry=road[r+1].Y-road[r].Y,tx=b.X-a.X,ty=b.Y-a.Y;
                    double denom=Math.Sqrt((rx*rx+ry*ry)*(tx*tx+ty*ty));if(denom<1e-12)return false;
                    double angle=Math.Acos(Math.Clamp(Math.Abs((rx*tx+ry*ty)/denom),0,1))*180/Math.PI;
                    if(angle<75)return false;
                }
            }
            return true;
        }

        private static void AddBypassedHighwayShops(TransportChunk output,string id,int extra,List<(double X,double Y)> route,int seed)
        {
            if(route.Count<3)return;var start=route[1];var end=route[2];double dx=end.X-start.X,dy=end.Y-start.Y,len=Math.Sqrt(dx*dx+dy*dy);if(len<1e-9)return;
            double ux=dx/len,uy=dy/len,vx=-uy,vy=ux;
            string[] types={"Gas Station","Farm Supply","Lumberyard","Auto Repair","Motel"};
            for(int i=0;i<2;i++)
            {
                var center=(X:start.X+dx*(.35+i*.3)+vx*.035,Y:start.Y+dy*(.35+i*.3)+vy*.035);
                var lot=RectCorners(center.X,center.Y,ux,uy,vx,vy,70.0/5280.0,180.0/5280.0);
                string type=types[Hash(seed,$"{id}:bypass-shop:{extra}:{i}")%5];string lotId=$"{id}:bypass-shop:{extra}:{i}";
                output.Features.Add(new MapFeature{Kind="town_business_lot",RouteId=lotId,Name=type,Points=lot});
                output.Features.Add(new MapFeature{Kind="town_business",RouteId=lotId+":building",Name=type,Carrier="commercial",
                    Points=RectCorners(center.X,center.Y,ux,uy,vx,vy,36.0/5280.0,52.0/5280.0)});
            }
        }

        private static void AddTownSidewalks(TransportChunk output,string id,int seed,WorldGenerationSettings settings,bool hasSchool)
        {
            var streets=(output.Features??new List<MapFeature>()).Where(f=>f.Kind=="town_street"&&f.Points?.Count>=2).ToList();
            foreach(var road in streets)
            {
                if(road.Points.Count<2)continue;
                bool both=road.Name=="Main Street"||hasSchool&&road.Name=="School Street";
                ulong hash=Hash(seed,id+":sidewalk:"+road.RouteId);int roll=(int)(hash%100);
                bool keepA=both||roll>=40,keepB=both||roll>=40;
                if(!both&&roll<40){keepA=(hash&1)==0;keepB=!keepA;}
                for(int segment=0;segment+1<road.Points.Count;segment+=2)
                {
                    if(keepA)AddParallelSidewalk(output,road,segment,1,settings);
                    if(keepB)AddParallelSidewalk(output,road,segment,-1,settings);
                }
            }
        }

        private static void AddParallelSidewalk(TransportChunk output,MapFeature road,int segment,double side,WorldGenerationSettings settings)
        {
            double dx=road.Points[segment+1].X-road.Points[segment].X,dy=road.Points[segment+1].Y-road.Points[segment].Y,len=Math.Sqrt(dx*dx+dy*dy);if(len<1e-9)return;
            double offset=(road.Width*.5+(settings.TownSidewalkWidthFeet*.5-settings.TownSidewalkInsetFeet)/5280.0)*side;
            double nx=-dy/len,ny=dx/len;
            var a=(X:road.Points[segment].X+nx*offset,Y:road.Points[segment].Y+ny*offset);var b=(X:road.Points[segment+1].X+nx*offset,Y:road.Points[segment+1].Y+ny*offset);
            output.Features.Add(new MapFeature{Kind="town_sidewalk",RouteId=road.RouteId+":sidewalk:"+segment+":"+(side>0?"a":"b"),Width=settings.TownSidewalkWidthFeet/5280.0,
                Points=SegmentPoints(new[]{a,b})});
        }

        private static void AddDoorWalk(TransportChunk output,string lotId,PlannedLot lot,int seed,WorldGenerationSettings settings)
        {
            var house=output.Features.First(f=>f.Kind=="town_building"&&f.Carrier==lotId);
            var door=WallPlanSpawner.Entrance(house);
            var frontage=(X:lot.Center.X+lot.FrontX*lot.Depth*.5,Y:lot.Center.Y+lot.FrontY*lot.Depth*.5);
            var sidewalk=FindNearestSegment(frontage,output.Features.Where(f=>f.Kind=="town_sidewalk").ToList(),_=>true);
            var start=sidewalk!=null&&sidewalk.Distance<12.0/5280.0?sidewalk.Point:frontage;
            var obstacles=output.Features.Where(f=>f.Carrier==lotId&&f.Kind is "town_building" or "town_garage").Select(f=>f.Points).ToList();
            var route=ParcelGeometry.Access(frontage,door,lot.Points,obstacles);
            if(route.Count<2)return;
            if(Distance(start,frontage)>1e-9)route.Insert(0,start);
            output.Features.Add(new MapFeature{Kind="town_door_walk",RouteId=lotId+":door-walk",Carrier=lotId,Surface="concrete",Width=4.0/5280.0,
                Points=SegmentPoints(route)});
        }

        private static bool LineSegmentsCross(IReadOnlyList<MapPoint> a,IReadOnlyList<MapPoint> b)
        {
            for(int i=0;i+1<a.Count;i+=2)for(int j=0;j+1<b.Count;j+=2)
                if(TryIntersection((a[i].X,a[i].Y),(a[i+1].X,a[i+1].Y),(b[j].X,b[j].Y),(b[j+1].X,b[j+1].Y),out _))return true;
            return false;
        }

        private static void AddTownDriveway(TransportChunk output,PlannedLot lot,IReadOnlyList<MapFeature> roads)
        {
            var garage=output.Features.FirstOrDefault(f=>f.Kind=="town_garage"&&f.Carrier==lot.Id);
            var frontage=(X:lot.Center.X+lot.FrontX*lot.Depth*.5,Y:lot.Center.Y+lot.FrontY*lot.Depth*.5);
            var endpoint=garage==null?
                (X:lot.HouseCenter.X+lot.AxisX*(lot.Width*.5-8.0/5280.0),Y:lot.HouseCenter.Y+lot.AxisY*(lot.Width*.5-8.0/5280.0)):
                (X:lot.GarageCenter.X+lot.FrontX*11.0/5280.0,Y:lot.GarageCenter.Y+lot.FrontY*11.0/5280.0);
            var obstacles=output.Features.Where(f=>f.Carrier==lot.Id&&f.Kind is "town_building" or "town_garage").Select(f=>f.Points).ToList();
            var route=ParcelGeometry.Access(frontage,endpoint,lot.Points,obstacles);
            if(route.Count<2)return;
            var curb=FindNearestSegment(frontage,(roads??Array.Empty<MapFeature>()).Concat(output.Features.Where(f=>f.Kind=="town_street")).ToList(),f=>f.Kind is "main_road" or "local_road" or "town_street");
            if(curb!=null&&curb.Distance<45.0/5280.0)route.Insert(0,curb.Point);
            output.Features.Add(new MapFeature{Kind="town_driveway",RouteId=lot.Id+":drive",Carrier=lot.Id,
                Surface="concrete",Width=10.0/5280.0,Points=SegmentPoints(route)});
        }

        private static void AddCivicGrounds(TransportChunk output,string id,MapPoint anchor,double ux,double uy,double vx,double vy,
            double width,double height,int columns,int rows,IReadOnlyList<(int Row,int Col)> activeBlocks,int population,int seed,WorldGenerationSettings settings,double[] streetsU,double[] streetsV)
        {
            var items=new List<(string Name,int Chance,double W,double H,int MinPopulation)>{
                ("Water Tower",settings.TownWaterTowerChancePercent,.035,.035,0),
                ("Ball Field",settings.TownBallFieldChancePercent,.075,.055,250),
                ("Cemetery",settings.TownCemeteryChancePercent,.055,.04,0),
                ("Library",settings.TownLibraryChancePercent,.025,.025,750),
                ("Edge Park",settings.TownEdgeParkChancePercent,.065,.055,0),
                ("Golf Course",settings.TownGolfCourseChancePercent,.14,.10,750),
                ("Fairgrounds",settings.TownFairgroundsChancePercent,.11,.09,750),
                ("Airstrip",settings.TownAirstripChancePercent,.20,.07,750),
                ("Central Square",settings.TownCentralSquareChancePercent,.06,.06,0)};
            const double block=300.0/5280.0;
            var coreBlocks=(activeBlocks??Array.Empty<(int Row,int Col)>())
                .OrderBy(cell=>Math.Abs(cell.Row-(rows-1)/2.0)+Math.Abs(cell.Col-(columns-1)/2.0))
                .ThenBy(cell=>Hash(seed,$"{id}:civic-block:{cell.Row}:{cell.Col}")).Take(4).ToList();
            var usedBlocks=new HashSet<(int Row,int Col)>();
            foreach(var item in items)
            {
                if(population<item.MinPopulation||Hash(seed,$"{id}:civic:{item.Name}")%100>=(ulong)item.Chance)continue;
                double cx=anchor.X,cy=anchor.Y,groundWidth=item.W,groundHeight=item.H;bool placedInCore=false;
                bool coreSite=item.Name is "Water Tower" or "Cemetery" or "Library" or "Edge Park" or "Central Square";
                if(coreSite)
                {
                    var cell=coreBlocks.FirstOrDefault(candidate=>!usedBlocks.Contains(candidate));
                    if(coreBlocks.Any(candidate=>candidate.Equals(cell))&&!usedBlocks.Contains(cell))
                    {
                        usedBlocks.Add(cell);
                        double localX=(streetsU[cell.Col]+streetsU[cell.Col+1])*.5;
                        double localY=(streetsV[cell.Row]+streetsV[cell.Row+1])*.5;
                        var center=World(anchor,ux,uy,vx,vy,localX,localY);cx=center.X;cy=center.Y;
                        double scale=Math.Min(1,(block*.82)/Math.Max(item.W,item.H));
                        groundWidth=item.W*scale;groundHeight=item.H*scale;placedInCore=true;
                    }
                }
                if(!placedInCore)
                {
                    int side=(int)(Hash(seed,$"{id}:civic-side:{item.Name}")%4);double offset=side%2==0?height*.5:width*.5;
                    double along=((Hash(seed,$"{id}:civic-edge:{item.Name}")%5)-2)*.11;
                    if(side==0){cx=anchor.X+ux*along+vx*(offset+item.H*.5+.035);cy=anchor.Y+uy*along+vy*(offset+item.H*.5+.035);}
                    else if(side==1){cx=anchor.X+ux*(offset+item.W*.5+.035)+vx*along;cy=anchor.Y+uy*(offset+item.W*.5+.035)+vy*along;}
                    else if(side==2){cx=anchor.X+ux*along-vx*(offset+item.H*.5+.035);cy=anchor.Y+uy*along-vy*(offset+item.H*.5+.035);}
                    else {cx=anchor.X-ux*(offset+item.W*.5+.035)+vx*along;cy=anchor.Y-uy*(offset+item.W*.5+.035)+vy*along;}
                }
                bool rotated=(Hash(seed,$"{id}:civic-orientation:{item.Name}")&1)!=0;
                var ground=rotated?RectCorners(cx,cy,vx,vy,-ux,-uy,groundHeight,groundWidth):RectCorners(cx,cy,ux,uy,vx,vy,groundWidth,groundHeight);
                output.Features.Add(new MapFeature{Kind="town_civic_ground",RouteId=$"{id}:civic:{item.Name}",Name=item.Name,Closed=true,
                    Points=ground,Width=groundWidth,Height=groundHeight});
                output.Features.Add(new MapFeature{Kind="town_civic_marker",RouteId=$"{id}:civic-marker:{item.Name}",Name=item.Name,
                    Points=new List<MapPoint>{new(){X=cx,Y=cy}}});
            }
        }
        private static List<MapPoint> SegmentPoints(IReadOnlyList<(double X,double Y)> points)
        {
            var result=new List<MapPoint>();for(int i=0;i+1<points.Count;i++){result.Add(new MapPoint{X=points[i].X,Y=points[i].Y});result.Add(new MapPoint{X=points[i+1].X,Y=points[i+1].Y});}return result;
        }

        private static double Distance((double X,double Y) a,(double X,double Y) b)
        {double dx=a.X-b.X,dy=a.Y-b.Y;return Math.Sqrt(dx*dx+dy*dy);}

        private static void AddRailIndustrialGround(TransportChunk output, string townId,
            (double X, double Y) trackPoint, (double X, double Y) railA, (double X, double Y) railB,
            double townWidth, double townHeight, ulong hash)
        {
            double side = (hash & 1) == 0 ? 1 : -1;
            double dx = railB.X - railA.X, dy = railB.Y - railA.Y, length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-9) { dx = 1; dy = 0; length = 1; }
            double vx = -dy / length, vy = dx / length;
            double halfAlong = .050, halfAcross = .035;
            double centerX = trackPoint.X + vx * side * (Math.Max(townWidth, townHeight) * .5 + .045 + halfAcross);
            double centerY = trackPoint.Y + vy * side * (Math.Max(townWidth, townHeight) * .5 + .045 + halfAcross);
            var ground = RectCorners(centerX, centerY, 1, 0, 0, 1, halfAlong * 2, halfAcross * 2);
            output.Features.Add(new MapFeature { Kind = "town_rail_ground", RouteId = townId + ":rail-ground", Name = "Industrial Ground", Points = ground });
            output.Features.Add(new MapFeature { Kind = "town_rail_elevator", RouteId = townId + ":elevator", Name = "Grain Elevator",
                Points = RectCorners(centerX + vx * side * .006, centerY + vy * side * .006, 1, 0, 0, 1, .026, .030) });
            for (int i = 0; i < 3; i++)
            {
                double along = (i - 1) * .023;
                output.Features.Add(new MapFeature { Kind = "town_rail_bin", RouteId = townId + ":bin:" + i, Name = "Grain Bin",
                    Points = RectCorners(centerX + along * vx, centerY + along * vy, 1, 0, 0, 1, .012, .012) });
            }
        }

        private static bool IntersectsRail(IReadOnlyList<MapPoint> polygon, IReadOnlyList<MapFeature> rails, double clearance)
        {
            if (polygon == null || polygon.Count < 3) return false;
            foreach (var rail in rails ?? Array.Empty<MapFeature>())
            {
                if (rail.Kind != "rail" || rail.Points == null) continue;
                for (int i = 0; i + 1 < rail.Points.Count; i += 2)
                {
                    var a = (rail.Points[i].X, rail.Points[i].Y); var b = (rail.Points[i + 1].X, rail.Points[i + 1].Y);
                    if (ContainsPoint(polygon, a.X, a.Y) || ContainsPoint(polygon, b.X, b.Y)) return true;
                    for (int j = 0; j < polygon.Count; j++)
                    {
                        var c = (polygon[j].X, polygon[j].Y); var d = (polygon[(j + 1) % polygon.Count].X, polygon[(j + 1) % polygon.Count].Y);
                        if (TryIntersection(a, b, c, d, out _) || SegmentDistance(a, c, d) < clearance || SegmentDistance(c, a, b) < clearance)
                            return true;
                    }
                }
            }
            return false;
        }

        private static void AddPavedTownStreet(TransportChunk output,string id,(double X,double Y) a,(double X,double Y) b,
            string name=null,double width=.008,IReadOnlyList<MapFeature> existingRoads=null)
        {
            double dx=b.X-a.X,dy=b.Y-a.Y,len=Math.Sqrt(dx*dx+dy*dy);
            if(len<1e-9)return;
            var covered=new List<(double Start,double End)>();
            foreach(var road in (existingRoads??Array.Empty<MapFeature>()).Concat(output.Features.ToArray()))
            {
                if(road.Kind is not ("main_road" or "local_road" or "town_street")||road.Points==null)continue;
                if(road.Kind!="main_road"&&road.Surface is not ("paved" or "asphalt"))continue;
                for(int i=0;i+1<road.Points.Count;i+=2)
                {
                    var c=road.Points[i];var d=road.Points[i+1];
                    double ex=d.X-c.X,ey=d.Y-c.Y,elen=Math.Sqrt(ex*ex+ey*ey);
                    if(elen<1e-9||Math.Abs(dx*ey-dy*ex)/(len*elen)>.02)continue;
                    if(Math.Abs((c.X-a.X)*dy-(c.Y-a.Y)*dx)/len>.0025)continue;
                    double t0=((c.X-a.X)*dx+(c.Y-a.Y)*dy)/(len*len);
                    double t1=((d.X-a.X)*dx+(d.Y-a.Y)*dy)/(len*len);
                    double start=Math.Max(0,Math.Min(t0,t1)),end=Math.Min(1,Math.Max(t0,t1));
                    if(end>start)covered.Add((start,end));
                }
            }
            // Keep the uncovered portions, so a partial overlap cannot erase a connection.
            double cursor=0;int piece=0;
            foreach(var interval in covered.OrderBy(c=>c.Start))
            {
                if(interval.Start>cursor)Emit(cursor,interval.Start);
                cursor=Math.Max(cursor,interval.End);
            }
            if(cursor<1)Emit(cursor,1);
            void Emit(double start,double end)
            {
                if((end-start)*len<.0001)return;
                output.Features.Add(new MapFeature{Kind="town_street",RouteId=id+":"+piece++,Name=name,Surface="paved",Width=width,
                    Points=new List<MapPoint>{new(){X=a.X+dx*start,Y=a.Y+dy*start},new(){X=a.X+dx*end,Y=a.Y+dy*end}}});
            }
        }

        private static List<PlannedLot> FilterClearLots(IReadOnlyList<PlannedLot> candidates,IReadOnlyList<MapFeature> roads,
            IReadOnlyList<MapFeature> townStreets,IReadOnlyList<MapFeature> water,IReadOnlyList<MapFeature> rails,IReadOnlyList<MapFeature> civic)
        {
            var accepted=new List<PlannedLot>();
            foreach(var lot in candidates.OrderBy(l=>l.Id,StringComparer.Ordinal))
            {
                if((roads??Array.Empty<MapFeature>()).Any(f=>(f.Kind=="main_road"||f.Kind=="local_road")&&StrictLineHitsPolygon(f.Points,lot.Points))||
                   (townStreets??Array.Empty<MapFeature>()).Any(f=>StrictLineHitsPolygon(f.Points,lot.Points))||
                   (rails??Array.Empty<MapFeature>()).Any(f=>f.Kind=="rail"&&StrictLineHitsPolygon(f.Points,lot.Points))||
                   (water??Array.Empty<MapFeature>()).Any(f=>f.Kind=="rivers"&&StrictLineHitsPolygon(f.Points,lot.Points)||
                       f.Kind=="lakes"&&InteriorsOverlap(f.Points,lot.Points))||
                   (civic??Array.Empty<MapFeature>()).Any(f=>InteriorsOverlap(f.Points,lot.Points))||
                   accepted.Any(other=>InteriorsOverlap(other.Points,lot.Points))) continue;
                accepted.Add(lot);
            }
            return accepted;
        }

        private static bool StrictLineHitsPolygon(IReadOnlyList<MapPoint> line,IReadOnlyList<MapPoint> polygon)
        {
            if(line==null||polygon==null||polygon.Count<3)return false;
            for(int i=0;i+1<line.Count;i+=2)
            {
                var a=line[i];var b=line[i+1];
                if(ContainsStrict(polygon,a)||ContainsStrict(polygon,b))return true;
                for(int j=0;j<polygon.Count;j++)
                {
                    var c=polygon[j];var d=polygon[(j+1)%polygon.Count];
                    double abx=b.X-a.X,aby=b.Y-a.Y,cdx=d.X-c.X,cdy=d.Y-c.Y;
                    double den=abx*cdy-aby*cdx;if(Math.Abs(den)<1e-12)continue;
                    double t=((c.X-a.X)*cdy-(c.Y-a.Y)*cdx)/den,u=((c.X-a.X)*aby-(c.Y-a.Y)*abx)/den;
                    if(t>1e-8&&t<1-1e-8&&u>1e-8&&u<1-1e-8)return true;
                }
            }
            return false;
        }

        private static bool InteriorsOverlap(IReadOnlyList<MapPoint> a,IReadOnlyList<MapPoint> b)
        {
            if(a==null||b==null||a.Count<3||b.Count<3)return false;
            return a.Any(p=>ContainsStrict(b,p))||b.Any(p=>ContainsStrict(a,p))||
                Enumerable.Range(0,a.Count).Any(i=>Enumerable.Range(0,b.Count).Any(j=>
                {
                    var p=a[i];var q=a[(i+1)%a.Count];var r=b[j];var s=b[(j+1)%b.Count];
                    double x=(q.X-p.X)*(s.Y-r.Y)-(q.Y-p.Y)*(s.X-r.X);
                    if(Math.Abs(x)<1e-12)return false;
                    double t=((r.X-p.X)*(s.Y-r.Y)-(r.Y-p.Y)*(s.X-r.X))/x;
                    double u=((r.X-p.X)*(q.Y-p.Y)-(r.Y-p.Y)*(q.X-p.X))/x;
                    return t>1e-8&&t<1-1e-8&&u>1e-8&&u<1-1e-8;
                }));
        }

        private static bool ContainsStrict(IReadOnlyList<MapPoint> polygon,MapPoint p)
        {
            bool inside=false;
            for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++)
                if((polygon[i].Y>p.Y)!=(polygon[j].Y>p.Y)&&p.X<(polygon[j].X-polygon[i].X)*(p.Y-polygon[i].Y)/(polygon[j].Y-polygon[i].Y)+polygon[i].X)inside=!inside;
            return inside;
        }

        private static double[] TownStreetPositions(int seed,string id,int count,string axis)
        {
            var result=new double[count+1];
            for(int i=0;i<count;i++)
                result[i+1]=result[i]+(330+Hash(seed,$"{id}:street-spacing:{axis}:{i-count/2}")%91)/5280.0;
            double center=result[count/2];
            for(int i=0;i<=count;i++)result[i]-=center;
            return result;
        }

        private static List<(int Row,int Col)> BuildTownBlockGrowth(int requiredBlocks,int seed,string id,out int columns,out int rows)
        {
            var ordered=new List<(int Row,int Col)>{(0,0),(0,1),(1,0),(1,1)};
            var occupied=ordered.ToHashSet();
            for(int ring=1;ordered.Count<requiredBlocks;ring++)
            {
                int low=-ring,high=1+ring;
                var candidates=new List<(int Row,int Col)>();
                for(int row=low;row<=high;row++)for(int col=low;col<=high;col++)
                    if(row==low||row==high||col==low||col==high)candidates.Add((row,col));
                while(candidates.Count>0&&ordered.Count<requiredBlocks)
                {
                    int Neighbors((int Row,int Col) c)=>new[]{(c.Row-1,c.Col),(c.Row+1,c.Col),(c.Row,c.Col-1),(c.Row,c.Col+1)}.Count(occupied.Contains);
                    var next=candidates.Where(c=>Neighbors(c)>0)
                        .OrderByDescending(Neighbors).ThenBy(c=>Hash(seed,$"{id}:growth:{c.Row}:{c.Col}")).First();
                    ordered.Add(next);occupied.Add(next);candidates.Remove(next);
                }
            }
            int outerRing=ordered.Max(cell=>Math.Max(Math.Max(cell.Row<0?-cell.Row:cell.Row>1?cell.Row-1:0,
                cell.Col<0?-cell.Col:cell.Col>1?cell.Col-1:0),0));
            rows=columns=2+2*outerRing;
            return ordered.Take(requiredBlocks).Select(cell=>(Row:cell.Row+outerRing,Col:cell.Col+outerRing)).ToList();
        }
        private static List<PlannedLot> BuildLots(int seed, string townId, MapPoint anchor, double ux, double uy,
            double vx, double vy, int columns, int rows,IReadOnlyList<(int Row,int Col)> activeBlocks, ElevationGridData terrain, IReadOnlyList<MapFeature> water,
            IReadOnlyList<MapPoint> outline,IReadOnlyList<MapFeature> extraRoads,IReadOnlyList<MapFeature> rails,double[] streetsU,double[] streetsV)
        {
            var lots = new List<PlannedLot>();
            const double curb=30.0/5280.0, alleyHalf=10.0/5280.0;
            foreach(var cell in activeBlocks)
            {
                int row=cell.Row,col=cell.Col;
                bool alongU=(Hash(seed,$"{townId}:parcel-axis:{row-rows/2}:{col-columns/2}")&1)==0;
                double left=streetsU[col]+curb,right=streetsU[col+1]-curb;
                double bottom=streetsV[row]+curb,top=streetsV[row+1]-curb;
                double cx=(left+right)*.5,cy=(bottom+top)*.5;
                double span=alongU?right-left:top-bottom;
                double depth=((alongU?top-bottom:right-left)*.5)-alleyHalf;
                // Two rows meet at the rear alley, without overlapping corner parcels.
                for(int side=0;side<2;side++)
                {
                    double targetFrontage=(44.0+Hash(seed,$"{townId}:block-type:{row-rows/2}:{col-columns/2}")%35)/5280.0;
                    int count=Math.Max(3,(int)Math.Round(span/targetFrontage));
                    var weights=Enumerable.Range(0,count).Select(n=>.85+(Hash(seed,$"{townId}:frontage:{row-rows/2}:{col-columns/2}:{side}:{n}")%301)/1000.0).ToArray();
                    double cursor=-span*.5,total=weights.Sum();
                    for(int slot=0;slot<count;slot++)
                    {
                        double frontage=span*weights[slot]/total,offset=cursor+frontage*.5;
                        cursor+=frontage;
                        double sign=side==0?-1:1;
                        double across=(alongU?(top-bottom):(right-left))*.5-depth*.5;
                        double lx=cx+(alongU?offset:sign*across),ly=cy+(alongU?sign*across:offset);
                        var center=World(anchor,ux,uy,vx,vy,lx,ly);
                        if(!SuitableLot(terrain,water,center.X,center.Y))continue;
                        string id=$"{townId}:parcel:{row-rows/2}:{col-columns/2}:{side}:{slot}";
                        double axisX=alongU?ux:vx,axisY=alongU?uy:vy;
                        double depthX=alongU?vx:ux,depthY=alongU?vy:uy;
                        double frontX=depthX*sign,frontY=depthY*sign;
                        double houseWidth=Math.Min(frontage-14.0/5280.0,(24+Hash(seed,id+":width")%19)/5280.0);
                        double houseDepth=(28+Hash(seed,id+":depth")%23)/5280.0;
                        double setback=(20+Hash(seed,id+":setback")%21)/5280.0;
                        double lateral=((Hash(seed,id+":lateral")%1001)/1000.0-.5)*Math.Max(0,frontage-houseWidth-16.0/5280.0);
                        var houseCenter=(X:center.X+frontX*(depth*.5-setback-houseDepth*.5)+axisX*lateral,
                            Y:center.Y+frontY*(depth*.5-setback-houseDepth*.5)+axisY*lateral);
                        var housePose=HousePose(seed,id,houseCenter,axisX,axisY,depthX,depthY,houseWidth,houseDepth);
                        var points=RectCorners(center.X,center.Y,axisX,axisY,depthX,depthY,frontage,depth);
                        if(outline!=null&&points.Any(p=>!ContainsPoint(outline,p.X,p.Y)))continue;
                        points=ParcelGeometry.ClearCorridors(points,houseCenter,(rails??Array.Empty<MapFeature>()).Concat(water??Array.Empty<MapFeature>()),12.0/5280.0);
                        if(points.Count<3||housePose.Points.Any(p=>!ParcelGeometry.Contains(points,(p.X,p.Y))))continue;
                        var frontagePoint=(X:center.X+frontX*depth*.5,Y:center.Y+frontY*depth*.5);
                        if(!ParcelGeometry.Contains(points,frontagePoint))continue;
                        double gw=20.0/5280.0,gd=22.0/5280.0;
                        var garage=(X:houseCenter.X-frontX*(houseDepth*.5+gd*.5+7.0/5280.0),Y:houseCenter.Y-frontY*(houseDepth*.5+gd*.5+7.0/5280.0));
                        var garagePoints=RectCorners(garage.X,garage.Y,axisX,axisY,depthX,depthY,gw,gd);
                        lots.Add(new PlannedLot{Id=id,BlockRow=row-rows/2,BlockCol=col-columns/2,LocalX=lx,LocalY=ly,Center=center,Points=points,
                            Width=frontage,Depth=depth,AxisX=axisX,AxisY=axisY,DepthX=depthX,DepthY=depthY,
                            FrontX=frontX,FrontY=frontY,HouseCenter=houseCenter,HousePoints=housePose.Points,HouseFacingRadians=housePose.FacingRadians,
                            GarageCenter=garage,GaragePoints=garagePoints.All(p=>ContainsPoint(points,p.X,p.Y))?garagePoints:null});
                    }
                }
            }
            return lots.OrderBy(lot => Hash(seed, lot.Id)).ToList();
        }

        private static List<PlannedLot> BuildLotsAlongExtraStreets(int seed,string townId,IReadOnlyList<MapFeature> roads,
            ElevationGridData terrain,IReadOnlyList<MapFeature> water,IReadOnlyList<MapFeature> rails)
        {
            var lots=new List<PlannedLot>();
            foreach(var road in roads??Array.Empty<MapFeature>())
            {
                if(road.Name is "OUTLYING ROAD" or "BYPASSED HIGHWAY"||road.Points?.Count<2)continue;
                for(int segment=0;segment+1<road.Points.Count;segment+=2)
                {
                    var a=(road.Points[segment].X,road.Points[segment].Y);var b=(road.Points[segment+1].X,road.Points[segment+1].Y);
                    double dx=b.X-a.X,dy=b.Y-a.Y,length=Math.Sqrt(dx*dx+dy*dy);if(length<.05)continue;
                    double ux=dx/length,uy=dy/length,vx=-uy,vy=ux;int count=Math.Max(1,(int)(length/.10));
                    for(int n=0;n<count;n++)foreach(double side in new[]{-1.0,1.0})
                    {
                        double t=(n+.5)/count,frontage=58.0/5280.0,depth=138.0/5280.0;
                        var center=(X:a.X+dx*t+vx*side*(road.Width*.5+depth*.5+.004),Y:a.Y+dy*t+vy*side*(road.Width*.5+depth*.5+.004));
                        if(!SuitableLot(terrain,water,center.X,center.Y))continue;
                        var points=RectCorners(center.X,center.Y,ux,uy,vx,vy,frontage,depth);
                        if(IntersectsRail(points,rails,.004))continue;
                        string lotId=$"{townId}:extra-lot:{road.RouteId}:{segment}:{n}:{(side>0?1:0)}";
                        var house=(X:center.X-vx*side*depth*.30,Y:center.Y-vy*side*depth*.30);
                        double hw=34.0/5280.0,hd=42.0/5280.0,gw=20.0/5280.0,gd=22.0/5280.0; var housePose=HousePose(seed,lotId,house,ux,uy,vx,vy,hw,hd);
                        var garage=(X:house.X-vx*side*(hd+gd)*.5-vx*side*7.0/5280.0,Y:house.Y-vy*side*(hd+gd)*.5-vy*side*7.0/5280.0);
                        lots.Add(new PlannedLot{Id=lotId,Center=center,Points=points,Width=frontage,Depth=depth,AxisX=ux,AxisY=uy,DepthX=vx,DepthY=vy,
                            FrontX=-vx*side,FrontY=-vy*side,HouseCenter=house,HousePoints=housePose.Points,HouseFacingRadians=housePose.FacingRadians,
                            GarageCenter=garage,GaragePoints=RectCorners(garage.X,garage.Y,ux,uy,vx,vy,gw,gd)});
                    }
                }
            }
            return lots;
        }

        private static List<PlannedLot> BuildExteriorLots(int seed, string townId, MapPoint anchor, double ux, double uy,
            double vx, double vy, IReadOnlyList<MapPoint> outline,ElevationGridData terrain, IReadOnlyList<MapFeature> water,IReadOnlyList<MapFeature> rails)
        {
            const double frontage = 50.0 / 5280.0, depth = 130.0 / 5280.0;
            var lots = new List<PlannedLot>();
            int index = 0;
            for (int side = 0; side < outline.Count; side++)
            {
                var start=outline[side];var end=outline[(side+1)%outline.Count];
                double dx=end.X-start.X,dy=end.Y-start.Y,span=Math.Sqrt(dx*dx+dy*dy);
                if(span<frontage*2)continue;
                dx/=span;dy/=span;double outwardX=dy,outwardY=-dx;
                // Preserve corner room for intersecting streets, then fill
                // each exterior-road face with a continuous row of parcels.
                double usableSpan = Math.Max(0, span - 2 * depth);
                int sideLots = Math.Max(1, (int)Math.Floor(usableSpan / frontage));
                for (int slot = 0; slot < sideLots; slot++, index++)
                {
                double along = -usableSpan / 2 + (slot + .5) * frontage;
                var center=(X:(start.X+end.X)*.5+dx*along+outwardX*depth*.5,Y:(start.Y+end.Y)*.5+dy*along+outwardY*depth*.5);
                if (!SuitableLot(terrain, water, center.X, center.Y)) continue;
                string id = townId + ":exterior-lot:" + side + ":" + slot;
                double houseWidth = 34.0 / 5280.0, houseDepth = 42.0 / 5280.0;
                var houseCenter = (X: center.X - outwardX * depth * .30, Y: center.Y - outwardY * depth * .30);
                double garageWidth = 20.0 / 5280.0, garageDepth = 22.0 / 5280.0;
                var garageCenter = (X: houseCenter.X - outwardX * (houseDepth + garageDepth) * .5 - outwardX * (7.0 / 5280.0),
                    Y: houseCenter.Y - outwardY * (houseDepth + garageDepth) * .5 - outwardY * (7.0 / 5280.0));
                var lotPoints = RectCorners(center.X, center.Y, dx,dy,outwardX,outwardY,frontage,depth);
                var housePose=HousePose(seed,id,houseCenter,dx,dy,outwardX,outwardY,houseWidth,houseDepth); var housePoints=housePose.Points;
                lots.Add(new PlannedLot
                {
                    Id = id, LocalX = along, LocalY = side, Center = center, Points = lotPoints,
                    Width = frontage, Depth = depth, AxisX = dx, AxisY = dy, DepthX = outwardX, DepthY = outwardY,
                    FrontX = -outwardX, FrontY = -outwardY, HouseCenter = houseCenter,
                    HousePoints = housePoints, HouseFacingRadians = housePose.FacingRadians,
                    GarageCenter = garageCenter, GaragePoints = RectCorners(garageCenter.X, garageCenter.Y, dx,dy,outwardX,outwardY,garageWidth,garageDepth)
                });
                }
            }
            return lots;
        }

        private static void AddCommercialBuildings(TransportChunk output, string id, string type, PlannedLot lot,bool civic=false)
        {
            (double WidthFt, double DepthFt) desired = type switch
            {
                "Gas Station" => (35, 48), "Grain Elevator" => (42, 58), "Grocery" => (42, 66),
                "Post Office" => (34, 48), "Bank" => (34, 44), "Church" => (40, 62),
                "City Hall / Fire Hall" => (42, 60), "School" => (44, 78), "Lumberyard" => (44, 76),
                "Farm Supply" => (40, 64), "Implement Dealer" => (44, 76), "Auto Repair" => (38, 56), "Motel" => (48, 68),
                _ => (30, 42)
            };
            double buildingWidth = Math.Min(desired.WidthFt / 5280.0, lot.Width * .82);
            double buildingDepth = Math.Min(desired.DepthFt / 5280.0, lot.Depth * .56);
            double awayX = -lot.FrontX, awayY = -lot.FrontY;
            var center = (X: lot.Center.X + awayX * lot.Depth * .10, Y: lot.Center.Y + awayY * lot.Depth * .10);
            var points = RectCorners(center.X, center.Y, lot.AxisX, lot.AxisY, lot.DepthX, lot.DepthY, buildingWidth, buildingDepth);
            output.Features.Add(new MapFeature { Kind = civic?"town_civic_building":"town_business", RouteId = id + ":building", Name = type, Carrier = civic?"civic":"commercial",
                Points = points, Width = buildingWidth, Height = buildingDepth });
            if (type == "Gas Station")
            {
                var canopy = (X: center.X + awayX * (buildingDepth * .5 + 12.0 / 5280.0), Y: center.Y + awayY * (buildingDepth * .5 + 12.0 / 5280.0));
                output.Features.Add(new MapFeature { Kind = "town_business_aux", RouteId = id + ":canopy", Name = "Fuel Canopy",
                    Points = RectCorners(canopy.X, canopy.Y, lot.AxisX, lot.AxisY, lot.DepthX, lot.DepthY, Math.Min(lot.Width * .7, 30.0 / 5280.0), 24.0 / 5280.0) });
            }
            if (type == "Grain Elevator")
            {
                for (int bin = 0; bin < 2; bin++)
                {
                    double side = bin == 0 ? -1 : 1;
                    var silo = (X: center.X + lot.AxisX * side * buildingWidth * .45, Y: center.Y + lot.AxisY * side * buildingWidth * .45);
                    output.Features.Add(new MapFeature { Kind = "town_business_aux", RouteId = id + ":silo:" + bin, Name = "Grain Bin",
                        Points = RectCorners(silo.X, silo.Y, lot.AxisX, lot.AxisY, lot.DepthX, lot.DepthY, 20.0 / 5280.0, 20.0 / 5280.0) });
                }
            }
        }

        private static List<string> PickBusinesses(int seed, string townId, int population,WorldGenerationSettings settings)
        {
            int tier = population < 250 ? 0 : population < 750 ? 1 : 2;
            var rules = new (string Name, int Small, int Medium, int Large)[]
            {
                ("Post Office",82,96,96),("Bank",25,67,91),("Grocery",6,30,70),("Cafe",22,54,76),
                ("Tavern",18,37,60),("Gas Station",17,60,94),("Grain Elevator",43,55,67),
                ("Farm Supply",10,20,30),("Implement Dealer",7,24,43),("Lumberyard",7,16,51),
                ("Hardware",3,9,27),("Auto Repair",7,26,53),("Beauty Shop",3,9,31),
                ("Insurance",7,33,70),("Church",41,78,92),("Drugstore",0,4,29),
                ("Funeral Home",0,16,44),("Nursing Home",0,9,47),("Veterinarian",3,12,37),
                ("City Hall / Fire Hall",50,80,100),("School",0,30,70),
                ("Motel",settings.TownMotelSmallChancePercent,settings.TownMotelMediumChancePercent,settings.TownMotelLargeChancePercent)
            };
            return rules.Where(rule => Hash(seed, townId + ":business:" + rule.Name) % 100 <
                    (ulong)(tier == 0 ? rule.Small : tier == 1 ? rule.Medium : rule.Large))
                .Select(rule => rule.Name).ToList();
        }

        private static string FamilyName(int seed, string id) => FamilySurnames[Hash(seed, "family:" + id) % (ulong)FamilySurnames.Length];
        private static string ResidentSurname(string name) => PopulationSpawner.SurnameFromPersonName(name);
        private static string HouseArchetype(string id,int seed) => SpawnGuide.HouseGenerator.Archetypes[(int)(Hash(seed,id) % 3)];
        private static uint HousePlanSeed(string id,int seed) { ulong hash=Hash(seed,id); return (uint)(hash>>32)^(uint)hash; }

        private static (List<MapPoint> Points, double FacingRadians) HousePose(int seed,string id,
            (double X,double Y) center,double axisX,double axisY,double depthX,double depthY,double width,double depth)
        {
            int quarterTurns=(int)(Hash(seed,id+":house-facing")%4);
            double facing=Math.Atan2(axisY,axisX)+quarterTurns*Math.PI*.5;
            var points=quarterTurns%2==0
                ? RectCorners(center.X,center.Y,axisX,axisY,depthX,depthY,width,depth)
                : RectCorners(center.X,center.Y,depthX,depthY,-axisX,-axisY,depth,width);
            return (points,facing);
        }

        private static List<(double X,double Y)> FindBusinessNodes(IReadOnlyList<MapFeature> roads,IReadOnlyList<MapFeature> streets)
        {
            var major=new List<((double X,double Y) A,(double X,double Y) B)>();
            foreach(var feature in roads??Array.Empty<MapFeature>())
            {
                if(!(feature.Kind=="main_road"||feature.Kind=="local_road"&&feature.Surface=="paved")||feature.Points==null)continue;
                for(int i=0;i+1<feature.Points.Count;i+=2)major.Add(((feature.Points[i].X,feature.Points[i].Y),(feature.Points[i+1].X,feature.Points[i+1].Y)));
            }
            var local=new List<((double X,double Y) A,(double X,double Y) B)>();
            foreach(var feature in streets??Array.Empty<MapFeature>())
            {
                if(feature.Kind!="town_street"||feature.Points==null)continue;
                for(int i=0;i+1<feature.Points.Count;i+=2)local.Add(((feature.Points[i].X,feature.Points[i].Y),(feature.Points[i+1].X,feature.Points[i+1].Y)));
            }
            var nodes=new List<(double X,double Y)>();
            foreach(var arterial in major)foreach(var street in local)
                if(TryIntersection(arterial.A,arterial.B,street.A,street.B,out var point)&&!nodes.Any(n=>Distance(n,point)<.001))nodes.Add(point);
            return nodes;
        }

        private static bool IsCivicBusiness(string type) => type is "Post Office" or "Church" or "City Hall / Fire Hall" or "School" or "Nursing Home";

        private static List<PlannedLot> FindCivicFrontLots(IReadOnlyList<PlannedLot> lots,int columns,int rows,double width,double height)
        {
            return (lots??Array.Empty<PlannedLot>())
                .GroupBy(lot=>(Row:lot.BlockRow,Col:lot.BlockCol))
                .OrderBy(group=>Math.Abs(group.Key.Row+.5)+Math.Abs(group.Key.Col+.5))
                .ThenBy(group=>group.Key.Row).ThenBy(group=>group.Key.Col)
                .Take(8)
                .Select(group=>group.OrderBy(lot=>lot.LocalX*lot.LocalX+lot.LocalY*lot.LocalY)
                    .ThenBy(lot=>lot.Id,StringComparer.Ordinal).First()).ToList();
        }
        private static double BusinessLotScore(PlannedLot lot,IReadOnlyList<MapFeature> roads,IReadOnlyList<(double X,double Y)> intersections)
        {
            var center=(lot.Center.X,lot.Center.Y);
            var nearestRoad=FindNearestSegment(center,roads,f=>f.Kind=="main_road"||f.Kind=="local_road"&&f.Surface=="paved");
            double roadDistance=nearestRoad?.Distance??10;
            double intersectionDistance=intersections==null||intersections.Count==0?roadDistance:
                intersections.Min(point=>Distance(center,point));
            return Math.Round(roadDistance*.45+intersectionDistance*.55,7);
        }
        private static RouteHit FindNearestSegment((double X, double Y) point, IReadOnlyList<MapFeature> features,
            Func<MapFeature, bool> predicate)
        {
            RouteHit best = null;
            foreach (var feature in features ?? Array.Empty<MapFeature>())
            {
                if (!predicate(feature) || feature.Points == null) continue;
                for (int i = 0; i + 1 < feature.Points.Count; i += 2)
                {
                    var a = (feature.Points[i].X, feature.Points[i].Y); var b = (feature.Points[i + 1].X, feature.Points[i + 1].Y);
                    var nearest = ClosestPoint(a, b, point); double distance = SegmentDistance(point, a, b);
                    if (best == null || distance < best.Distance) best = new RouteHit { Feature = feature, A = a, B = b, Point = nearest, Distance = distance };
                }
            }
            return best;
        }

        private static bool TryNearbyCountyCorner((double X, double Y) anchor, IReadOnlyList<MapFeature> roads, out (double X, double Y) corner)
        {
            corner = default; double best = 2.0;
            var segments = new List<((double X, double Y) A, (double X, double Y) B)>();
            foreach (var feature in roads ?? Array.Empty<MapFeature>())
            {
                if (feature.Kind != "local_road" || feature.Surface != "paved" || feature.Points == null) continue;
                for (int i = 0; i + 1 < feature.Points.Count; i += 2)
                    segments.Add(((feature.Points[i].X, feature.Points[i].Y), (feature.Points[i + 1].X, feature.Points[i + 1].Y)));
            }
            for (int i = 0; i < segments.Count; i++)
            for (int j = i + 1; j < segments.Count; j++)
            {
                if (!TryIntersection(segments[i].A, segments[i].B, segments[j].A, segments[j].B, out var p)) continue;
                double distance = SegmentDistance(anchor, p, p);
                if (distance < best) { best = distance; corner = p; }
            }
            return best < 2.0;
        }

        private static void AddTownRoadConnections(TransportChunk output,string id,IReadOnlyList<MapFeature> roads,
            MapPoint anchor,double ux,double uy,double vx,double vy)
        {
            var network=output.Features.Where(f=>f.Kind=="town_street"&&f.Points?.Count>=2).ToList();
            var approaches=(roads??Array.Empty<MapFeature>()).Where(f=>f.Kind=="main_road"||f.Kind=="local_road"&&f.Surface=="paved")
                .Select(f=>FindNearestSegment((anchor.X,anchor.Y),new[]{f},_=>true))
                .Where(hit=>hit!=null&&hit.Distance<=2.5)
                .GroupBy(hit=>hit.Feature.RouteId??$"{hit.Point.X:R}:{hit.Point.Y:R}",StringComparer.Ordinal)
                .Select(group=>group.OrderBy(hit=>hit.Distance).First()).OrderBy(hit=>hit.Distance)
                .ThenBy(hit=>hit.Feature.RouteId,StringComparer.Ordinal).Take(4).ToList();
            int connection=0;
            foreach(var approach in approaches)
            {
                var join=FindNearestSegment(approach.Point,network,_=>true);
                if(join==null||join.Distance<.004||join.Distance>1.5)continue;
                var start=(X:approach.Point.X,Y:approach.Point.Y);
                var finish=(X:join.Point.X,Y:join.Point.Y);
                double startDx=start.X-anchor.X,startDy=start.Y-anchor.Y;
                double finishDx=finish.X-anchor.X,finishDy=finish.Y-anchor.Y;
                double startU=startDx*ux+startDy*uy,startV=startDx*vx+startDy*vy;
                double finishU=finishDx*ux+finishDy*uy,finishV=finishDx*vx+finishDy*vy;
                // Route the link in two legs parallel to the town grid axes.
                var elbow=World(anchor,ux,uy,vx,vy,finishU,startV);
                var route=new List<(double X,double Y)>{start};
                if(Distance(start,elbow)>.0001)route.Add(elbow);
                if(Distance(route[^1],finish)>.0001)route.Add(finish);
                if(route.Count<2)continue;
                for(int i=0;i+1<route.Count;i++)
                    AddPavedTownStreet(output,$"{id}:road-link:{connection++}",route[i],route[i+1],"Town Road",.012,roads);
            }
        }
        private static void AddMainStreetOverlay(TransportChunk output, string townId, RouteHit route, IReadOnlyList<MapPoint> outline)
        {
            if (route == null || route.Distance > 1.5) return;
            var cuts = new List<double> { 0, 1 };
            double dx = route.B.X - route.A.X, dy = route.B.Y - route.A.Y;
            for (int i = 0; i < outline.Count; i++)
            {
                var c = outline[i]; var d = outline[(i + 1) % outline.Count];
                double sx = d.X - c.X, sy = d.Y - c.Y, den = dx * sy - dy * sx;
                if (Math.Abs(den) < 1e-12) continue;
                double qx = c.X - route.A.X, qy = c.Y - route.A.Y;
                double t = (qx * sy - qy * sx) / den, u = (qx * dy - qy * dx) / den;
                if (t > 0 && t < 1 && u >= 0 && u <= 1) cuts.Add(t);
            }
            cuts.Sort();
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                double t0 = cuts[i], t1 = cuts[i + 1], mid = (t0 + t1) / 2;
                if (t1 - t0 < 1e-8 || !ContainsPoint(outline, route.A.X + dx * mid, route.A.Y + dy * mid)) continue;
                AddPavedTownStreet(output,townId+":through-main:"+i,
                    (route.A.X+dx*t0,route.A.Y+dy*t0),(route.A.X+dx*t1,route.A.Y+dy*t1),"Main Street",.014,null);
            }
        }

        private static bool ContainsPoint(IReadOnlyList<MapPoint> polygon, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i]; var b = polygon[j];
                if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y + 1e-15) + a.X) inside = !inside;
            }
            return inside;
        }

        private static bool PolylineIntersectsPolygon(IReadOnlyList<MapPoint> line,IReadOnlyList<MapPoint> polygon)
        {
            if(line==null||line.Count<2||polygon==null||polygon.Count<3)return false;
            for(int i=0;i+1<line.Count;i+=2)
            {
                var a=(line[i].X,line[i].Y);var b=(line[i+1].X,line[i+1].Y);
                if(ContainsPoint(polygon,a.X,a.Y)||ContainsPoint(polygon,b.X,b.Y))return true;
                for(int j=0;j<polygon.Count;j++)
                    if(TryIntersection(a,b,(polygon[j].X,polygon[j].Y),(polygon[(j+1)%polygon.Count].X,polygon[(j+1)%polygon.Count].Y),out _))return true;
            }
            return false;
        }

        private static List<MapPoint> RectCorners(double cx, double cy, double ux, double uy, double vx, double vy,
            double width, double height)
        {
            double hw = width / 2, hh = height / 2;
            var corners = new List<MapPoint>();
            foreach (var p in new[] { (-hw,-hh), (hw,-hh), (hw,hh), (-hw,hh) })
            {
                var world = World(new MapPoint { X = cx, Y = cy }, ux, uy, vx, vy, p.Item1, p.Item2);
                corners.Add(new MapPoint { X = world.X, Y = world.Y });
            }
            return corners;
        }

        private static (double X, double Y) World(MapPoint anchor, double ux, double uy, double vx, double vy, double x, double y) =>
            (anchor.X + ux * x + vx * y, anchor.Y + uy * x + vy * y);

        private static (double X, double Y) ClosestPoint((double X, double Y) a, (double X, double Y) b, (double X, double Y) p)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, den = dx * dx + dy * dy;
            double t = den < 1e-12 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / den, 0, 1);
            return (a.X + dx * t, a.Y + dy * t);
        }

        private static bool PolygonOverlap(IReadOnlyList<MapPoint> a, IReadOnlyList<MapPoint> b) =>
            !Separated(a, b) && !Separated(b, a);

        private static bool Separated(IReadOnlyList<MapPoint> axes, IReadOnlyList<MapPoint> polygon)
        {
            for (int i = 0; i < axes.Count; i++)
            {
                var p = axes[i]; var q = axes[(i + 1) % axes.Count]; double nx = -(q.Y - p.Y), ny = q.X - p.X;
                double minA = double.PositiveInfinity, maxA = double.NegativeInfinity, minB = double.PositiveInfinity, maxB = double.NegativeInfinity;
                foreach (var point in axes) { double d = point.X * nx + point.Y * ny; minA = Math.Min(minA, d); maxA = Math.Max(maxA, d); }
                foreach (var point in polygon) { double d = point.X * nx + point.Y * ny; minB = Math.Min(minB, d); maxB = Math.Max(maxB, d); }
                if (maxA < minB || maxB < minA) return true;
            }
            return false;
        }

        private static bool TryIntersection((double X, double Y) a, (double X, double Y) b,
            (double X, double Y) c, (double X, double Y) d, out (double X, double Y) point)
        {
            double rx = b.X-a.X, ry = b.Y-a.Y, sx = d.X-c.X, sy = d.Y-c.Y;
            double den = rx*sy-ry*sx;
            if (Math.Abs(den) < 1e-12) { point = default; return false; }
            double t = ((c.X-a.X)*sy-(c.Y-a.Y)*sx)/den, u = ((c.X-a.X)*ry-(c.Y-a.Y)*rx)/den;
            if (t < 0 || t > 1 || u < 0 || u > 1) { point = default; return false; }
            point = (a.X+t*rx, a.Y+t*ry); return true;
        }

        private static bool SuitableLot(ElevationGridData terrain, IReadOnlyList<MapFeature> water, double x, double y)
        {
            if (terrain != null)
            {
                double center = SampleElevation(terrain, x, y);
                double maxSlope = 0;
                foreach (var offset in new[] { (-.02, 0.0), (.02, 0.0), (0.0, -.02), (0.0, .02) })
                    maxSlope = Math.Max(maxSlope, Math.Abs(SampleElevation(terrain, x + offset.Item1, y + offset.Item2) - center));
                if (center < -35 || maxSlope > 45) return false;
            }
            foreach (var feature in water ?? Array.Empty<MapFeature>())
            {
                if (feature.Points == null) continue;
                if (feature.Kind == "rivers")
                {
                    for (int i = 0; i + 1 < feature.Points.Count; i += 2)
                        if (SegmentDistance((x, y), (feature.Points[i].X, feature.Points[i].Y),
                            (feature.Points[i + 1].X, feature.Points[i + 1].Y)) < Math.Max(.08, feature.Width * .5)) return false;
                }
                else if (feature.Kind == "lakes" && feature.Points.Count >= 3 &&
                    x >= feature.Points.Min(p => p.X) && x <= feature.Points.Max(p => p.X) &&
                    y >= feature.Points.Min(p => p.Y) && y <= feature.Points.Max(p => p.Y)) return false;
            }
            return true;
        }

        private static double SampleElevation(ElevationGridData grid, double xMiles, double yMiles)
        {
            double gx = (xMiles * 5280 - grid.OriginXFt) / grid.StepFt;
            double gy = (yMiles * 5280 - grid.OriginYFt) / grid.StepFt;
            int ix = Math.Max(0, Math.Min(grid.Values[0].Count - 2, (int)Math.Floor(gx)));
            int iy = Math.Max(0, Math.Min(grid.Values.Count - 2, (int)Math.Floor(gy)));
            double tx = Math.Max(0, Math.Min(1, gx - ix)), ty = Math.Max(0, Math.Min(1, gy - iy));
            double a = grid.Values[iy][ix] * (1 - tx) + grid.Values[iy][ix + 1] * tx;
            double b = grid.Values[iy + 1][ix] * (1 - tx) + grid.Values[iy + 1][ix + 1] * tx;
            return a * (1 - ty) + b * ty;
        }

        private static double SegmentDistance((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double length2 = dx * dx + dy * dy;
            double t = length2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length2));
            return Math.Sqrt(Math.Pow(p.X - (a.X + t * dx), 2) + Math.Pow(p.Y - (a.Y + t * dy), 2));
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

        private static async Task StopWorkerAsync(Process process)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            try { await process.WaitForExitAsync().ConfigureAwait(false); } catch { }
        }
    }
}

