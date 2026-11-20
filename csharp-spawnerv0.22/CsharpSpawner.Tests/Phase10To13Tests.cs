using System.Text.Json;
using CsharpSpawner;
using SpawnGuide;

namespace CsharpSpawner.Tests;

[CollectionDefinition("World generation", DisableParallelization = true)]
public sealed class WorldGenerationCollection { }

[Collection("World generation")]
public sealed class Phase10To13Tests
{
    private static readonly Lazy<(WorldQueryResult First, WorldQueryResult Overlap)> Seed31 = new(() =>
    {
        var settings = new WorldGenerationSettings();
        var first = WorldQuery.Load(31, settings, -4.5 * 5280, -3 * 5280, 6 * 5280, WorldDetail.Mid);
        var overlap = WorldQuery.Load(31, settings, -.5 * 5280, -3 * 5280, 6 * 5280, WorldDetail.Mid);
        return (first, overlap);
    });

    [Fact]
    public void SpawnGuideArchetypesRetainOpeningsAndExactStoryHeightFor1993()
    {
        foreach (var archetype in HouseGenerator.Archetypes)
        {
            var plan = HouseGenerator.Generate(archetype, 1993, "none", new HouseConfig());
            Assert.NotEmpty(plan.Footprint);
            Assert.NotEmpty(plan.Walls);
            Assert.Contains(plan.Walls, wall => wall.Openings.Count > 0);
            Assert.All(plan.Walls, wall => Assert.Equal(plan.Storeys * (97.125 / 12), wall.HeightFeet, 4));
        }
    }

