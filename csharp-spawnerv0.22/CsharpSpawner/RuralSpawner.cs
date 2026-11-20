using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CsharpSpawner
{
    // Places road-front farmsteads and acreages before towns grow around them.
    public static class RuralSpawner
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { IncludeFields = true };
        private static readonly string[] FamilyNames = { "Bakker", "Brouwer", "De Jong", "De Vries", "Hofman", "Jansen", "Jorissen", "Klein", "Koster", "Meyer", "Mulder", "Schmidt", "Smit", "Van Dijk", "Van Wyk", "Visser", "Vogel", "Vos", "Wagner", "Wolfe" };
        private static readonly string[] OutbuildingTypes = { "Machine Shed", "Barn", "Grain Bin", "Corn Crib", "Hog Building", "Poultry House", "Quonset" };

        /// <summary>In-process generation of one rural chunk from saved roads and terrain.</summary>
        public static TransportChunk GenerateChunk(int seed, TownInput input, IReadOnlyList<MapFeature> roads, IReadOnlyList<MapFeature> water, WorldGenerationSettings settings = null)
            => GenerateChunk(seed,input,roads,water,Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),settings);

        public static TransportChunk GenerateChunk(int seed,TownInput input,IReadOnlyList<MapFeature> roads,IReadOnlyList<MapFeature> water,
            IReadOnlyList<MapFeature> rails,IReadOnlyList<MapFeature> settlements,WorldGenerationSettings settings=null)
        {
            settings ??= new WorldGenerationSettings();
            var output = new TransportChunk { ChunkX = input.ChunkX, ChunkY = input.ChunkY };
            var localWater=FeaturesNearChunk(water,input.ChunkX,input.ChunkY,1);
            var localRoads=RoadsForChunk(roads,input.ChunkX,input.ChunkY);
            foreach (var road in localRoads)
                PlaceLotsForRoad(seed, road, input.TerrainGrid, localWater, output, settings,roads,rails);
            GenerateFieldEntrances(seed,input,localRoads,output.Features,settings,output);
            GenerateCattleYards(seed,input,roads,localWater,settlements,settings,output);
            GenerateCoopCorners(seed,input,roads,rails,settlements,settings,output);
            return output;
        }
        public static IReadOnlyList<WorldFeature> GenerateWorldFeatures(int seed, TownInput input, IReadOnlyList<MapFeature> roads, IReadOnlyList<MapFeature> water, WorldGenerationSettings settings = null) =>
            LegacyMapFeatureConverter.ToWorldFeatures(GenerateChunk(seed,input,roads,water,settings).Features);

        public static async Task<int> RunWorkerAsync()
        {
            try
            {
                var request = JsonSerializer.Deserialize<RuralRequest>(await Console.In.ReadToEndAsync().ConfigureAwait(false), JsonOptions)
                    ?? throw new InvalidOperationException("Missing rural worker request.");
                var roads = request.Roads ?? new List<MapFeature>();
                var water = request.WaterFeatures ?? new List<MapFeature>();
                var rails=request.RailFeatures??new List<MapFeature>();
                var settlements=request.Settlements??new List<MapFeature>();
                var settings = request.Settings ?? new WorldGenerationSettings();
                var roadIndex = new FeatureSpatialIndex(roads);
                var waterIndex = new FeatureSpatialIndex(water);
                var railIndex = new FeatureSpatialIndex(rails);
                var settlementIndex = new FeatureSpatialIndex(settlements);
                using var stdout = new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                foreach (var input in request.Chunks ?? new List<TownInput>())
                {
                    var output=GenerateChunk(request.Seed,input,roadIndex.QueryChunk(input.ChunkX,input.ChunkY,1),
                        waterIndex.QueryChunk(input.ChunkX,input.ChunkY,1),railIndex.QueryChunk(input.ChunkX,input.ChunkY,.1),
                        settlementIndex.QueryChunk(input.ChunkX,input.ChunkY,settings.CoopCornerTownExclusionMiles),settings);
                    await stdout.WriteLineAsync(JsonSerializer.Serialize(output, JsonOptions)).ConfigureAwait(false);
                }
                return 0;
            }
            catch (Exception ex) { await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false); return 1; }
        }

        private static List<MapFeature> RoadsForChunk(IReadOnlyList<MapFeature> roads,int chunkX,int chunkY) =>
            (roads??Array.Empty<MapFeature>()).Where(f=>f.Kind=="local_road"&&f.Points?.Count>=2&&OverlapsChunk(f,chunkX,chunkY,.5))
                .OrderBy(f=>f.RouteId,StringComparer.Ordinal).ToList();

        private static List<MapFeature> FeaturesNearChunk(IReadOnlyList<MapFeature> features,int chunkX,int chunkY,double margin) =>
            (features??Array.Empty<MapFeature>()).Where(f=>f.Points?.Count>0&&OverlapsChunk(f,chunkX,chunkY,margin)).ToList();

        private static bool OverlapsChunk(MapFeature feature,int chunkX,int chunkY,double margin)
        {
            double minX=feature.Points.Min(p=>p.X),maxX=feature.Points.Max(p=>p.X);
            double minY=feature.Points.Min(p=>p.Y),maxY=feature.Points.Max(p=>p.Y);
            return maxX>=chunkX-margin&&minX<=chunkX+1+margin&&maxY>=chunkY-margin&&minY<=chunkY+1+margin;
        }

        private sealed class FieldFrontage
        {
            public MapFeature Road; public bool Horizontal; public double Line,Start,End,Side; public int Qx,Qy;
        }

        private static void GenerateFieldEntrances(int seed,TownInput input,IReadOnlyList<MapFeature> roads,
            IReadOnlyList<MapFeature> existing,WorldGenerationSettings settings,TransportChunk output)
        {
            var frontages=new Dictionary<(int X,int Y),List<FieldFrontage>>();
            foreach(var road in roads??Array.Empty<MapFeature>())
            {
                bool horizontal=road.RouteId?.StartsWith("section:ew:",StringComparison.Ordinal)==true;
                bool vertical=road.RouteId?.StartsWith("section:ns:",StringComparison.Ordinal)==true;
                if((!horizontal&&!vertical)||road.Points?.Count<2)continue;
                double start=horizontal?Math.Min(road.Points[0].X,road.Points[1].X):Math.Min(road.Points[0].Y,road.Points[1].Y);
                double end=horizontal?Math.Max(road.Points[0].X,road.Points[1].X):Math.Max(road.Points[0].Y,road.Points[1].Y);
                double line=horizontal?(road.Points[0].Y+road.Points[1].Y)*.5:(road.Points[0].X+road.Points[1].X)*.5;
                int pieces=Math.Max(1,(int)Math.Ceiling((end-start)*2));
                for(int piece=0;piece<pieces;piece++)
                {
                    double intervalStart=start+piece*.5,intervalEnd=Math.Min(end,intervalStart+.5);
                    if(intervalEnd-intervalStart<.1)continue;
                    int along=(int)Math.Floor((intervalStart+intervalEnd)*.5*2);
                    foreach(double side in new[]{-1.0,1.0})
                    {
                        int qx=horizontal?along:(int)Math.Floor((line+side*.0001)*2);
                        int qy=horizontal?(int)Math.Floor((line+side*.0001)*2):along;
                        var key=(qx,qy);
                        if(!frontages.TryGetValue(key,out var list))frontages[key]=list=new List<FieldFrontage>();
                        list.Add(new FieldFrontage{Road=road,Horizontal=horizontal,Line=line,Start=intervalStart,End=intervalEnd,Side=side,Qx=qx,Qy=qy});
                    }
                }
            }
            foreach(var pair in frontages.OrderBy(pair=>pair.Key.X).ThenBy(pair=>pair.Key.Y))
            {
                string key=$"field-entrance:{pair.Key.X}:{pair.Key.Y}";
                ulong hash=Hash(seed,key);
                var options=pair.Value.OrderBy(frontage=>frontage.Road.RouteId,StringComparer.Ordinal).ToList();
                var frontage=options[(int)(hash%(ulong)options.Count)];
                int min=Math.Max(0,settings.FieldEntrancesMinPerQuarter),max=Math.Max(min,settings.FieldEntrancesMaxPerQuarter);
                int count=min+(int)(Hash(seed,key+":count")%(ulong)(max-min+1));
                for(int index=0;index<count;index++)
                {
                    double setback=settings.FieldEntranceSetbackFeet/5280.0;
                    double along=count==1
                        ? ((Hash(seed,key+":end")&1)==0?frontage.Start+setback:frontage.End-setback)
                        : index==0?frontage.Start+setback:index==count-1?frontage.End-setback:
                            frontage.Start+(frontage.End-frontage.Start)*index/(count-1.0);
                    if(along<=frontage.Start||along>=frontage.End)continue;
                    double x=frontage.Horizontal?along:frontage.Line;
                    double y=frontage.Horizontal?frontage.Line:along;
                    if((int)Math.Floor(x)!=input.ChunkX||(int)Math.Floor(y)!=input.ChunkY)continue;
                    var mouth=(X:x,Y:y);
                    if(existing.Any(feature=>feature.Kind=="rural_lot"&&NearLot(mouth,feature.Points,.012)))continue;
                    double length=(settings.FieldEntranceLengthMinFeet+
                        (settings.FieldEntranceLengthMaxFeet-settings.FieldEntranceLengthMinFeet)*
                        ((Hash(seed,key+":length:"+index)%10001)/10000.0))/5280.0;
                    double endX=x+(frontage.Horizontal?0:frontage.Side*length);
                    double endY=y+(frontage.Horizontal?frontage.Side*length:0);
                    output.Features.Add(new MapFeature{Kind="field_entrance",RouteId=$"{key}:{index}",Name="Field Entrance",
                        Carrier=frontage.Road.RouteId,Surface="dirt",Width=settings.FieldEntranceWidthFeet/5280.0,
                        Points=SegmentPoints(new[]{mouth,(X:endX,Y:endY)})});
                }
            }
        }

        private static void GenerateCattleYards(int seed,TownInput input,IReadOnlyList<MapFeature> roads,
            IReadOnlyList<MapFeature> water,IReadOnlyList<MapFeature> settlements,WorldGenerationSettings settings,TransportChunk output)
        {
            if(settings.CattleYardChancePercent<=0)return;
            int sideLength=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(Math.Max(1,settings.CattleYardSectionsPerSite))));
            int baseX=FloorMultiple(input.ChunkX,sideLength),baseY=FloorMultiple(input.ChunkY,sideLength);
            for(int groupX=baseX-sideLength;groupX<=baseX+sideLength;groupX+=sideLength)
            for(int groupY=baseY-sideLength;groupY<=baseY+sideLength;groupY+=sideLength)
            {
                string id=$"cattle-yard:{groupX}:{groupY}";
                ulong hash=Hash(seed,id);
                if(hash%100>=(ulong)settings.CattleYardChancePercent)continue;
                int candidate=(int)(Hash(seed,id+":road")%4);
                string routeId=candidate switch
                {
                    0=>$"section:ew:{groupY+1}:{groupX}",
                    1=>$"section:ew:{groupY+1}:{groupX+1}",
                    2=>$"section:ns:{groupX+1}:{groupY}",
                    _=>$"section:ns:{groupX+1}:{groupY+1}"
                };
                var road=(roads??Array.Empty<MapFeature>()).FirstOrDefault(feature=>feature.RouteId==routeId&&
                    feature.Kind=="local_road"&&feature.Surface!="paved"&&feature.Points?.Count>=2);
                if(road==null)continue;
                double ax=road.Points[0].X,ay=road.Points[0].Y,bx=road.Points[1].X,by=road.Points[1].Y;
                double dx=bx-ax,dy=by-ay,length=Math.Sqrt(dx*dx+dy*dy);
                if(length<.5)continue;
                double ux=dx/length,uy=dy/length,nx=-uy,ny=ux;
                double acres=settings.CattleYardMinAcres+(settings.CattleYardMaxAcres-settings.CattleYardMinAcres)*
                    ((Hash(seed,id+":area")%10001)/10000.0);
                double width=Math.Sqrt(acres*43560*1.5)/5280.0,depth=(acres*43560)/(width*5280.0*5280.0);
                double t=.5+(((Hash(seed,id+":position")%10001)/10000.0)-.5)*Math.Max(0,1-width/length);
                double side=(Hash(seed,id+":side")&1)==0?1:-1;
                double cx=ax+dx*t+nx*side*(.018+depth*.5),cy=ay+dy*t+ny*side*(.018+depth*.5);
                if((int)Math.Floor(cx)!=input.ChunkX||(int)Math.Floor(cy)!=input.ChunkY)continue;
                if((settlements??Array.Empty<MapFeature>()).Any(site=>site.Kind=="settlement"&&site.Points?.Count>0&&
                    Distance((cx,cy),(site.Points[0].X,site.Points[0].Y))<1.0))continue;
                var parcel=RectCorners(cx,cy,ux,uy,width,depth);
                if(NearWater(water,parcel,cx,cy))continue;
                if(output.Features.Any(feature=>feature.Kind=="rural_lot"&&PolygonsOverlap(parcel,
                    feature.Points?.Select(point=>(point.X,point.Y)).ToList())))continue;
                output.Features.Add(new MapFeature{Kind="cattle_yard",RouteId=id,Name="Cattle Yard",Carrier=id,
                    Closed=true,Width=width,Height=depth,Surface=road.Surface,
                    Points=parcel.Select(p=>new MapPoint{X=p.X,Y=p.Y}).ToList()});
                double penWidth=width*.22,penDepth=depth*.22;
                for(int pen=0;pen<3;pen++)
                {
                    double px=cx+ux*((pen-1)*width*.25)+nx*side*depth*.22;
                    double py=cy+uy*((pen-1)*width*.25)+ny*side*depth*.22;
                    output.Features.Add(new MapFeature{Kind="cattle_pen",RouteId=id+":pen:"+pen,Carrier=id,Closed=true,
                        Points=RectCorners(px,py,ux,uy,penWidth,penDepth).Select(p=>new MapPoint{X=p.X,Y=p.Y}).ToList()});
                }
                var feedStart=(X:ax+dx*t,Y:ay+dy*t);
                var feedEnd=(X:cx+nx*side*depth*.18,Y:cy+ny*side*depth*.18);
                output.Features.Add(new MapFeature{Kind="cattle_feed_road",RouteId=id+":feed-road",Carrier=id,Surface="gravel",Width=.01,
                    Points=SegmentPoints(new[]{feedStart,feedEnd})});
                double officeX=cx-ux*width*.34+nx*side*depth*.28,officeY=cy-uy*width*.34+ny*side*depth*.28;
                output.Features.Add(new MapFeature{Kind="cattle_office_shed",RouteId=id+":office",Carrier=id,Name="Office Shed",Closed=true,
                    Points=RectCorners(officeX,officeY,ux,uy,38.0/5280.0,30.0/5280.0).Select(p=>new MapPoint{X=p.X,Y=p.Y}).ToList()});
            }
        }

        private static void GenerateCoopCorners(int seed,TownInput input,IReadOnlyList<MapFeature> roads,
            IReadOnlyList<MapFeature> rails,IReadOnlyList<MapFeature> settlements,WorldGenerationSettings settings,TransportChunk output)
        {
            var localRoads=FeaturesNearChunk(roads,input.ChunkX,input.ChunkY,.02);
            var localRails=FeaturesNearChunk(rails,input.ChunkX,input.ChunkY,.1);
            var localSettlements=FeaturesNearChunk(settlements,input.ChunkX,input.ChunkY,settings.CoopCornerTownExclusionMiles);
            var ew=localRoads.Where(f=>f.RouteId?.StartsWith("section:ew:",StringComparison.Ordinal)==true&&f.Points?.Count>=2).ToList();
            var ns=localRoads.Where(f=>f.RouteId?.StartsWith("section:ns:",StringComparison.Ordinal)==true&&f.Points?.Count>=2).ToList();
            var seen=new HashSet<(long X,long Y)>();
            foreach(var horizontal in ew)foreach(var vertical in ns)
            {
                if(Math.Abs(horizontal.Points[0].Y-vertical.Points[0].Y)>3||Math.Abs(horizontal.Points[0].X-vertical.Points[0].X)>3)continue;
                if(!TryIntersection((horizontal.Points[0].X,horizontal.Points[0].Y),(horizontal.Points[1].X,horizontal.Points[1].Y),
                    (vertical.Points[0].X,vertical.Points[0].Y),(vertical.Points[1].X,vertical.Points[1].Y),out var corner))continue;
                if((int)Math.Floor(corner.X)!=input.ChunkX||(int)Math.Floor(corner.Y)!=input.ChunkY)continue;
                var key=((long)Math.Round(corner.X*100000),(long)Math.Round(corner.Y*100000));if(!seen.Add(key))continue;
                if(localSettlements.Any(site=>site.Kind=="settlement"&&site.Points?.Count>0&&
                    Distance(corner,(site.Points[0].X,site.Points[0].Y))<settings.CoopCornerTownExclusionMiles))continue;
                var nearest=NearestRailPoint(corner,localRails);
                if(nearest.Distance>settings.CoopCornerRailDistanceFeet/5280.0)continue;
                string id=$"coop-corner:{seed}:{key.Item1}:{key.Item2}";
                double cx=corner.X+.045,cy=corner.Y+.035;
                output.Features.Add(new MapFeature{Kind="rural_coop_elevator",RouteId=id+":elevator",Carrier=id,Name="Co-op Elevator",Closed=true,
                    Points=RectCorners(cx,cy,1,0,48.0/5280.0,42.0/5280.0).Select(p=>new MapPoint{X=p.X,Y=p.Y}).ToList()});
                for(int bin=0;bin<4;bin++)
                    output.Features.Add(new MapFeature{Kind="rural_coop_bin",RouteId=id+":bin:"+bin,Carrier=id,Name="Grain Bin",Closed=true,
                        Points=RectCorners(cx+.035+(bin%2)*.012,cy+.025+(bin/2)*.012,1,0,20.0/5280.0,20.0/5280.0)
                            .Select(p=>new MapPoint{X=p.X,Y=p.Y}).ToList()});
            }
        }

        private static void PlaceLotsForRoad(int seed, MapFeature road, ElevationGridData terrain,
            IReadOnlyList<MapFeature> water, TransportChunk output, WorldGenerationSettings settings,IReadOnlyList<MapFeature> roads,IReadOnlyList<MapFeature> rails)
        {
            if (road.Points == null || road.Points.Count < 2 || road.RouteId == null) return;
            var a = (X: road.Points[0].X, Y: road.Points[0].Y);
            var b = (X: road.Points[1].X, Y: road.Points[1].Y);
            double dx = b.X - a.X, dy = b.Y - a.Y, length = Math.Sqrt(dx * dx + dy * dy);
            if (length < .5) return;
            double ux = dx / length, uy = dy / length, nx = -uy, ny = ux;
            int farmCount = WeightedFarmCount(Hash(seed, road.RouteId + ":farm-count"), settings);
            bool hasAcreage = Hash(seed, road.RouteId + ":acreage") % 100 < (ulong)settings.AcreageChancePercent;
            var occupied = new List<(double X, double Y, double W, double H)>();
            for (int i = 0; i < farmCount + (hasAcreage ? 1 : 0); i++)
            {
                bool acreage = i >= farmCount;
                string id = road.RouteId + (acreage ? ":acreage" : ":farmstead:" + i);
                ulong hash = Hash(seed, id);
                int widthFeet = acreage ? 150 + (int)((hash >> 11) % 7) * 25 : 300 + (int)((hash >> 11) % 5) * 50;
                double areaMin = acreage ? settings.AcreageAreaMinAcres : settings.FarmsteadAreaMinAcres;
                double areaMax = acreage ? settings.AcreageAreaMaxAcres : settings.FarmsteadAreaMaxAcres;
                // Keep the old frontage ranges while drawing parcel area directly
                // from the configured acreage band, so every valid band changes lots.
                double areaRoll = ((hash >> 19) % 1_000_001) / 1_000_000.0;
                double area = areaMin + (areaMax - areaMin) * areaRoll;
                double depthFeet = area * 43560.0 / widthFeet;
                double w = widthFeet / 5280.0, d = depthFeet / 5280.0;

                double side = (hash & 1) == 0 ? -1 : 1;
                if (farmCount > 1) side = (i & 1) == 0 ? side : -side;
                double usable = Math.Max(.1, length - w - .08);
                double t = (w * .5 + .04 + (usable * (((hash >> 27) % 10001) / 10000.0))) / length;
                t = Math.Max(w / (2 * length), Math.Min(1 - w / (2 * length), t));
                double row=Math.Max(road.Width*.5,18.0/5280)+(15+((hash>>43)%26))/5280.0;
                double cx = a.X + dx * t + nx * side * (row + d * .5);
                double cy = a.Y + dy * t + ny * side * (row + d * .5);
                if((int)Math.Floor(cx)!=output.ChunkX||(int)Math.Floor(cy)!=output.ChunkY)continue;
                var corners = RectCorners(cx, cy, ux, uy, w, d);
                if (occupied.Any(box => BoxesOverlap(cx, cy, w, d, box.X, box.Y, box.W, box.H))) continue;
                if (!GoodTerrain(terrain, cx, cy, ux, uy, w, d) || NearWater(water, corners, cx, cy)) continue;
                var parcel=ChamferCorners(cx,cy,ux,uy,w,d,Math.Min(w,d)*(.02+.08*((hash>>48)%101)/100.0))
                    .Select(p=>new MapPoint{X=p.Item1,Y=p.Item2}).ToList();
                parcel=ParcelGeometry.ClearCorridors(parcel,(cx,cy),(roads??Array.Empty<MapFeature>()).Where(f=>f.RouteId!=road.RouteId).Concat(rails??Array.Empty<MapFeature>()),15.0/5280.0);
                if(parcel.Count<3||!ParcelGeometry.Contains(parcel,(cx,cy))||!ParcelGeometry.Contains(parcel,(a.X+dx*t+nx*side*row,a.Y+dy*t+ny*side*row)))continue;
                if(output.Features.Any(f=>f.Kind=="rural_lot"&&PolygonsOverlap(parcel.Select(p=>(p.X,p.Y)).ToList(),f.Points.Select(p=>(p.X,p.Y)).ToList())))continue;
                occupied.Add((cx, cy, w, d));
                string ownerName = FamilyNames[Hash(seed, id + ":owner") % (ulong)FamilyNames.Length];
                output.Features.Add(new MapFeature
                {
                    Kind = "rural_lot", RouteId = id, Name = ownerName, Carrier = acreage ? "Acreage" : "Farmstead",
                    Width = w, Height = d, Points = parcel
                });
                double houseWidth = (32 + (int)((hash >> 31) % 13)) / 5280.0;
                double houseDepth = (30 + (int)((hash >> 38) % 17)) / 5280.0;
                double houseBack=Math.Clamp((acreage?35:65)+(int)((hash>>53)%101),25,Math.Max(25,d*5280-houseDepth*5280-35))/5280.0;
                double houseAlong=(((hash>>23)%1001)/1000.0-.5)*Math.Max(0,w-houseWidth-70.0/5280.0);
                var houseCenter = (X: a.X + dx * t + ux*houseAlong+nx * side * (row + houseBack + houseDepth / 2),
                    Y: a.Y + dy * t + uy*houseAlong+ny * side * (row + houseBack + houseDepth / 2));
                if(RectCorners(houseCenter.X,houseCenter.Y,ux,uy,houseWidth,houseDepth).Any(p=>!ParcelGeometry.Contains(parcel,p)))
                {output.Features.RemoveAll(f=>f.RouteId==id);continue;}
                output.Features.Add(new MapFeature { Kind = "rural_building", RouteId = id + ":house", Carrier = id,
                    Name = acreage?"Country home":"Farmhouse", Width = houseWidth, Height = houseDepth,
                    Points = RectCorners(houseCenter.X, houseCenter.Y, ux, uy, houseWidth, houseDepth)
                        .Select(p => new MapPoint { X = p.X, Y = p.Y }).ToList() });
                AddRuralOutbuildings(seed, id, acreage, hash, a, dx, dy, t, ux, uy, nx, ny, side,
                    w, d, row, houseBack, houseWidth, houseDepth, houseCenter, road.Surface, terrain, settings, output);
            }
        }

        private static void AddRuralOutbuildings(int seed, string lotId, bool acreage, ulong lotHash,
            (double X, double Y) roadStart, double dx, double dy, double t, double ux, double uy, double nx, double ny,
            double side, double lotWidth, double lotDepth, double roadRow, double houseBack, double houseWidth, double houseDepth,
            (double X, double Y) houseCenter, string roadSurface, ElevationGridData terrain, WorldGenerationSettings settings, TransportChunk output)
        {
            int count = acreage ? 1 + (int)(Hash(seed, lotId + ":outbuilding-count") % 3) : 2 + (int)(Hash(seed, lotId + ":outbuilding-count") % 7);
            bool gambrel=( !acreage && Hash(seed,lotId+":gambrel-barn")%100<(ulong)settings.GambrelBarnChancePercent );
            int gambrelSlot=gambrel?(int)(Hash(seed,lotId+":gambrel-slot")%(ulong)count):-1;
            var placed = new List<((double X, double Y) Center, double Width, double Depth, string Name)>();
            double baseBack = roadRow + houseBack + houseDepth + 25.0 / 5280.0;
            for (int i = 0; i < count; i++)
            {
                ulong h = Hash(seed, lotId + ":outbuilding:" + i);
                string name = OutbuildingTypes[h % (ulong)OutbuildingTypes.Length];
                if(gambrel&&i==gambrelSlot)name="Existing gambrel barn";
                else if(gambrel&&name=="Barn")name="Machine Shed";
                var size = OutbuildingSize(name, acreage, h);
                double bw = size.WidthFt / 5280.0, bd = size.DepthFt / 5280.0;
                int row = i / 3, slot = i % 3;
                int layout=(int)(Hash(seed,lotId+":yard-layout")%3);
                double along = (slot - 1) * Math.Min(lotWidth * .27, 95.0 / 5280.0);
                if(layout==1)along=(i%2==0?-1:1)*lotWidth*.27;
                along+=((h>>16)%17-8.0)/5280.0;
                double back = baseBack + (layout==1?i/2:row) * (105.0 / 5280.0);
                if (Math.Abs(along) + bw / 2 > lotWidth / 2 - .006 || back + bd / 2 > roadRow + lotDepth - .008) continue;
                var center = (X: roadStart.X + dx * t + ux * along + nx * side * (back + bd / 2),
                    Y: roadStart.Y + dy * t + uy * along + ny * side * (back + bd / 2));
                var shape = RectCorners(center.X, center.Y, ux, uy, bw, bd);
                if (!GoodTerrain(terrain, center.X, center.Y, ux, uy, bw, bd) ||
                    placed.Any(other => PolygonsOverlap(shape,RectCorners(other.Center.X,other.Center.Y,ux,uy,other.Width,other.Depth)))) continue;
                var parcel=output.Features.First(f=>f.RouteId==lotId).Points;
                if(shape.Any(p=>!ParcelGeometry.Contains(parcel,p)))continue;
                placed.Add((center, bw, bd, name));
                output.Features.Add(new MapFeature { Kind = "rural_building", RouteId = lotId + ":outbuilding:" + i,
                    Carrier = lotId, Name = name, Width = bw, Height = bd,
                    YearBuilt=name=="Existing gambrel barn"?1989:0,
                    Points = shape.Select(p => new MapPoint { X = p.X, Y = p.Y }).ToList() });
            }
            AddFarmDriveAndPath(seed,lotId,roadStart,dx,dy,t,ux,uy,nx,ny,side,houseWidth,houseDepth,houseCenter,roadSurface,placed,output,roadRow);
            if (placed.Count > 0)
            {
                double apronWidth = Math.Min(lotWidth * .78, Math.Max(.04, placed.Sum(item => item.Width) + .025));
                double apronDepth = 22.0 / 5280.0;
                var apronCenter = (X: roadStart.X + dx * t + nx * side * (baseBack + apronDepth / 2),
                    Y: roadStart.Y + dy * t + ny * side * (baseBack + apronDepth / 2));
                var apron = RectCorners(apronCenter.X, apronCenter.Y, ux, uy, apronWidth, apronDepth);
                output.Features.Add(new MapFeature { Kind = "rural_yard", RouteId = lotId + ":yard", Carrier = lotId,
                    Points = apron.Select(p => new MapPoint { X = p.X, Y = p.Y }).ToList() });
            }
            AddShelterbelt(seed,lotId,houseCenter,houseWidth,houseDepth,ux,uy,nx,ny,placed,settings,output);
        }

        private static void AddFarmDriveAndPath(int seed,string lotId,(double X,double Y) roadStart,double dx,double dy,double t,
            double ux,double uy,double nx,double ny,double side,double houseWidth,double houseDepth,(double X,double Y) houseCenter,
            string roadSurface,IReadOnlyList<((double X,double Y) Center,double Width,double Depth,string Name)> buildings,TransportChunk output,double roadRow)
        {
            var parcel=output.Features.First(f=>f.RouteId==lotId).Points;
            var structures=output.Features.Where(f=>f.Kind=="rural_building"&&f.Carrier==lotId).ToList();
            var obstacles=structures.Select(f=>f.Points).ToList();
            var roadPoint=(X:roadStart.X+dx*t,Y:roadStart.Y+dy*t);
            var frontage=(X:roadPoint.X+nx*side*roadRow,Y:roadPoint.Y+ny*side*roadRow);
            var house=structures.First(f=>f.RouteId==lotId+":house");
            double lateral=houseWidth*.5+18.0/5280.0;
            var parking=(X:houseCenter.X+ux*lateral,Y:houseCenter.Y+uy*lateral);
            if(!ParcelGeometry.Contains(parcel,parking))parking=(houseCenter.X-ux*lateral,houseCenter.Y-uy*lateral);
            var trunk=ParcelGeometry.Access(frontage,parking,parcel,obstacles);
            if(trunk.Count<2)return;
            if(trunk.Count>1)
            {
                trunk.Insert(0,roadPoint);
                output.Features.Add(new MapFeature{Kind="rural_driveway",RouteId=lotId+":drive",Carrier=lotId,Surface="gravel",Width=14.0/5280,Points=SegmentPoints(trunk)});
            }
            int branch=0;
            foreach(var building in structures.Where(f=>f!=house))
            {
                double cx=building.Points.Average(p=>p.X),cy=building.Points.Average(p=>p.Y);
                var entry=(X:cx-nx*side*building.Height*.5,Y:cy-ny*side*building.Height*.5);
                var route=ParcelGeometry.Access(parking,entry,parcel,obstacles);
                if(route.Count<2)continue;
                output.Features.Add(new MapFeature{Kind="rural_driveway",RouteId=lotId+":drive:branch:"+branch++,Carrier=lotId,
                    Surface="gravel",Width=12.0/5280,Points=SegmentPoints(route)});
            }
            var door=(X:houseCenter.X-nx*side*houseDepth*.5,Y:houseCenter.Y-ny*side*houseDepth*.5);
            var walk=ParcelGeometry.Access(parking,door,parcel,obstacles);
            if(walk.Count>1)output.Features.Add(new MapFeature{Kind="rural_path",RouteId=lotId+":path",Carrier=lotId,
                Surface="concrete",Width=4.0/5280,Points=SegmentPoints(walk)});
        }

        private static void AddShelterbelt(int seed,string lotId,(double X,double Y) house,double houseWidth,double houseDepth,
            double ux,double uy,double nx,double ny,IReadOnlyList<((double X,double Y) Center,double Width,double Depth,string Name)> buildings,
            WorldGenerationSettings settings,TransportChunk output)
        {
            var points=RectCorners(house.X,house.Y,ux,uy,houseWidth,houseDepth);
            foreach(var building in buildings)points.AddRange(RectCorners(building.Center.X,building.Center.Y,ux,uy,building.Width,building.Depth));
            double minX=points.Min(p=>p.X),maxX=points.Max(p=>p.X),minY=points.Min(p=>p.Y),maxY=points.Max(p=>p.Y);
            int rows=settings.ShelterbeltMinRows+(int)(Hash(seed,lotId+":shelterbelt-rows")%(ulong)(settings.ShelterbeltMaxRows-settings.ShelterbeltMinRows+1));
            double spacing=28.0/5280.0,treeSpacing=42.0/5280.0;
            int countX=Math.Max(1,(int)Math.Ceiling((maxX-minX)/treeSpacing)),countY=Math.Max(1,(int)Math.Ceiling((maxY-minY)/treeSpacing));
            for(int row=0;row<rows;row++)
            {
                double northY=maxY+(row+1)*spacing,westX=minX-(row+1)*spacing;
                for(int i=0;i<=countX;i++)AddTreeMarker(minX+(maxX-minX)*i/countX,northY,row,i);
                for(int i=0;i<=countY;i++)AddTreeMarker(westX,minY+(maxY-minY)*i/countY,row,i+countX+1);
            }
            void AddTreeMarker(double x,double y,int row,int index)=>output.TreeReservations.Add(new MapFeature{Kind="tree_marker",
                RouteId=$"{lotId}:shelterbelt:{row}:{index}",Carrier=lotId,Name="Farm shelterbelt",Width=.0012,
                Points=new List<MapPoint>{new(){X=x,Y=y}}});
        }

        private static (double WidthFt, double DepthFt) OutbuildingSize(string name, bool acreage, ulong hash)
        {
            return name switch
            {
                "Machine Shed" => (acreage ? 36 : 50, acreage ? 48 : 80),
                "Barn" or "Existing gambrel barn" => (acreage ? 30 : 40, acreage ? 42 : 60),
                "Grain Bin" => (30, 30), "Corn Crib" => (24, 34),
                "Hog Building" => (36, 64), "Poultry House" => (38, 72),
                _ => (48, 70)
            };
        }

        private static int WeightedFarmCount(ulong hash, WorldGenerationSettings settings)
        {
            int roll = (int)(hash % 100);
            return roll < settings.FarmCountZeroBelowPercent ? 0 : roll < settings.FarmCountOneBelowPercent ? 1 : 2;
        }

        private static List<(double X, double Y)> RectCorners(double cx, double cy, double ux, double uy, double width, double depth)
        {
            double nx = -uy, ny = ux, hw = width * .5, hd = depth * .5;
            return new List<(double X, double Y)>
            {
                (cx-ux*hw-nx*hd, cy-uy*hw-ny*hd), (cx+ux*hw-nx*hd, cy+uy*hw-ny*hd),
                (cx+ux*hw+nx*hd, cy+uy*hw+ny*hd), (cx-ux*hw+nx*hd, cy-uy*hw+ny*hd)
            };
        }

        private static List<MapPoint> SegmentPoints(IReadOnlyList<(double X,double Y)> points)
        {
            var result=new List<MapPoint>();
            for(int i=0;i+1<points.Count;i++)
            {result.Add(new MapPoint{X=points[i].X,Y=points[i].Y});result.Add(new MapPoint{X=points[i+1].X,Y=points[i+1].Y});}
            return result;
        }

        private static bool NearLot((double X,double Y) point,IReadOnlyList<MapPoint> polygon,double radius)
        {
            if(polygon==null||polygon.Count<3)return false;
            bool inside=false;
            for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++)
            {
                var a=polygon[i];var b=polygon[j];
                if((a.Y>point.Y)!=(b.Y>point.Y)&&point.X<(b.X-a.X)*(point.Y-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;
                if(SegmentDistance(point,(a.X,a.Y),(b.X,b.Y))<=radius)return true;
            }
            return inside;
        }

        private static bool PolygonsOverlap(IReadOnlyList<(double X,double Y)> a,IReadOnlyList<(double X,double Y)> b)
        {
            if(a==null||b==null||a.Count<3||b.Count<3)return false;
            var aPoints=a.Select(point=>new MapPoint{X=point.X,Y=point.Y}).ToList();
            var bPoints=b.Select(point=>new MapPoint{X=point.X,Y=point.Y}).ToList();
            foreach(var point in a)if(NearLot(point,bPoints,0))return true;
            foreach(var point in b)if(NearLot(point,aPoints,0))return true;
            for(int i=0;i<a.Count;i++)for(int j=0;j<b.Count;j++)
                if(TryIntersection(a[i],a[(i+1)%a.Count],b[j],b[(j+1)%b.Count],out _))return true;
            return false;
        }

        private static bool TryIntersection((double X,double Y) a,(double X,double Y) b,
            (double X,double Y) c,(double X,double Y) d,out (double X,double Y) point)
        {
            double rx=b.X-a.X,ry=b.Y-a.Y,sx=d.X-c.X,sy=d.Y-c.Y,den=rx*sy-ry*sx;
            if(Math.Abs(den)<1e-12){point=default;return false;}
            double qx=c.X-a.X,qy=c.Y-a.Y,t=(qx*sy-qy*sx)/den,u=(qx*ry-qy*rx)/den;
            if(t< -1e-9||t>1+1e-9||u< -1e-9||u>1+1e-9){point=default;return false;}
            point=(a.X+t*rx,a.Y+t*ry);return true;
        }

        private static ((double X,double Y) Point,double Distance) NearestRailPoint((double X,double Y) point,IReadOnlyList<MapFeature> rails)
        {
            var nearest=(Point:default((double X,double Y)),Distance:double.PositiveInfinity);
            foreach(var rail in rails??Array.Empty<MapFeature>())
            {
                if(rail.Kind!="rail"||rail.Points==null)continue;
                for(int i=0;i+1<rail.Points.Count;i+=2)
                {
                    var a=(rail.Points[i].X,rail.Points[i].Y);var b=(rail.Points[i+1].X,rail.Points[i+1].Y);
                    double distance=SegmentDistance(point,a,b);
                    if(distance<nearest.Distance)
                    {double dx=b.X-a.X,dy=b.Y-a.Y,len=dx*dx+dy*dy,t=len<1e-12?0:Math.Clamp(((point.X-a.X)*dx+(point.Y-a.Y)*dy)/len,0,1);
                     nearest=((a.X+t*dx,a.Y+t*dy),distance);}
                }
            }
            return nearest;
        }

        private static double Distance((double X,double Y) a,(double X,double Y) b)
        {double dx=a.X-b.X,dy=a.Y-b.Y;return Math.Sqrt(dx*dx+dy*dy);}

        private static int FloorMultiple(int value,int size)=>(int)Math.Floor(value/(double)size)*size;

        private static List<(double X, double Y)> ChamferCorners(double cx, double cy, double ux, double uy,
            double width, double depth, double chamfer)
        {
            double vx = -uy, vy = ux, hw = width * .5, hd = depth * .5;
            double cut = Math.Min(chamfer, Math.Min(hw, hd) * .45);
            var local = new (double X, double Y)[]
            {
                (-hw + cut, -hd), (hw - cut, -hd), (hw, -hd + cut), (hw, hd - cut),
                (hw - cut, hd), (-hw + cut, hd), (-hw, hd - cut), (-hw, -hd + cut)
            };
            return local.Select(p => (cx + ux * p.X + vx * p.Y, cy + uy * p.X + vy * p.Y)).ToList();
        }

        private static bool GoodTerrain(ElevationGridData grid, double x, double y, double ux, double uy, double w, double d)
        {
            if (grid?.Values == null || grid.Values.Count < 2) return true;
            double center = TransportSpawner.SampleElevation(grid, x, y), variation = 0;
            foreach (var p in RectCorners(x, y, ux, uy, w * .8, d * .8))
                variation = Math.Max(variation, Math.Abs(TransportSpawner.SampleElevation(grid, p.X, p.Y) - center));
            return center > -35 && variation < 65;
        }

        private static bool NearWater(IReadOnlyList<MapFeature> water, IReadOnlyList<(double X, double Y)> corners, double cx, double cy)
        {
            double left = corners.Min(p => p.X), right = corners.Max(p => p.X), top = corners.Min(p => p.Y), bottom = corners.Max(p => p.Y);
            foreach (var feature in water ?? Array.Empty<MapFeature>())
            {
                if (feature.Points == null) continue;
                if (feature.Kind == "lakes" && feature.Points.Count >= 3 && feature.Points.Max(p => p.X) >= left && feature.Points.Min(p => p.X) <= right &&
                    feature.Points.Max(p => p.Y) >= top && feature.Points.Min(p => p.Y) <= bottom&&PolygonsOverlap(corners,feature.Points.Select(p=>(p.X,p.Y)).ToList())) return true;
                if (feature.Kind == "rivers")
                    for (int i = 0; i + 1 < feature.Points.Count; i += 2)
                        if(NearLot((feature.Points[i].X,feature.Points[i].Y),corners.Select(p=>new MapPoint{X=p.X,Y=p.Y}).ToList(),feature.Width*.5+15.0/5280)||corners.Any(p=>SegmentDistance(p,(feature.Points[i].X,feature.Points[i].Y),(feature.Points[i+1].X,feature.Points[i+1].Y))<feature.Width*.5+15.0/5280)||
                           Enumerable.Range(0,corners.Count).Any(j=>TryIntersection(corners[j],corners[(j+1)%corners.Count],(feature.Points[i].X,feature.Points[i].Y),(feature.Points[i+1].X,feature.Points[i+1].Y),out _)))return true;
            }
            return false;
        }

        private static bool BoxesOverlap(double x, double y, double w, double h, double ox, double oy, double ow, double oh) =>
            Math.Abs(x - ox) * 2 < w + ow && Math.Abs(y - oy) * 2 < h + oh;

        private static double SegmentDistance((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, len = dx * dx + dy * dy;
            double t = len < 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len));
            return Math.Sqrt(Math.Pow(p.X - a.X - t * dx, 2) + Math.Pow(p.Y - a.Y - t * dy, 2));
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

