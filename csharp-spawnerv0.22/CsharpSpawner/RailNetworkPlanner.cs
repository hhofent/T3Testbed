using System;
using System.Collections.Generic;
using System.Linq;

namespace CsharpSpawner;

// The through alignment exists independently of the requested chunks and towns.
// Every secondary route starts at an exact vertex on that alignment.
internal static class RailNetworkPlanner
{
    internal sealed record Route(string Id,string Name,List<(double X,double Y)> Points);
    internal const double SampleMiles=.025;

    internal static double MainNorthing(int seed,double x)
    {
        double phase=(Hash(seed,"main-phase")%6283)/1000.0;
        return 2.5+.035*Math.Sin(x*.065+phase)+.015*Math.Sin(x*.19+phase*.7);
    }

    internal static List<(double X,double Y)> MainPoints(int seed,int chunkX)
        => Enumerable.Range(0,41).Select(i=>(X:chunkX+i*SampleMiles,Y:MainNorthing(seed,chunkX+i*SampleMiles))).ToList();

    internal static List<Route> BuildBranches(int seed,IReadOnlyList<MapFeature> towns,IReadOnlyList<MapFeature> roads)
    {
        var candidates=new List<Route>();
        foreach(var town in (towns??Array.Empty<MapFeature>()).Where(t=>t.Points?.Count>0&&!string.IsNullOrEmpty(t.RouteId))
            .GroupBy(t=>t.RouteId,StringComparer.Ordinal).Select(g=>g.First()).OrderBy(t=>t.RouteId,StringComparer.Ordinal))
        {
            var site=(X:town.Points[0].X,Y:town.Points[0].Y);
            var setbackNormal=TerminalNormal(site,roads);
            if(setbackNormal.Y*(MainNorthing(seed,site.X)-site.Y)<0)setbackNormal=(-setbackNormal.X,-setbackNormal.Y);
            double yardSide=(Hash(seed,town.RouteId+":yard-side")&1)==0?1:-1;
            // A terminal occupies its own yard beside the town road, rather
            // than ending in the central road intersection.
            var end=(X:site.X+setbackNormal.X*.04-setbackNormal.Y*yardSide*.025,
                     Y:site.Y+setbackNormal.Y*.04+setbackNormal.X*yardSide*.025);
            double distance=Math.Abs(end.Y-MainNorthing(seed,end.X));
            // Small settlements receive rail only occasionally. Do not build a
            // separate line beside the trunk for sites it already serves.
            if(distance<.35||distance>2.5||!town.IsMajorTown&&Hash(seed,town.RouteId+":branch")%5!=0)continue;
            Route chosen=null;
            for(int variant=0;variant<12;variant++)
            {
                double direction=((Hash(seed,town.RouteId+":switch-side")&1)==0?1:-1)*(variant<6?1:-1);
                double run=.30+(variant%6)*.12;
                double junctionX=Math.Round((end.X-direction*run)/SampleMiles)*SampleMiles;
                var start=(X:junctionX,Y:MainNorthing(seed,junctionX));
                double toTown=end.Y>start.Y?1:-1;
                double tangent=(MainNorthing(seed,junctionX+.001)-MainNorthing(seed,junctionX-.001))/.002;
                // Leave the main tangentially, then turn toward the terminal.
                // A north/south terminal approach crosses an east/west town road squarely.
                var p1=(X:start.X+direction*run*.65,Y:start.Y+direction*run*.65*tangent);
                var terminalNormal=TerminalNormal(end,roads);
                if(terminalNormal.X*(end.X-start.X)+terminalNormal.Y*(end.Y-start.Y)<0)terminalNormal=(-terminalNormal.X,-terminalNormal.Y);
                double approach=Math.Min(distance*.55,.75);
                var p2=(X:end.X-terminalNormal.X*approach,Y:end.Y-terminalNormal.Y*approach);
                int samples=Math.Max(16,(int)Math.Ceiling((distance+run)/.025));
                var points=new List<(double X,double Y)>();
                for(int i=0;i<=samples;i++)
                {
                    double t=i/(double)samples,u=1-t;
                    points.Add((u*u*u*start.X+3*u*u*t*p1.X+3*u*t*t*p2.X+t*t*t*end.X,
                        u*u*u*start.Y+3*u*u*t*p1.Y+3*u*t*t*p2.Y+t*t*t*end.Y));
                }
                if(TransportSpawner.HasShallowRoadCrossing(points,roads))continue;
                chosen=new Route("rail:branch:"+town.RouteId,town.Name,points);break;
            }
            if(chosen!=null)candidates.Add(chosen);
        }
        // Reject competing branches rather than drawing a second nearby track.
        // Local priority is deterministic, so overlapping queries agree.
        return candidates.Where(route=>!candidates.Any(other=>string.CompareOrdinal(other.Id,route.Id)<0&&Competes(route,other))).ToList();
    }