    [Fact]
    public void Seed31QueryIsByteStableAndReturnsDetailPayloads()
    {
        var data = Seed31.Value;
        var duplicate = WorldQuery.Load(31, new WorldGenerationSettings(), -4.5 * 5280, -3 * 5280, 6 * 5280, WorldDetail.Mid);
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(data.First), JsonSerializer.SerializeToUtf8Bytes(duplicate));
        Assert.NotNull(data.First.TerrainGrid);
        Assert.InRange(data.First.TerrainGrid.StepFt, 10, 30);
        var far = WorldQuery.Load(31, new WorldGenerationSettings(), -4.5 * 5280, -3 * 5280, 6 * 5280, WorldDetail.Far);
        Assert.DoesNotContain(far.Features, f => f.Kind is "town_lot" or "town_building" or "rural_lot");
        Assert.Null(far.TerrainGrid);
        var houseCenter=Center(data.First.Features.First(f=>f.Kind=="town_building"));
        var near = WorldQuery.Load(31, new WorldGenerationSettings(), houseCenter.X-2640, houseCenter.Z-2640, 5280, WorldDetail.Near);
        Assert.NotEmpty(near.WallPlans ?? Array.Empty<WorldWallPlan>());
        Assert.DoesNotContain(near.Features, f => f.Kind == "town_building");
        var fixtureHouse = new MapFeature { Kind = "town_building", RouteId = "lot-seed1993", Carrier = "lot-seed1993",
            Points = new() { new() { X = 0, Y = 0 }, new() { X = .02, Y = 0 }, new() { X = .02, Y = .02 }, new() { X = 0, Y = .02 } } };
        var fixtureWalls = WallPlanSpawner.Generate(31, 0, 0, new[] { fixtureHouse });
        Assert.Single(fixtureWalls);
        Assert.NotEmpty(fixtureWalls[0].Walls);
    }

    [Fact]
    public void OverlappingSeed31WindowsAgreeOnSharedFeatureCentersAndTownIdentity()
    {
        var (first, overlap) = Seed31.Value;
        var townGeometryA = first.Features.First(f=>f.Kind=="town"&&f.Id=="main_road:ew:0:settlement:0").Geometry;
        var townGeometryB = overlap.Features.First(f=>f.Kind=="town"&&f.Id=="main_road:ew:0:settlement:0").Geometry;
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(townGeometryA).SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(townGeometryB)),$"A={string.Join(";",townGeometryA.Select(p=>$"{p.X:R},{p.Z:R}"))} B={string.Join(";",townGeometryB.Select(p=>$"{p.X:R},{p.Z:R}"))}");
        var rightIds = overlap.Features.Select(f => (f.Kind, f.Id)).ToHashSet();
        foreach (var feature in first.Features)
        {
            if (feature.Geometry.Count == 0) continue;
            var center = Center(feature); double x = center.X, z = center.Z;
            if (x < -.5 * 5280 || x > 1.5 * 5280 || z < -3 * 5280 || z > 3 * 5280) continue;
            Assert.True(rightIds.Contains((feature.Kind, feature.Id)), $"{feature.Kind} center {x:F1},{z:F1}; {feature.Id}; matching IDs: {string.Join(";", overlap.Features.Where(f=>f.Id.StartsWith(feature.Id.Split(":exterior-lot:")[0],StringComparison.Ordinal)).Select(f=>$"{f.Kind}/{f.Id}"))}");
        }
        var stableTownsA = first.Features.Where(f => f.Kind == "town").Select(f => (f.Id, f.Name)).ToHashSet();
        var stableTownsB = overlap.Features.Where(f => f.Kind == "town").Select(f => (f.Id, f.Name)).ToHashSet();
        Assert.True(stableTownsA.Intersect(stableTownsB).Any(), $"A: {string.Join(";", first.Features.Where(f=>f.Kind=="town").Select(f=>$"{f.Id}/{f.Name}/{Center(f)}"))} B: {string.Join(";", overlap.Features.Where(f=>f.Kind=="town").Select(f=>$"{f.Id}/{f.Name}/{Center(f)}"))}");
        Assert.Contains(("main_road:ew:0:settlement:0", "Misty Ridge"), stableTownsA);
        Assert.Contains(("main_road:ew:0:settlement:0", "Misty Ridge"), stableTownsB);
    }

    [Fact]
    public void Seed31RailCrossingsAreSafeAndMostlySquare()
    {
        var features = Seed31.Value.First.Features;
        var rails = features.Where(f => f.Kind == "rail").ToList();
        var roads = features.Where(f => f.Kind is "main_road" or "local_road" or "town_street").ToList();
        var angles = new List<double>();
        foreach (var rail in rails) foreach (var road in roads)
        for (int i=0;i+1<rail.Geometry.Count;i+=2) for (int j=0;j+1<road.Geometry.Count;j+=2)
        {
            var a=rail.Geometry[i];var b=rail.Geometry[i+1];var c=road.Geometry[j];var d=road.Geometry[j+1];
            if(!Crosses(a,b,c,d))continue;
            double rx=b.X-a.X,rz=b.Z-a.Z,dx=d.X-c.X,dz=d.Z-c.Z;
            double angle=Math.Acos(Math.Clamp(Math.Abs(rx*dx+rz*dz)/(Math.Sqrt(rx*rx+rz*rz)*Math.Sqrt(dx*dx+dz*dz)),0,1))*180/Math.PI;
            Assert.True(angle>=45,$"Rail-road crossing angle {angle:F3}: {rail.Id} with {road.Id} at {c.X:F1},{c.Z:F1} to {d.X:F1},{d.Z:F1}");
            angles.Add(angle);
        }
        Assert.NotEmpty(angles);
        Assert.All(angles,angle=>Assert.True(angle>=45,$"Rail-road crossing angle {angle:F1} degrees."));
        Assert.True(angles.Count(angle=>angle>=75&&angle<=105)>=Math.Ceiling(angles.Count*.8),$"{angles.Count(angle=>angle>=75&&angle<=105)}/{angles.Count} crossings are within 15 degrees of square.");
    }

    [Fact]
    public void Seed31RoadEndsConnectOrUseAnAllowedTownDeadEnd()
    {
        var features=Seed31.Value.First.Features;
        var roads=features.Where(f=>f.Kind is "main_road" or "local_road" or "town_street").ToList();
        var bulbs=features.Where(f=>f.Kind=="town_court_bulb").ToList();
        var sectionIds=roads.Where(f=>f.Kind=="local_road"&&f.Id.StartsWith("section:",StringComparison.Ordinal))
            .Select(f=>f.Id).ToHashSet(StringComparer.Ordinal);
        foreach(var road in roads)for(int i=0;i+1<road.Geometry.Count;i+=2)
        foreach(var endpoint in new[]{road.Geometry[i],road.Geometry[i+1]})
        {
            if(endpoint.X<=-4.5*5280+2||endpoint.X>=1.5*5280-2||endpoint.Z<=-3*5280+2||endpoint.Z>=3*5280-2)continue;
            bool connected=roads.Any(other=>
            {
                for(int j=0;j+1<other.Geometry.Count;j+=2)
                {
                    if(other.Id==road.Id&&j==i)continue;
                    if(DistanceToSegment(endpoint,other.Geometry[j],other.Geometry[j+1])<12)return true;
                }
                return false;
            });
            bool allowed=road.Kind=="town_street"&&road.Id.Contains(":extra:",StringComparison.Ordinal)&&
                (road.Name is "COURT" or "PLAT EDGE" or "OUTLYING ROAD")||
                IsPlannedSectionCutoutEnd(road,endpoint,sectionIds)||
                bulbs.Any(b=>b.Geometry.Count>0&&Distance(endpoint,b.Geometry[0])<60);
            var nearest=roads.Where(other=>other.Id!=road.Id).Select(other=>(Road:other,Distance:DistanceToRoad(endpoint,other))).OrderBy(item=>item.Distance).FirstOrDefault();
            Assert.True(connected||allowed,$"Unconnected endpoint {endpoint.X:F1},{endpoint.Z:F1} on {road.Kind}:{road.Id} ({road.Name}); nearest {nearest.Road?.Kind}:{nearest.Road?.Id} at {nearest.Distance:F1} ft.");
        }
    }

    [Fact]
    public void Seed31BuildingsDoNotOverlapWaterOrOtherBuildings()
    {
        var features = Seed31.Value.First.Features;
        var buildings = features.Where(f => f.Kind is "rural_building" or "town_building" or "town_business" or "town_garage").ToList();
        var waters = features.Where(f => f.Kind is "rivers" or "lakes").ToList();
        for (int i = 0; i < buildings.Count; i++)
        {
            Assert.DoesNotContain(waters, water => Overlap(buildings[i], water));
            for (int j = i + 1; j < buildings.Count; j++) Assert.False(Overlap(buildings[i], buildings[j]), $"{buildings[i].Kind}:{buildings[i].Id} intersects {buildings[j].Kind}:{buildings[j].Id}");
        }
    }

    [Fact]
    public void SectionRoadCrossingFixtureKeepsRiverAndCreekRules()
    {
        MapFeature River(string id, int order, MapPoint a, MapPoint b, double width = .01) => new()
        { Kind = "rivers", RouteId = id, StreamOrder = order, Width = width, Points = new() { a, b } };
        TownInput Input(int x, int y) => new() { ChunkX = x, ChunkY = y };
        var creek = River("creek", 1, new() { X = .5, Y = -.1 }, new() { X = .5, Y = .1 });
        var creekRoad = RoadSpawner.GenerateChunk(31, Input(0, 0), new[] { creek });
        Assert.Contains(creekRoad.Features, f => f.RouteId == "section:ew:0:0");
        Assert.Contains(creekRoad.Features, f => f.Kind == "bridges" && f.RouteId == "section:ew:0:0");

        var major = River("major", 2, new() { X = .5, Y = -.1 }, new() { X = .5, Y = .1 });
        var gravelMajor = RoadSpawner.GenerateChunk(31, Input(0, 0), new[] { major });
        Assert.DoesNotContain(gravelMajor.Features, f => f.Kind == "local_road" && f.RouteId == "section:ew:0:0");

        var countyCrossing = River("county-river", 2, new() { X = .8, Y = .5 }, new() { X = 1.2, Y = .5 });
        var county = RoadSpawner.GenerateChunk(31, Input(1, 0), new[] { countyCrossing });
        Assert.Contains(county.Features, f => f.Kind == "local_road" && f.RouteId == "section:ns:1:0" && f.Surface == "paved");
        Assert.Contains(county.Features, f => f.Kind == "bridges" && f.RouteId == "section:ns:1:0");

        var diagonalMajor = River("bank-link-river", 2, new() { X = .2, Y = -.2 }, new() { X = 1.2, Y = 1.2 });
        var bankApproach = RoadSpawner.GenerateChunk(31, Input(0, 0), new[] { diagonalMajor });
        Assert.Contains(bankApproach.Features, f => f.Kind == "local_road" && f.RouteId?.StartsWith("bank-link:") == true);
    }

    private static bool Overlap(WorldFeature a, WorldFeature b)
    {
        if (a.Geometry.Count == 0 || b.Geometry.Count == 0) return false;
        if (a.Closed && b.Closed) return PolygonsOverlap(a.Geometry, b.Geometry);
        if (a.Closed) return LinesHitPolygon(b.Geometry, a.Geometry);
        if (b.Closed) return LinesHitPolygon(a.Geometry, b.Geometry);
        return false;
    }
    private static (double X,double Z) Center(WorldFeature f)
    {
        if(f.Closed)return(f.Geometry.Average(p=>p.X),f.Geometry.Average(p=>p.Z));
        double x=0,z=0,total=0;
        for(int i=0;i+1<f.Geometry.Count;i+=2){var a=f.Geometry[i];var b=f.Geometry[i+1];double length=Math.Sqrt(Math.Pow(b.X-a.X,2)+Math.Pow(b.Z-a.Z,2));x+=(a.X+b.X)*.5*length;z+=(a.Z+b.Z)*.5*length;total+=length;}
        return total>0?(x/total,z/total):(f.Geometry.Average(p=>p.X),f.Geometry.Average(p=>p.Z));
    }
    private static bool PolygonsOverlap(IReadOnlyList<WorldPosition> a,IReadOnlyList<WorldPosition> b) =>
        a.Any(p=>Contains(b,p))||b.Any(p=>Contains(a,p))||EdgesIntersect(a,b);
    private static bool LinesHitPolygon(IReadOnlyList<WorldPosition> lines,IReadOnlyList<WorldPosition> polygon)
    {
        for(int i=0;i+1<lines.Count;i+=2){if(Contains(polygon,lines[i])||Contains(polygon,lines[i+1]))return true;
            for(int j=0;j<polygon.Count;j++)if(SegmentsCross(lines[i],lines[i+1],polygon[j],polygon[(j+1)%polygon.Count]))return true;}
        return false;
    }
    private static bool EdgesIntersect(IReadOnlyList<WorldPosition> a,IReadOnlyList<WorldPosition> b)
    {for(int i=0;i<a.Count;i++)for(int j=0;j<b.Count;j++)if(SegmentsCross(a[i],a[(i+1)%a.Count],b[j],b[(j+1)%b.Count]))return true;return false;}
    private static bool Contains(IReadOnlyList<WorldPosition> p,WorldPosition v)
    {bool inside=false;for(int i=0,j=p.Count-1;i<p.Count;j=i++)if((p[i].Z>v.Z)!=(p[j].Z>v.Z)&&v.X<(p[j].X-p[i].X)*(v.Z-p[i].Z)/(p[j].Z-p[i].Z)+p[i].X)inside=!inside;return inside;}
    private static bool SegmentsCross(WorldPosition a,WorldPosition b,WorldPosition c,WorldPosition d)
    {
        double Cross(WorldPosition p,WorldPosition q,WorldPosition r)=>(q.X-p.X)*(r.Z-p.Z)-(q.Z-p.Z)*(r.X-p.X);
        bool On(WorldPosition p,WorldPosition q,WorldPosition r)=>r.X>=Math.Min(p.X,q.X)-1e-8&&r.X<=Math.Max(p.X,q.X)+1e-8&&r.Z>=Math.Min(p.Z,q.Z)-1e-8&&r.Z<=Math.Max(p.Z,q.Z)+1e-8;
        double x=Cross(a,b,c),y=Cross(a,b,d),u=Cross(c,d,a),v=Cross(c,d,b);
        if(((x>0&&y<0)||(x<0&&y>0))&&((u>0&&v<0)||(u<0&&v>0)))return true;
        return Math.Abs(x)<1e-8&&On(a,b,c)||Math.Abs(y)<1e-8&&On(a,b,d)||Math.Abs(u)<1e-8&&On(c,d,a)||Math.Abs(v)<1e-8&&On(c,d,b);
    }
    private static bool Crosses(WorldPosition a,WorldPosition b,WorldPosition c,WorldPosition d)=>SegmentsCross(a,b,c,d);
    private static double Distance(WorldPosition a,WorldPosition b)=>Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Z-b.Z,2));
    private static double DistanceToRoad(WorldPosition p,WorldFeature road)
    {
        double best=double.PositiveInfinity;
        for(int i=0;i+1<road.Geometry.Count;i+=2){var a=road.Geometry[i];var b=road.Geometry[i+1];double dx=b.X-a.X,dz=b.Z-a.Z;
            double t=Math.Clamp(((p.X-a.X)*dx+(p.Z-a.Z)*dz)/Math.Max(1e-20,dx*dx+dz*dz),0,1);
            best=Math.Min(best,Distance(p,new WorldPosition(a.X+t*dx,0,a.Z+t*dz)));}
        return best;
    }
    private static double DistanceToSegment(WorldPosition p,WorldPosition a,WorldPosition b)
    {
        double dx=b.X-a.X,dz=b.Z-a.Z;
        double t=Math.Clamp(((p.X-a.X)*dx+(p.Z-a.Z)*dz)/Math.Max(1e-20,dx*dx+dz*dz),0,1);
        return Distance(p,new WorldPosition(a.X+t*dx,0,a.Z+t*dz));
    }
    private static bool IsPlannedSectionCutoutEnd(WorldFeature road,WorldPosition endpoint,HashSet<string> sectionIds)
    {
        if(road.Kind!="local_road")return false;
        var parts=road.Id.Split(':');
        if(parts.Length!=4||parts[0]!="section"||!int.TryParse(parts[3],out int segment))return false;
        bool horizontal=parts[1]=="ew";
        if(!horizontal&&parts[1]!="ns")return false;
        double along=(horizontal?endpoint.X:endpoint.Z)/5280.0;
        int adjacent=Math.Abs(along-segment)<Math.Abs(along-(segment+1))?segment-1:segment+1;
        return !sectionIds.Contains($"section:{parts[1]}:{parts[2]}:{adjacent}");
    }
}
