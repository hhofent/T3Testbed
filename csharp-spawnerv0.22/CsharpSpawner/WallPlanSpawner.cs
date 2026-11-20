using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SpawnGuide;

namespace CsharpSpawner;

/// <summary>Near-scale house wall plans copied from the retained SpawnGuide archetypes.</summary>
public static class WallPlanSpawner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };

    public static IReadOnlyList<WorldWallPlan> Generate(int worldSeed, int chunkX, int chunkNorth,
        IReadOnlyList<MapFeature> townFeatures)
    {
        var features = townFeatures ?? Array.Empty<MapFeature>();
        var streets = features.Where(f => f.Kind == "town_street" && f.Points?.Count >= 2).ToList();
        var result = new List<WorldWallPlan>();
        foreach (var house in features.Where(f => f.Kind == "town_building" && f.Points?.Count >= 3))
        {
            double cx = house.Points.Average(p => p.X), cy = house.Points.Average(p => p.Y);
            if ((int)Math.Floor(cx) != chunkX || (int)Math.Floor(cy) != chunkNorth) continue;
            string id = house.RouteId ?? house.Carrier ?? $"house:{cx:R}:{cy:R}";
            ulong hash = Hash(worldSeed, id);
            string archetype = string.IsNullOrEmpty(house.BuildingArchetype) ? HouseGenerator.Archetypes[(int)(hash % 3)] : house.BuildingArchetype;
            uint planSeed = house.BuildingPlanSeed != 0 ? house.BuildingPlanSeed : (uint)(hash >> 32) ^ (uint)hash;
            var plan = HouseGenerator.Generate(archetype, planSeed, "none", new HouseConfig());
            var front = !string.IsNullOrEmpty(house.BuildingArchetype) || house.FacingRadians != 0
                ? (X: Math.Cos(house.FacingRadians), Y: Math.Sin(house.FacingRadians))
                : FindRoadDirection(cx, cy, streets);
            var backX = -front.X; var backY = -front.Y;
            var axisX = (backY, -backX);
            var accepted = house.Points.Select(p => (X:p.X,Y:p.Y)).ToList();
            double footprintWidth=Math.Max(.001,accepted.Max(p=>p.X*axisX.Item1+p.Y*axisX.Item2)-accepted.Min(p=>p.X*axisX.Item1+p.Y*axisX.Item2))*5280;
            double footprintDepth=Math.Max(.001,accepted.Max(p=>p.X*backX+p.Y*backY)-accepted.Min(p=>p.X*backX+p.Y*backY))*5280;
            double centerX=accepted.Average(p=>p.X)*5280,centerY=accepted.Average(p=>p.Y)*5280;
            var walls = new List<WorldWall>(plan.Walls.Count);
            var footprint = accepted.Select(p=>new WorldPosition(p.X*5280,0,p.Y*5280)).ToList();
            foreach (var wall in plan.Walls)
                walls.Add(new WorldWall($"{id}:wall:{walls.Count}", id, Transform(wall.Start.X, wall.Start.Z),
                    Transform(wall.End.X, wall.End.Z), wall.HeightFeet, wall.ThicknessFeet, wall.Tag.ToString(),
                    wall.IsExterior, wall.Openings.Select(o => new WorldWallOpening(o.OffsetFeet, o.WidthFeet, o.Kind.ToString())).ToList()));
            result.Add(new WorldWallPlan(id, archetype, plan.Seed, plan.Storeys, plan.RoofNote, plan.SidingNote, footprint, walls));

            WorldPosition Transform(double x, double z) => new(
                centerX + axisX.Item1 * ((x - plan.WidthFeet / 2) * footprintWidth / plan.WidthFeet) + backX * ((z - plan.DepthFeet / 2) * footprintDepth / plan.DepthFeet),
                0,
                centerY + axisX.Item2 * ((x - plan.WidthFeet / 2) * footprintWidth / plan.WidthFeet) + backY * ((z - plan.DepthFeet / 2) * footprintDepth / plan.DepthFeet));
        }
        return result;
    }

    internal static (double X,double Y) Entrance(MapFeature house)
    {
        var plan=HouseGenerator.Generate(house.BuildingArchetype,house.BuildingPlanSeed,"none",new HouseConfig());
        double frontX=Math.Cos(house.FacingRadians),frontY=Math.Sin(house.FacingRadians);
        double backX=-frontX,backY=-frontY,axisX=backY,axisY=-backX;
        var points=house.Points;
        double width=(points.Max(p=>p.X*axisX+p.Y*axisY)-points.Min(p=>p.X*axisX+p.Y*axisY));
        double depth=(points.Max(p=>p.X*backX+p.Y*backY)-points.Min(p=>p.X*backX+p.Y*backY));
        var wall=plan.Walls.FirstOrDefault(w=>w.IsExterior&&w.Openings.Any(o=>o.Kind.ToString()=="Door"));
        if(wall==null)return (points.Average(p=>p.X)+frontX*depth*.5,points.Average(p=>p.Y)+frontY*depth*.5);
        var door=wall.Openings.First(o=>o.Kind.ToString()=="Door");
        double dx=wall.End.X-wall.Start.X,dz=wall.End.Z-wall.Start.Z;
        double t=(door.OffsetFeet+door.WidthFeet*.5)/Math.Max(1e-9,Math.Sqrt(dx*dx+dz*dz));
        double x=(wall.Start.X+dx*t-plan.WidthFeet*.5)*width/plan.WidthFeet;
        double z=(wall.Start.Z+dz*t-plan.DepthFeet*.5)*depth/plan.DepthFeet;
        return (points.Average(p=>p.X)+axisX*x+backX*z,points.Average(p=>p.Y)+axisY*x+backY*z);
    }

    public static async Task<int> RunWorkerAsync()
    {
        try
        {
            var request = JsonSerializer.Deserialize<WallRequest>(await Console.In.ReadToEndAsync().ConfigureAwait(false), JsonOptions)
                ?? throw new InvalidOperationException("Missing wall plan request.");
            using var stdout = new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            foreach (var input in request.Chunks ?? new())
            {
                var chunk = new WallChunk { ChunkX = input.ChunkX, ChunkY = input.ChunkY,
                    Plans = Generate(request.Seed, input.ChunkX, input.ChunkY, request.TownFeatures).ToList() };
                await stdout.WriteLineAsync(JsonSerializer.Serialize(chunk, JsonOptions)).ConfigureAwait(false);
            }
            return 0;
        }
        catch (Exception ex) { await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false); return 1; }
    }

    private static (double X, double Y) FindRoadDirection(double x, double y, IReadOnlyList<MapFeature> streets)
    {
        double best = double.PositiveInfinity; var result = (X: 0.0, Y: -1.0);
        foreach (var road in streets)
        for (int i = 0; i + 1 < road.Points.Count; i += 2)
        {
            var a = road.Points[i]; var b = road.Points[i + 1];
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double t = Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / Math.Max(1e-20, dx * dx + dy * dy), 0, 1);
            double px = a.X + t * dx, py = a.Y + t * dy, distance = Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
            if (distance >= best) continue;
            best = distance; double length = Math.Sqrt(dx * dx + dy * dy);
            if (length > 1e-12) result = distance > 1e-12
                ? ((px - x) / distance, (py - y) / distance)
                : (-dy / length, dx / length);
        }
        return result;
    }

    private static ulong Hash(int seed, string value)
    {
        ulong h = 14695981039346656037UL ^ (uint)seed;
        foreach (char c in value) { h ^= c; h *= 1099511628211UL; }
        return h;
    }
}