    private static (double X,double Y) TerminalNormal((double X,double Y) site,IReadOnlyList<MapFeature> roads)
    {
        double best=.05;var normal=(X:0.0,Y:1.0);
        foreach(var road in roads??Array.Empty<MapFeature>())
        for(int i=0;i+1<road.Points.Count;i+=2)
        {
            var a=(X:road.Points[i].X,Y:road.Points[i].Y);var b=(X:road.Points[i+1].X,Y:road.Points[i+1].Y);
            double distance=Distance(site,a,b),dx=b.X-a.X,dy=b.Y-a.Y,length=Math.Sqrt(dx*dx+dy*dy);
            if(distance>=best||length<1e-9)continue;
            best=distance;normal=(-dy/length,dx/length);
        }
        return normal;
    }

    private static bool Competes(Route a,Route b)
    {
        var ae=a.Points[^1];var be=b.Points[^1];
        if(Math.Abs(ae.X-be.X)>3||Math.Abs(ae.Y-be.Y)>5)return false;
        for(int i=0;i+1<a.Points.Count;i++)for(int j=0;j+1<b.Points.Count;j++)
        {
            var p=a.Points[i];var q=a.Points[i+1];var r=b.Points[j];var s=b.Points[j+1];
            if(Intersects(p,q,r,s))return true;
            // Protect the switch throat, where separation must grow gradually.
            if(i<4&&j<4)continue;
            double dx=q.X-p.X,dy=q.Y-p.Y,ex=s.X-r.X,ey=s.Y-r.Y;
            double lengths=Math.Sqrt((dx*dx+dy*dy)*(ex*ex+ey*ey));
            if(lengths>1e-12&&Math.Abs(dx*ey-dy*ex)/lengths<.35&&Distance(p,r,s)<.25)return true;
        }
        return false;
    }

    private static bool Intersects((double X,double Y) a,(double X,double Y) b,(double X,double Y) c,(double X,double Y) d)
    {
        double rx=b.X-a.X,ry=b.Y-a.Y,sx=d.X-c.X,sy=d.Y-c.Y,den=rx*sy-ry*sx;
        if(Math.Abs(den)<1e-12)return false;
        double t=((c.X-a.X)*sy-(c.Y-a.Y)*sx)/den,u=((c.X-a.X)*ry-(c.Y-a.Y)*rx)/den;
        return t>0&&t<1&&u>0&&u<1;
    }
    private static double Distance((double X,double Y) p,(double X,double Y) a,(double X,double Y) b)
    {
        double dx=b.X-a.X,dy=b.Y-a.Y,t=Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/Math.Max(1e-20,dx*dx+dy*dy),0,1);
        return Math.Sqrt(Math.Pow(p.X-a.X-t*dx,2)+Math.Pow(p.Y-a.Y-t*dy,2));
    }
    private static ulong Hash(int seed,string text)
    {
        unchecked{ulong h=14695981039346656037UL^(uint)seed;foreach(char c in text){h^=c;h*=1099511628211UL;}return h^(h>>32);}
    }
}
