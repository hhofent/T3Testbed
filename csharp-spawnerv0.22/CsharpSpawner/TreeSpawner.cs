using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CsharpSpawner;

/// <summary>Final vegetation pass. Trees are trunk markers only and consume saved upstream features.</summary>
public static class TreeSpawner
{
    private static readonly JsonSerializerOptions JsonOptions=new(){IncludeFields=true};

    public static TreeChunk GenerateChunk(int seed,TreeInput input,IReadOnlyList<MapFeature> rural,
        IReadOnlyList<MapFeature> towns,IReadOnlyList<MapFeature> roads,IReadOnlyList<MapFeature> rails,
        IReadOnlyList<MapFeature> water,WorldGenerationSettings settings=null,IReadOnlyList<MapFeature> reservations=null)
    {
        settings??=new WorldGenerationSettings();
        var output=new TreeChunk{ChunkX=input.ChunkX,ChunkY=input.ChunkY};
        var localRural=NearChunk(rural,input.ChunkX,input.ChunkY,.08);
        var localTowns=NearChunk(towns,input.ChunkX,input.ChunkY,.12);
        var localRoads=NearChunk(roads.Concat(towns??Array.Empty<MapFeature>()),input.ChunkX,input.ChunkY,.04)
            .Where(f=>f.Kind is "main_road" or "local_road" or "town_street" or "town_alley").ToList();
        var localRails=NearChunk(rails,input.ChunkX,input.ChunkY,.05);
        var localWater=NearChunk(water,input.ChunkX,input.ChunkY,.04);
        var buildings=localRural.Concat(localTowns).Where(IsBuilding).ToList();
        var drives=localRural.Concat(localTowns).Where(f=>f.Kind is "rural_driveway" or "town_driveway").ToList();

        // Phase 8 reserved these exact marker locations for farm shelterbelts.
        foreach(var marker in NearChunk(reservations,input.ChunkX,input.ChunkY,.08).Where(f=>f.Points?.Count>0))
            Add(marker.Points[0].X,marker.Points[0].Y,marker.RouteId,marker.Name,marker.Carrier,marker.Width);

        if(settings.TreeDensityPercent>0)
        {
            // Seeded chance is applied per one-mile section: one grove per four sections by default.
            int groupSize=Math.Max(1,settings.TreeGroveSectionsPerGrove);
            for(int gy=input.ChunkY;gy<=input.ChunkY;gy++)for(int gx=input.ChunkX;gx<=input.ChunkX;gx++)
            {
                ulong groupHash=Hash(seed,$"tree-grove:{gx}:{gy}");
                if(groupHash%(ulong)groupSize!=0)continue;
                double cx=gx+((Hash(seed,$"tree-grove-x:{gx}:{gy}")%10000)/10000.0);
                double cy=gy+((Hash(seed,$"tree-grove-y:{gx}:{gy}")%10000)/10000.0);
                int count=Math.Max(2,(int)Math.Round(2+settings.TreeDensityPercent*.10));
                for(int n=0;n<count;n++)
                {
                    double angle=(Hash(seed,$"tree-grove-angle:{gx}:{gy}:{n}")%100000)/100000.0*Math.PI*2;
                    double radius=(.012+(Hash(seed,$"tree-grove-radius:{gx}:{gy}:{n}")%10000)/10000.0*.045)*Math.Sqrt(settings.TreeDensityPercent/20.0);
                    Add(cx+Math.Cos(angle)*radius,cy+Math.Sin(angle)*radius,$"tree:grove:{gx}:{gy}:{n}","Grove tree","",.0012);
                }
            }

            // Sparse bank trees, at irregularly spaced samples on either bank.
            foreach(var river in localWater.Where(f=>f.Kind=="rivers"&&f.Points?.Count>=2))
            for(int seg=0;seg+1<river.Points.Count;seg+=2)
            {
                var a=river.Points[seg];var b=river.Points[seg+1];double dx=b.X-a.X,dy=b.Y-a.Y,len=Math.Sqrt(dx*dx+dy*dy);
                if(len<.035)continue;double ux=dx/len,uy=dy/len,nx=-uy,ny=ux;
                int samples=(int)Math.Ceiling(len/.12);
                for(int n=0;n<samples;n++)
                {
                    ulong h=Hash(seed,$"tree-bank:{river.RouteId}:{seg}:{n}");if(h%100UL>=(ulong)settings.TreeDensityPercent)continue;
                    double t=(n+.5)/samples,offset=Math.Max(.008,river.Width*.5)+.008+(h%1000)/100000.0;
                    double side=(h&1)==0?1:-1;
                    Add(a.X+dx*t+nx*side*offset,a.Y+dy*t+ny*side*offset,$"tree:bank:{river.RouteId}:{seg}:{n}","River bank tree","",.0012);
                }
            }
        }

        // Fifteen percent seeded street-tree starts; density controls the additional grove/bank pass.
        foreach(var street in localRoads.Where(f=>f.Kind=="town_street"))
        {
            for(int i=0;i+1<(street.Points?.Count??0);i+=2)
            {
                var a=street.Points[i];var b=street.Points[i+1];double dx=b.X-a.X,dy=b.Y-a.Y,len=Math.Sqrt(dx*dx+dy*dy);
                if(len<.035)continue;double ux=dx/len,uy=dy/len,nx=-uy,ny=ux;int samples=(int)Math.Ceiling(len/.10);
                for(int n=0;n<samples;n++)
                {
                    ulong h=Hash(seed,$"tree-street:{street.RouteId}:{i}:{n}");if(h%100UL>=(ulong)settings.TownStreetTreeChancePercent)continue;
                    double t=(n+.5)/samples,side=(h&1)==0?1:-1,offset=street.Width*.5+.009;
                    Add(a.X+dx*t+nx*side*offset,a.Y+dy*t+ny*side*offset,$"tree:street:{street.RouteId}:{i}:{n}","Street tree","",.0011);
                }
            }
        }

        // Seeded yard trees sit inside residential parcels and clear the actual house and drive footprints.
        foreach(var lot in localTowns.Where(f=>f.Kind=="town_lot"&&f.Points?.Count>=4))
        {
            if(Hash(seed,lot.RouteId+":yard-tree")%100UL>=(ulong)settings.TreeDensityPercent)continue;
            double cx=lot.Points.Average(p=>p.X),cy=lot.Points.Average(p=>p.Y);
            double dx=(Hash(seed,lot.RouteId+":yard-tree-x")%1000)/100000.0-.005;
            double dy=(Hash(seed,lot.RouteId+":yard-tree-y")%1000)/100000.0-.005;
            Add(cx+dx,cy+dy,"tree:yard:"+lot.RouteId,"Town yard tree",lot.RouteId,.0011);
        }
        return output;

        void Add(double x,double y,string id,string name,string carrier,double size)
        {
            if((int)Math.Floor(x)!=input.ChunkX||(int)Math.Floor(y)!=input.ChunkY)return;
            var point=(X:x,Y:y);
            if(Blocked(point,localRoads,buildings,drives,localRails,localWater,size))return;
            output.Features.Add(new MapFeature{Kind="tree_marker",RouteId=id,Name=name,Carrier=carrier,Width=size,
                Points=new List<MapPoint>{new(){X=x,Y=y}}});
        }
    }

