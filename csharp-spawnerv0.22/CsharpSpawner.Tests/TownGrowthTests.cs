using CsharpSpawner;

namespace CsharpSpawner.Tests;

public sealed class TownGrowthTests
{
    [Fact]
    public void TownBuildsEnoughOccupiedHouseCapacityAndUsesResidentSurnames()
    {
        var site = new MapFeature { Kind="settlement", RouteId="fixture:settlement:0", Name="Fixture", Population=36,
            Points=new() { new MapPoint { X=.5, Y=.5 } } };
        var road = new MapFeature { Kind="main_road", RouteId="fixture:road", Surface="paved", Width=.02,
            Points=new() { new MapPoint { X=0, Y=.5 }, new MapPoint { X=1, Y=.5 } } };
        var nearbyRoad = new MapFeature { Kind="local_road", RouteId="fixture:nearby-road", Surface="paved", Width=.0065,
            Points=new() { new MapPoint { X=0, Y=.62 }, new MapPoint { X=1, Y=.62 } } };
        var residents = Enumerable.Range(0,36).Select(i=>new Settler { Id=$"p{i:D2}", Name=$"Person{i} van der Meer", ClusterId=site.RouteId }).ToList();
        var result=TownSpawner.GenerateChunk(33,new TownInput { ChunkX=0,ChunkY=0 },new[]{site},Array.Empty<MapFeature>(),
            new[]{road,nearbyRoad},Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),new WorldGenerationSettings(),residents);

