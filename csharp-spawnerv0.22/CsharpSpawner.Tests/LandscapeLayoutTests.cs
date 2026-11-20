using CsharpSpawner;
using System.Text.Json;

namespace CsharpSpawner.Tests;

[Collection("World generation")]
public sealed class LandscapeLayoutTests
{
    [Fact]
    public void RailTurnsPreserveEndpointsAndSpreadHeadingChange()
    {
        var source=new[]{(X:0.0,Y:0.0),(X:0.0,Y:1.0),(X:2.0,Y:1.0)};
        var route=TransportSpawner.SmoothRailAlignment(source,.3);
        Assert.Equal(source[0],route[0]);Assert.Equal(source[^1],route[^1]);
        Assert.True(route.Count>12);
        for(int i=1;i+1<route.Count;i++)
        {
            double ax=route[i].X-route[i-1].X,ay=route[i].Y-route[i-1].Y;
            double bx=route[i+1].X-route[i].X,by=route[i+1].Y-route[i].Y;
            double cosine=(ax*bx+ay*by)/Math.Sqrt((ax*ax+ay*ay)*(bx*bx+by*by));
            Assert.True(Math.Acos(Math.Clamp(cosine,-1,1))*180/Math.PI<20);
        }
    }

    [Fact]
    public void TownAccessReachesActualDoorAndGarageWithoutCrossingBuildings()
    {
        var site=new MapFeature{Kind="settlement",RouteId="access:town",Population=60,Points=new(){new(){X=.5,Y=.5}}};
        var residents=Enumerable.Range(0,60).Select(i=>new Settler{Id=$"person:{i}",Name=$"Person{i} Smith",ClusterId=site.RouteId}).ToList();
        var result=TownSpawner.GenerateChunk(33,new TownInput{ChunkX=0,ChunkY=0},new[]{site},Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),new WorldGenerationSettings{GarageChancePercent=100},residents);
        var houses=result.Features.Where(f=>f.Kind=="town_building").ToList();Assert.NotEmpty(houses);
        foreach(var house in houses)
        {
            var walk=Assert.Single(result.Features,f=>f.Kind=="town_door_walk"&&f.Carrier==house.Carrier);
            var door=WallPlanSpawner.Entrance(house);
            Assert.True(Distance(walk.Points[^1],door)<1e-8);
            var structures=result.Features.Where(f=>f.Carrier==house.Carrier&&f.Kind is "town_building" or "town_garage").ToList();
            var drive=Assert.Single(result.Features,f=>f.Kind=="town_driveway"&&f.Carrier==house.Carrier);
            foreach(var path in new[]{walk,drive})AssertClear(path,structures);
            var garage=structures.FirstOrDefault(f=>f.Kind=="town_garage");
            if(garage!=null)Assert.True(Enumerable.Range(0,4).Min(i=>DistanceToEdge(drive.Points[^1],garage.Points[i],garage.Points[(i+1)%4]))<1e-8);
        }
    }

    [Theory]
    [InlineData(33)]
    [InlineData(19)]
    public void RuralBuildingsFitParcelsAndAccessDoesNotCrossBuildings(int seed)
    {
        var roads=new[]{new MapFeature{Kind="local_road",RouteId="section:ew:0:0",Surface="gravel",Width=.008,Points=new(){new(){X=0,Y=.5},new(){X=1,Y=.5}}}};
        var settings=new WorldGenerationSettings{FarmCountZeroBelowPercent=0,FarmCountOneBelowPercent=0,AcreageChancePercent=100};
        var result=RuralSpawner.GenerateChunk(seed,new TownInput{ChunkX=0,ChunkY=0},roads,Array.Empty<MapFeature>(),settings);
        var lots=result.Features.Where(f=>f.Kind=="rural_lot").ToDictionary(f=>f.RouteId);Assert.NotEmpty(lots);
        var buildings=result.Features.Where(f=>f.Kind=="rural_building").ToList();Assert.NotEmpty(buildings);
        foreach(var building in buildings)Assert.All(building.Points,p=>Assert.True(ParcelGeometry.Contains(lots[building.Carrier].Points,(p.X,p.Y))));
        foreach(var lot in lots.Values)
        {
            var structures=buildings.Where(f=>f.Carrier==lot.RouteId).ToList();
            Assert.Contains(result.Features,f=>f.Kind=="rural_driveway"&&f.Carrier==lot.RouteId);
            foreach(var path in result.Features.Where(f=>f.Carrier==lot.RouteId&&f.Kind is "rural_driveway" or "rural_path"))AssertClear(path,structures);
        }
        Assert.Equal(JsonSerializer.Serialize(result),JsonSerializer.Serialize(RuralSpawner.GenerateChunk(seed,new TownInput{ChunkX=0,ChunkY=0},roads,Array.Empty<MapFeature>(),settings)));
    }

    [Fact]
    public void TownParcelsFollowRiverAndRailClearancesAndRoadsGetBridges()
    {
        var site=new MapFeature{Kind="settlement",RouteId="corridor:town",Population=60,Points=new(){new(){X=.5,Y=.5}}};
        var road=new MapFeature{Kind="main_road",RouteId="main",Surface="paved",Width=.02,Points=new(){new(){X=0,Y=.5},new(){X=1,Y=.5}}};
        var rail=new MapFeature{Kind="rail",RouteId="rail",Width=.01,Points=new(){new(){X=.54,Y=0},new(){X=.54,Y=1}}};
        var river=new MapFeature{Kind="rivers",RouteId="river",Width=.012,Points=new(){new(){X=.4,Y=.2},new(){X=.65,Y=.8}}};
        var residents=Enumerable.Range(0,60).Select(i=>new Settler{Id=$"river:{i}",Name=$"Person{i} Smith",ClusterId=site.RouteId}).ToList();
        var result=TownSpawner.GenerateChunk(33,new TownInput{ChunkX=0,ChunkY=0},new[]{site},new[]{rail},new[]{road},new[]{river},Array.Empty<MapFeature>(),new WorldGenerationSettings(),residents);
        Assert.True(result.Features.Where(f=>f.Kind=="town_building").Sum(f=>f.HousingSlots)>=60);
        Assert.Contains(result.Features,f=>f.Kind=="bridges"&&f.Carrier=="town_street");
        foreach(var lot in result.Features.Where(f=>f.Kind=="town_lot"))
            Assert.All(lot.Points,p=>Assert.True(Math.Abs(p.X-.54)>=.005+12.0/5280-1e-9));
        Assert.Empty(TownSpawner.FindTownLotClearanceViolations(result.Features.Concat(new[]{rail,river,road}).ToList()));
    }

    [Fact]
    public void PondAndLakeBasinsHaveDifferentSizesAndIrregularShorelines()
    {
        var settings=new WorldGenerationSettings();WorldGenerator.Configure(settings);
        var lakes=new List<WorldGenerator.LakeBasin>();var ponds=new List<WorldGenerator.LakeBasin>();
        for(int y=-4;y<=4;y++)for(int x=-4;x<=4;x++)
        {
            var lake=WorldGenerator.GetSmallLakeBasin(33,x,y,false);if(lake.Active)lakes.Add(lake);
            var pond=WorldGenerator.GetSmallLakeBasin(33,x,y,true);if(pond.Active)ponds.Add(pond);
        }
        Assert.NotEmpty(lakes);Assert.NotEmpty(ponds);
        Assert.True(ponds.Max(p=>p.Radius)<lakes.Min(p=>p.Radius));
        Assert.True(lakes.Select(l=>Math.Round(l.Aspect,1)).Distinct().Count()>2);
        var basin=ponds.First(p=>p.Aspect<.8);
        double center=WorldGenerator.AddOccasionalLakeBasin(33,basin.X,basin.Y,settings.SeaLevelFeet+65);
        Assert.True(center<settings.SeaLevelFeet);
        double ca=Math.Cos(basin.Angle),sa=Math.Sin(basin.Angle),radius=basin.Radius*.65;
        double major=WorldGenerator.AddOccasionalLakeBasin(33,basin.X+ca*radius,basin.Y+sa*radius,settings.SeaLevelFeet+65);
        double minor=WorldGenerator.AddOccasionalLakeBasin(33,basin.X-sa*radius,basin.Y+ca*radius,settings.SeaLevelFeet+65);
        Assert.True(Math.Abs(major-minor)>1);
    }

    static void AssertClear(MapFeature path,List<MapFeature> buildings)
    {
        for(int i=0;i+1<path.Points.Count;i+=2)
        foreach(var building in buildings)
            Assert.False(ParcelGeometry.CrossesInterior((path.Points[i].X,path.Points[i].Y),(path.Points[i+1].X,path.Points[i+1].Y),building.Points),$"{path.RouteId} crosses {building.RouteId}");
    }
    static double Distance(MapPoint p,(double X,double Y) q)=>Math.Sqrt(Math.Pow(p.X-q.X,2)+Math.Pow(p.Y-q.Y,2));
    static double DistanceToEdge(MapPoint p,MapPoint a,MapPoint b)
    {double dx=b.X-a.X,dy=b.Y-a.Y,t=Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/(dx*dx+dy*dy),0,1);return Distance(p,(a.X+t*dx,a.Y+t*dy));}
}