    public static async Task<int> RunWorkerAsync()
    {
        try
        {
            var request=JsonSerializer.Deserialize<TreeRequest>(await Console.In.ReadToEndAsync().ConfigureAwait(false),JsonOptions)
                ??throw new InvalidOperationException("Missing tree worker request.");
            using var stdout=new System.IO.StreamWriter(Console.OpenStandardOutput()){AutoFlush=true};
            var ruralIndex = new FeatureSpatialIndex(request.RuralFeatures);
            var townIndex = new FeatureSpatialIndex(request.TownFeatures);
            var roadIndex = new FeatureSpatialIndex(request.RoadFeatures);
            var railIndex = new FeatureSpatialIndex(request.RailFeatures);
            var waterIndex = new FeatureSpatialIndex(request.WaterFeatures);
            foreach(var input in request.Chunks??new())
            {
                var chunk=GenerateChunk(request.Seed,input,ruralIndex.QueryChunk(input.ChunkX,input.ChunkY,.08),
                    townIndex.QueryChunk(input.ChunkX,input.ChunkY,.12),
                    roadIndex.QueryChunk(input.ChunkX,input.ChunkY,.04).Concat(townIndex.QueryChunk(input.ChunkX,input.ChunkY,.04)).ToList(),
                    railIndex.QueryChunk(input.ChunkX,input.ChunkY,.05),
                    waterIndex.QueryChunk(input.ChunkX,input.ChunkY,.04),request.Settings,
                    (request.TreeReservations??new()).Where(f=>f.Points?.Count>0&&f.Points[0].X>=input.ChunkX-.08&&f.Points[0].X<=input.ChunkX+1.08&&f.Points[0].Y>=input.ChunkY-.08&&f.Points[0].Y<=input.ChunkY+1.08).ToList());
                await stdout.WriteLineAsync(JsonSerializer.Serialize(chunk,JsonOptions)).ConfigureAwait(false);
            }
            return 0;
        }
        catch(Exception ex){await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false);return 1;}
    }

    private static bool IsBuilding(MapFeature f)=>f.Kind is "rural_building" or "town_building" or "town_garage" or
        "town_business" or "town_business_aux" or "cattle_office_shed" or "rural_coop_elevator" or "rural_coop_bin";

    private static List<MapFeature> NearChunk(IEnumerable<MapFeature> input,int x,int y,double margin)=>
        (input??Array.Empty<MapFeature>()).Where(f=>f.Points?.Count>0&&f.Points.Max(p=>p.X)>=x-margin&&f.Points.Min(p=>p.X)<=x+1+margin&&
            f.Points.Max(p=>p.Y)>=y-margin&&f.Points.Min(p=>p.Y)<=y+1+margin).ToList();

    private static bool Blocked((double X,double Y) p,IReadOnlyList<MapFeature> roads,IReadOnlyList<MapFeature> buildings,
        IReadOnlyList<MapFeature> drives,IReadOnlyList<MapFeature> rails,IReadOnlyList<MapFeature> water,double treeRadius)
    {
        foreach(var road in roads??Array.Empty<MapFeature>())
            if(PolylineDistance(p,road.Points)<road.Width*.5+treeRadius+.001)return true;
        foreach(var feature in (buildings??Array.Empty<MapFeature>()).Concat(drives??Array.Empty<MapFeature>()))
        {
            if(feature.Points==null)continue;
            if(IsPolygon(feature)&&Contains(feature.Points,p))return true;
            if(feature.Points.Count>=2&&PolylineDistance(p,feature.Points)<treeRadius+.002)return true;
        }
        foreach(var rail in rails??Array.Empty<MapFeature>())
            if(PolylineDistance(p,rail.Points)<Math.Max(.006,rail.Width*.5)+treeRadius)return true;
        foreach(var waterFeature in water??Array.Empty<MapFeature>())
        {
            if(waterFeature.Kind=="rivers"&&PolylineDistance(p,waterFeature.Points)<Math.Max(.006,waterFeature.Width*.5)+treeRadius)return true;
            if(waterFeature.Kind=="lakes"&&waterFeature.Points?.Count>=3&&Contains(waterFeature.Points,p))return true;
        }
        return false;
    }

    private static bool IsPolygon(MapFeature f)=>f.Closed||f.Kind is "rural_lot" or "town_lot" or "town_business_lot" or "cattle_yard" or
        "rural_building" or "town_building" or "town_garage" or "town_business" or "town_business_aux" or "cattle_office_shed";
    private static bool Contains(IReadOnlyList<MapPoint> p,(double X,double Y) v)
    {
        bool inside=false;for(int i=0,j=p.Count-1;i<p.Count;j=i++)
            if((p[i].Y>v.Y)!=(p[j].Y>v.Y)&&v.X<(p[j].X-p[i].X)*(v.Y-p[i].Y)/(p[j].Y-p[i].Y)+p[i].X)inside=!inside;
        return inside;
    }
    private static double PolylineDistance((double X,double Y) p,IReadOnlyList<MapPoint> points)
    {
        if(points==null||points.Count<2)return double.PositiveInfinity;double best=double.PositiveInfinity;
        for(int i=0;i+1<points.Count;i+=2)
        {
            var a=points[i];var b=points[i+1];double dx=b.X-a.X,dy=b.Y-a.Y,len=dx*dx+dy*dy;
            double t=len<1e-12?0:Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/len,0,1);
            best=Math.Min(best,Math.Sqrt(Math.Pow(p.X-a.X-t*dx,2)+Math.Pow(p.Y-a.Y-t*dy,2)));
        }
        return best;
    }
    private static ulong Hash(int seed,string text)
    {unchecked{ulong h=14695981039346656037UL;foreach(char c in text){h^=c;h*=1099511628211UL;}h^=(uint)seed;h*=1099511628211UL;return h^(h>>32);}}
}