        var houses=result.Features.Where(f=>f.Kind=="town_building").ToList();
        Assert.NotEmpty(houses);
        Assert.True(houses.Sum(h=>h.HousingSlots)>=residents.Count);
        Assert.All(result.Features.Where(f=>f.Kind=="town_lot"),lot=>Assert.Equal("van der Meer",lot.Name));
        Assert.All(result.Features.Where(f=>f.Kind is "town_street" or "town_alley"),f=>Assert.Equal("paved",f.Surface));
        Assert.Contains(result.Features,f=>f.Kind=="town_street"&&f.RouteId?.Contains(":road-link:")==true);
        Assert.Empty(TownSpawner.FindTownLotClearanceViolations(result.Features.Concat(new[]{road,nearbyRoad}).ToList()));
    }

    [Fact]
    public void TownGrowthIsStableForSameSeedAndResidents()
    {
        var site = new MapFeature { Kind="settlement", RouteId="fixture:settlement:0", Name="Fixture", Population=60,
            Points=new() { new MapPoint { X=.5, Y=.5 } } };
        var residents=Enumerable.Range(0,60).Select(i=>new Settler{Id=$"p{i:D2}",Name=$"Person{i} Smith",ClusterId=site.RouteId}).ToList();
        var input=new TownInput { ChunkX=0,ChunkY=0 };
        var first=TownSpawner.GenerateChunk(33,input,new[]{site},Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),new WorldGenerationSettings(),residents);
        var second=TownSpawner.GenerateChunk(33,input,new[]{site},Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),new WorldGenerationSettings(),residents);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(first),System.Text.Json.JsonSerializer.Serialize(second));
        Assert.True(first.Features.Count(f=>f.Kind=="town_street")>0);
        Assert.True(first.Features.Count(f=>f.Kind=="town_lot")>=first.Features.Count(f=>f.Kind=="town_building"));
    }

    [Fact]
    public void LargerPopulationGrowsAdditionalBlocksAndFrontage()
    {
        var site=new MapFeature{Kind="settlement",RouteId="growth:site",Name="Growth",Population=24,Points=new(){new(){X=.5,Y=.5}}};
        var input=new TownInput{ChunkX=0,ChunkY=0};
        TransportChunk Generate(int population)
        {
            site.Population=population;
            var residents=Enumerable.Range(0,population).Select(i=>new Settler{Id=$"p{i:D3}",Name=$"Resident{i} Smith",ClusterId=site.RouteId}).ToList();
            return TownSpawner.GenerateChunk(33,input,new[]{site},Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),new WorldGenerationSettings(),residents);
        }
        var small=Generate(24); var large=Generate(240);
        Assert.True(large.Features.Count(f=>f.Kind=="town_lot")>small.Features.Count(f=>f.Kind=="town_lot"));
        Assert.True(large.Features.Count(f=>f.Kind=="town_street")>small.Features.Count(f=>f.Kind=="town_street"));
        Assert.True(large.Features.Where(f=>f.Kind=="town_building").Sum(f=>f.HousingSlots)>=240,$"capacity={large.Features.Where(f=>f.Kind=="town_building").Sum(f=>f.HousingSlots)} lots={large.Features.Count(f=>f.Kind=="town_lot")} buildings={large.Features.Count(f=>f.Kind=="town_building")}");
    }

    [Theory]
    [InlineData(33, 0.0)]
    [InlineData(17, 0.35)]
    public void VariedParcelsContainHomesAndRetainRoadClearance(int seed,double angle)
    {
        var site=new MapFeature{Kind="settlement",RouteId="organic:site",Population=180,Points=new(){new(){X=.5,Y=.5}}};
        double ux=Math.Cos(angle),uy=Math.Sin(angle);
        var road=new MapFeature{Kind="main_road",RouteId="arterial",Surface="paved",Width=.02,
            Points=new(){new(){X=.5-ux,Y=.5-uy},new(){X=.5+ux,Y=.5+uy}}};
        var residents=Enumerable.Range(0,180).Select(i=>new Settler{Id=$"p{i:D3}",Name=$"Resident{i} Smith",ClusterId=site.RouteId}).ToList();
        var result=TownSpawner.GenerateChunk(seed,new TownInput{ChunkX=0,ChunkY=0},new[]{site},Array.Empty<MapFeature>(),
            new[]{road},Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),new WorldGenerationSettings(),residents);
        var lots=result.Features.Where(f=>f.Kind=="town_lot").ToDictionary(f=>f.RouteId);
        var homes=result.Features.Where(f=>f.Kind=="town_building").ToList();
        Assert.True(homes.Sum(f=>f.HousingSlots)>=residents.Count);
        Assert.Equal(residents.Count,result.Features.Count(f=>f.Kind=="town_person"));
        Assert.True(lots.Values.Select(l=>Math.Round(Math.Sqrt(Math.Pow(l.Points[1].X-l.Points[0].X,2)+Math.Pow(l.Points[1].Y-l.Points[0].Y,2))*5280,1)).Distinct().Count()>3);
        foreach(var home in homes)
        {
            var polygon=lots[home.Carrier].Points;
            foreach(var point in home.Points)
            {
                // A convex parcel must contain every corner of the rotated footprint.
                var crosses=Enumerable.Range(0,4).Select(i=>(polygon[(i+1)%4].X-polygon[i].X)*(point.Y-polygon[i].Y)-(polygon[(i+1)%4].Y-polygon[i].Y)*(point.X-polygon[i].X)).ToArray();
                Assert.True(crosses.All(c=>c>=-1e-10)||crosses.All(c=>c<=1e-10));
            }
        }
        Assert.Empty(TownSpawner.FindTownLotClearanceViolations(result.Features.Concat(new[]{road}).ToList()));
        Assert.All(result.Features.Where(f=>f.Kind is "town_street" or "town_alley"),f=>Assert.Equal("paved",f.Surface));
        Assert.DoesNotContain(result.Features,f=>f.RouteId?.Contains(":extra:")==true);
    }

    [Fact]
    public void ClearanceCheckerDetectsRoadWaterRailCivicAndLotConflicts()
    {
        var lot=new MapFeature{Kind="town_lot",RouteId="lot",Points=new(){new(){X=0,Y=0},new(){X=1,Y=0},new(){X=1,Y=1},new(){X=0,Y=1}}};
        MapFeature Line(string kind)=>new(){Kind=kind,Points=new(){new(){X=-1,Y=.5},new(){X=2,Y=.5}}};
        var civic=new MapFeature{Kind="town_civic_ground",Points=new(){new(){X=.2,Y=.2},new(){X=.8,Y=.2},new(){X=.8,Y=.8},new(){X=.2,Y=.8}}};
        var overlap=new MapFeature{Kind="town_lot",RouteId="overlap",Points=new(){new(){X=.5,Y=.5},new(){X=1.5,Y=.5},new(){X=1.5,Y=1.5},new(){X=.5,Y=1.5}}};
        foreach(var conflict in new[]{Line("main_road"),Line("local_road"),Line("town_street"),Line("rail"),Line("rivers"),civic,overlap,
            new MapFeature{Kind="lakes",Points=civic.Points}})
            Assert.NotEmpty(TownSpawner.FindTownLotClearanceViolations(new[]{lot,conflict}));
    }
}
