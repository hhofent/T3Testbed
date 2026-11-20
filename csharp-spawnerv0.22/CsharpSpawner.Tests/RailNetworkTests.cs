using CsharpSpawner;
using System.Text.Json;

namespace CsharpSpawner.Tests;

public sealed class RailNetworkTests
{
    [Theory]
    [InlineData(33)]
    [InlineData(31)]
    [InlineData(19)]
    public void AllTrackBelongsToMainlineComponentAndHasNoLooseInteriorEnds(int seed)
    {
        var towns=new[]{Town("west",-1.5,.5),Town("east",1.5,4),Town("near-west",-1.45,.55)};
        var inputs=(from x in Enumerable.Range(-4,9) from y in Enumerable.Range(-1,7) select new TransportInput{ChunkX=x,ChunkY=y}).ToList();
        var chunks=TransportSpawner.GenerateRailChunks(seed,inputs,Array.Empty<MapFeature>(),towns,Array.Empty<MapFeature>());
        var rails=chunks.SelectMany(c=>c.Features).Where(f=>f.Kind=="rail").ToList();
        var branches=rails.Where(f=>f.Carrier=="branch").Select(f=>f.RouteId).Distinct().ToList();
        Assert.NotEmpty(branches);Assert.True(branches.Count<towns.Length);
        Assert.Single(rails.Where(f=>f.Carrier=="mainline").Select(f=>f.RouteId).Distinct());
        var graph=new Dictionary<(long X,long Y),HashSet<(long X,long Y)>>();
        foreach(var rail in rails)for(int i=0;i+1<rail.Points.Count;i+=2)
        {
            var a=Key(rail.Points[i]);var b=Key(rail.Points[i+1]);
            if(!graph.ContainsKey(a))graph[a]=new();if(!graph.ContainsKey(b))graph[b]=new();
            graph[a].Add(b);graph[b].Add(a);
        }
        var visited=new HashSet<(long X,long Y)>();var pending=new Queue<(long X,long Y)>();
        pending.Enqueue(Key(rails.First(f=>f.Carrier=="mainline").Points[0]));
        while(pending.TryDequeue(out var node))if(visited.Add(node))foreach(var neighbor in graph[node])pending.Enqueue(neighbor);
        Assert.Equal(graph.Count,visited.Count);
        foreach(var node in graph.Where(n=>n.Value.Count==1).Select(n=>n.Key))
        {
            bool boundary=node.X==Key(new MapPoint{X=-4}).X||node.X==Key(new MapPoint{X=5}).X;
            bool terminal=chunks.SelectMany(c=>c.Features).Any(f=>f.Kind=="rail_terminal"&&Key(f.Points[0])==node&&branches.Contains(f.Carrier));
            Assert.True(boundary||terminal,$"Loose rail end {node}");
        }
        Assert.DoesNotContain(rails,f=>f.RouteId.Contains("continuation")||f.RouteId.Contains("diagonal"));
    }

    [Fact]
    public void MainlineContinuesWithoutTownsAndChunkRequestsAgree()
    {
        var inputs=Enumerable.Range(-5,11).Select(x=>new TransportInput{ChunkX=x,ChunkY=2}).ToList();
        var batch=TransportSpawner.GenerateRailChunks(33,inputs,Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),Array.Empty<MapFeature>());
        foreach(var input in inputs)
        {
            var together=batch.Single(c=>c.ChunkX==input.ChunkX);Assert.Single(together.Features,f=>f.Kind=="rail");
            var alone=TransportSpawner.GenerateRailChunk(33,input,Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),Array.Empty<MapFeature>());
            Assert.Equal(JsonSerializer.Serialize(together),JsonSerializer.Serialize(alone));
        }
        for(int i=0;i+1<batch.Count;i++)Assert.Equal(Key(batch[i].Features.Single().Points[^1]),Key(batch[i+1].Features.Single().Points[0]));
    }

    static MapFeature Town(string id,double x,double y)=>new(){Kind="settlement",RouteId=id,Name=id,IsMajorTown=true,Points=new(){new(){X=x,Y=y}}};
    static (long X,long Y) Key(MapPoint p)=>((long)Math.Round(p.X*1e8),(long)Math.Round(p.Y*1e8));
}
