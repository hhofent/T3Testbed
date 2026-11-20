using System;
using System.Collections.Generic;
using System.Linq;

namespace CsharpSpawner;

// Shared convex parcel and access geometry, in map miles.
internal static class ParcelGeometry
{
    internal static bool Contains(IReadOnlyList<MapPoint> p,(double X,double Y) q,double tolerance=1e-10)
    {
        bool positive=false,negative=false;
        for(int i=0;i<p.Count;i++)
        {
            var a=p[i];var b=p[(i+1)%p.Count];
            double cross=(b.X-a.X)*(q.Y-a.Y)-(b.Y-a.Y)*(q.X-a.X);
            positive|=cross>tolerance;negative|=cross< -tolerance;
        }
        return !(positive&&negative);
    }

    internal static List<MapPoint> ClearCorridors(List<MapPoint> polygon,(double X,double Y) keep,
        IEnumerable<MapFeature> corridors,double setback)
    {
        foreach(var f in corridors.Where(f=>f.Kind is "rail" or "rivers" or "main_road" or "local_road"))
        for(int i=0;i+1<f.Points.Count&&polygon.Count>=3;i+=2)
        {
            var a=f.Points[i];var b=f.Points[i+1];
            double dx=b.X-a.X,dy=b.Y-a.Y,len=Math.Sqrt(dx*dx+dy*dy);
            if(len<1e-10)continue;
            double clearance=Math.Max(f.Width*.5, f.Kind=="rail"?20.0/5280:0)+setback;
            if(polygon.Max(p=>p.X)<Math.Min(a.X,b.X)-clearance||polygon.Min(p=>p.X)>Math.Max(a.X,b.X)+clearance||
               polygon.Max(p=>p.Y)<Math.Min(a.Y,b.Y)-clearance||polygon.Min(p=>p.Y)>Math.Max(a.Y,b.Y)+clearance)continue;
            double sign=((keep.X-a.X)*(-dy)+(keep.Y-a.Y)*dx)>=0?1:-1;
            double Distance(MapPoint p)=>sign*((p.X-a.X)*(-dy)+(p.Y-a.Y)*dx)/len-clearance;
            var clipped=new List<MapPoint>();
            for(int j=0;j<polygon.Count;j++)
            {
                var c=polygon[j];var d=polygon[(j+1)%polygon.Count];double dc=Distance(c),dd=Distance(d);
                if(dc>=0)clipped.Add(c);
                if((dc>=0)!=(dd>=0))
                {double t=dc/(dc-dd);clipped.Add(new MapPoint{X=c.X+(d.X-c.X)*t,Y=c.Y+(d.Y-c.Y)*t});}
            }
            polygon=clipped;
        }
        return polygon;
    }

    internal static List<MapPoint> Segments(IEnumerable<(double X,double Y)> route)
    {
        var points=route.ToList();var result=new List<MapPoint>();
        for(int i=0;i+1<points.Count;i++)
        {result.Add(new(){X=points[i].X,Y=points[i].Y});result.Add(new(){X=points[i+1].X,Y=points[i+1].Y});}
        return result;
    }

    internal static bool CrossesInterior((double X,double Y) a,(double X,double Y) b,IReadOnlyList<MapPoint> polygon)
    {
        // Convex clipping finds the entire interior interval, including narrow buildings.
        double orientation=0;for(int i=0;i<polygon.Count;i++)orientation+=polygon[i].X*polygon[(i+1)%polygon.Count].Y-polygon[(i+1)%polygon.Count].X*polygon[i].Y;
        double sign=orientation>=0?1:-1,low=0,high=1;
        for(int i=0;i<polygon.Count;i++)
        {
            var c=polygon[i];var d=polygon[(i+1)%polygon.Count];
            double start=sign*((d.X-c.X)*(a.Y-c.Y)-(d.Y-c.Y)*(a.X-c.X));
            double delta=sign*((d.X-c.X)*(b.Y-a.Y)-(d.Y-c.Y)*(b.X-a.X));
            if(Math.Abs(delta)<1e-15){if(start<=1e-12)return false;continue;}
            double t=(1e-12-start)/delta;
            if(delta>0)low=Math.Max(low,t);else high=Math.Min(high,t);
            if(high<=low)return false;
        }
        return high-low>1e-7&&high>1e-7&&low<1-1e-7;
    }

    internal static List<(double X,double Y)> Access((double X,double Y) start,(double X,double Y) end,
        IReadOnlyList<MapPoint> parcel,IReadOnlyList<List<MapPoint>> obstacles)
    {
        if(!Contains(parcel,start)||!Contains(parcel,end))return new();
        var nodes=new List<(double X,double Y)>{start,end};
        foreach(var obstacle in obstacles)
        {
            double cx=obstacle.Average(p=>p.X),cy=obstacle.Average(p=>p.Y);
            foreach(var p in obstacle)
            {
                double dx=p.X-cx,dy=p.Y-cy,len=Math.Sqrt(dx*dx+dy*dy);
                var q=(X:p.X+dx/len*6.0/5280,Y:p.Y+dy/len*6.0/5280);
                if(Contains(parcel,q))nodes.Add(q);
            }
        }
        var costs=Enumerable.Repeat(double.PositiveInfinity,nodes.Count).ToArray();
        var previous=Enumerable.Repeat(-1,nodes.Count).ToArray();var visited=new bool[nodes.Count];costs[0]=0;
        for(int n=0;n<nodes.Count;n++)
        {
            int current=-1;
            for(int i=0;i<nodes.Count;i++)if(!visited[i]&&(current<0||costs[i]<costs[current]))current=i;
            if(current<0||double.IsPositiveInfinity(costs[current]))break;
            if(current==1)break;visited[current]=true;
            for(int j=0;j<nodes.Count;j++)
            {
                if(visited[j]||j==current||obstacles.Any(p=>CrossesInterior(nodes[current],nodes[j],p)))continue;
                double dx=nodes[current].X-nodes[j].X,dy=nodes[current].Y-nodes[j].Y,cost=costs[current]+Math.Sqrt(dx*dx+dy*dy);
                if(cost<costs[j]){costs[j]=cost;previous[j]=current;}
            }
        }
        if(previous[1]<0)return new();
        var route=new List<(double X,double Y)>();
        for(int i=1;i>=0;i=previous[i])route.Add(nodes[i]);route.Reverse();return route;
    }
}
