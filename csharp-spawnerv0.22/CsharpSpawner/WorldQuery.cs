using System;
using System.Collections.Generic;
using System.Linq;

namespace CsharpSpawner;

/// <summary>Loads a deterministic, clipped world window in feet.</summary>
public static class WorldQuery
{
    private const double FeetPerMile = 5280.0;
    private const int ContextMiles = 9;

    public static WorldQueryResult Load(int seed, WorldGenerationSettings settings,
        double originXFeet, double originZFeet, double sizeFeet, WorldDetail detail)
    {
        if (sizeFeet <= 0 || !double.IsFinite(sizeFeet) || !double.IsFinite(originXFeet) || !double.IsFinite(originZFeet))
            throw new ArgumentOutOfRangeException(nameof(sizeFeet), "World query origin and size must be finite; size must be positive.");
        settings ??= new WorldGenerationSettings();
        WorldGenerator.Configure(settings);

        // The fixed context exceeds the existing 9-mile settlement separation,
        // so a camera edge cannot suppress a neighboring site's seeded choice.
        double left = originXFeet / FeetPerMile, south = originZFeet / FeetPerMile;
        double right = left + sizeFeet / FeetPerMile, north = south + sizeFeet / FeetPerMile;
        const double detailMarginMiles=3;
        int minX = (int)Math.Floor(left) - ContextMiles, maxX = (int)Math.Ceiling(right) + ContextMiles;
        int minZ = (int)Math.Floor(south) - ContextMiles, maxZ = (int)Math.Ceiling(north) + ContextMiles;
        var scenes = new List<ViewerScene>();
        var waters = new List<WaterChunk>();
        var populationInputs = new List<PopulationInput>();
        var transportInputs = new List<TransportInput>();
        var townInputs = new List<TownInput>();
        for (int z=minZ; z<maxZ; z++) for(int x=minX; x<maxX; x++)
        {
            var scene=WorldGenerator.GenerateChunk(seed,x,z);
            scene.SeaLevelFt=settings.SeaLevelFeet;
            scenes.Add(scene);
            waters.Add(WaterSpawner.GenerateChunk(new WaterChunkInput { ChunkX=x, ChunkY=z, SeaLevelFt=settings.SeaLevelFeet,
                WaterGrid=scene.WaterGrid, RiverBeds=scene.RiverBeds }));
            populationInputs.Add(new PopulationInput { ChunkX=x, ChunkY=z, TerrainGrid=scene.TerrainGrid });
            transportInputs.Add(new TransportInput { ChunkX=x, ChunkY=z, TerrainGrid=scene.TerrainGrid });
            townInputs.Add(new TownInput { ChunkX=x, ChunkY=z, TerrainGrid=scene.TerrainGrid });
        }
        var waterFeatures=Canonical(waters.SelectMany(c=>c.Features));
        var population=PopulationSpawner.Generate(new PopulationRequest { Seed=seed, Settings=settings, Chunks=populationInputs,
            WaterFeatures=waterFeatures, ExistingRoads=new List<MapFeature>() });
        var populationFeatures=Canonical(population.SelectMany(c=>c.Features));
        // Terrain, water, and population keep the larger reproducibility context.
        // Later geometry only needs the visible area plus a local dependency halo.
        var visibleInputs=transportInputs.Where(input=>input.ChunkX+1>left-detailMarginMiles&&input.ChunkX<right+detailMarginMiles&&
            input.ChunkY+1>south-detailMarginMiles&&input.ChunkY<north+detailMarginMiles).ToList();
        var visibleTownInputs=townInputs.Where(input=>input.ChunkX+1>left-detailMarginMiles&&input.ChunkX<right+detailMarginMiles&&
            input.ChunkY+1>south-detailMarginMiles&&input.ChunkY<north+detailMarginMiles).ToList();
        var railTowns=populationFeatures.Where(f=>f.Kind=="settlement").ToList();
        var mainRoads=populationFeatures.Where(f=>f.Kind=="main_road").ToList();
        var road=new List<TransportChunk>();
        var savedMainRoads=populationFeatures.Where(f=>f.Kind=="main_road").ToList();
        // Rail routing reads roads three miles beyond each emitted chunk.
        // Supply that dependency halo so panning cannot change tracks and town parcels.
        var roadInputs=townInputs.Where(input=>input.ChunkX+1>left-detailMarginMiles-3&&input.ChunkX<right+detailMarginMiles+3&&
            input.ChunkY+1>south-detailMarginMiles-3&&input.ChunkY<north+detailMarginMiles+3).ToList();
        foreach(var input in roadInputs) road.Add(RoadSpawner.GenerateChunk(seed,input,waterFeatures,savedMainRoads));
        // Rail routing accounts for planned section-road centerlines too; road
        // features remain a later visible stage in the final feature payload.
        var roadFeatures=Canonical(populationFeatures.Concat(road.SelectMany(c=>c.Features)));
        var rail=TransportSpawner.GenerateRailChunks(seed,visibleInputs,waterFeatures,railTowns,roadFeatures).ToList();
        var railFeatures=Canonical(rail.SelectMany(c=>c.Features));
        var rural=new List<TransportChunk>();
        foreach(var input in visibleTownInputs) rural.Add(RuralSpawner.GenerateChunk(seed,input,roadFeatures,waterFeatures,railFeatures,railTowns,settings));
        var ruralFeatures=Canonical(rural.SelectMany(c=>c.Features));
        var towns=new List<TransportChunk>();
        var settlers=population.SelectMany(chunk=>chunk.Settlers??new List<Settler>()).ToList();
        foreach(var input in visibleTownInputs) towns.Add(TownSpawner.GenerateChunk(seed,input,
            populationFeatures.Where(f=>f.Kind=="settlement").ToList(),railFeatures,roadFeatures,waterFeatures,ruralFeatures,settings,settlers));

        var all=new List<(MapFeature Feature,int X,int Z,int Index)>();
        AddScenes(all,scenes.Select(s=>(s.ChunkX,s.ChunkY,s.Features)));
        AddScenes(all,waters.Select(s=>(s.ChunkX,s.ChunkY,s.Features)));
        AddScenes(all,population.Select(s=>(s.ChunkX,s.ChunkY,s.Features)));
        AddScenes(all,rail.Select(s=>(s.ChunkX,s.ChunkY,s.Features)));
        AddScenes(all,road.Select(s=>(s.ChunkX,s.ChunkY,s.Features)));
        AddScenes(all,rural.Select(s=>(s.ChunkX,s.ChunkY,s.Features)));
        AddScenes(all,towns.Select(s=>(s.ChunkX,s.ChunkY,s.Features)));
        var removedRuralIds=new HashSet<string>(towns.SelectMany(chunk=>(chunk.RemovedRuralLotIds??new List<string>())
            .Concat(chunk.RemovedRuralFeatureIds??new List<string>())),StringComparer.Ordinal);
        if(removedRuralIds.Count>0)all.RemoveAll(item=>removedRuralIds.Contains(item.Feature.RouteId??"")||removedRuralIds.Contains(item.Feature.Carrier??""));
        var survivingRural = rural.SelectMany(c => c.Features ?? new List<MapFeature>())
            .Where(f => !removedRuralIds.Contains(f.RouteId ?? "") && !removedRuralIds.Contains(f.Carrier ?? "")).ToList();
        var townFeaturesForTrees = towns.SelectMany(c => c.Features ?? new List<MapFeature>()).ToList();
        var treeChunks = visibleTownInputs.Select(input => TreeSpawner.GenerateChunk(seed,
            new TreeInput { ChunkX = input.ChunkX, ChunkY = input.ChunkY }, survivingRural, townFeaturesForTrees,
            roadFeatures, railFeatures, waterFeatures, settings,
            rural.SelectMany(c=>c.TreeReservations??new List<MapFeature>()).Where(f=>!removedRuralIds.Contains(f.Carrier??"")).ToList())).ToList();
        AddScenes(all, treeChunks.Select(s => (s.ChunkX, s.ChunkY, s.Features)));
        var features=all.Select(item=>LegacyMapFeatureConverter.ToWorldFeature(item.Feature) with
            { Id=item.Feature.RouteId ?? $"{item.Feature.Kind}:{item.X}:{item.Z}:{item.Index}" })
            .Select(f=>Clip(f,left*FeetPerMile,south*FeetPerMile,right*FeetPerMile,north*FeetPerMile))
            .Where(f=>f!=null).GroupBy(f=>(f.Kind,f.Id))
            .Select(group=>group.First() with { Geometry=MergeGeometry(group) }).ToList();
        var people=population.SelectMany(c=>c.Settlers).Select(p=>LegacyMapFeatureConverter.ToPerson(p))
            .Where(p=>p.Position.X>=left*FeetPerMile&&p.Position.X<=right*FeetPerMile&&p.Position.Z>=south*FeetPerMile&&p.Position.Z<=north*FeetPerMile).ToList();
        features = detail switch
        {
            WorldDetail.Far => features.Where(f => f.Kind is "settlement" or "town" or "main_road" or "rail" ||
                f.Kind == "local_road" && f.Surface == "paved" || f.Kind == "rivers" && f.StreamOrder >= 3).ToList(),
            WorldDetail.Near => features.Where(f => f.Kind != "town_building").ToList(),
            _ => features
        };
        if (detail == WorldDetail.Far)
            features = features.Select(f => f.Kind == "town"
                ? f with { Geometry = new[] { new WorldPosition(f.Geometry.Average(p => p.X), f.Geometry.Average(p => p.Y), f.Geometry.Average(p => p.Z)) }, Closed = false }
                : f).ToList();
        var visiblePeople = detail == WorldDetail.Far ? new List<WorldPerson>() : people;
        ElevationGridData detailTerrain = null;
        IReadOnlyList<WorldWallPlan> walls = null;
        if (detail is WorldDetail.Mid or WorldDetail.Near)
            detailTerrain = WorldGenerator.GenerateDetailTerrainGrid(seed, left * FeetPerMile, south * FeetPerMile,
                right * FeetPerMile, north * FeetPerMile, 30, settings);
        if (detail == WorldDetail.Near)
        {
            var townFeatures = towns.SelectMany(t => t.Features ?? new List<MapFeature>()).ToList();
            var plans = new List<WorldWallPlan>();
            for (int z = (int)Math.Floor(south); z < (int)Math.Ceiling(north); z++)
            for (int x = (int)Math.Floor(left); x < (int)Math.Ceiling(right); x++)
                plans.AddRange(WallPlanSpawner.Generate(seed, x, z, townFeatures));
            walls = plans;
        }
        return new WorldQueryResult(features, visiblePeople, detailTerrain, walls);
    }

    private static void AddScenes(List<(MapFeature Feature,int X,int Z,int Index)> target,
        IEnumerable<(int X,int Z,List<MapFeature> Features)> chunks)
    { foreach(var c in chunks) for(int i=0;i<(c.Features?.Count??0);i++) target.Add((c.Features[i],c.X,c.Z,i)); }

    private static List<MapFeature> Canonical(IEnumerable<MapFeature> features) => (features ?? Enumerable.Empty<MapFeature>())
        .OrderBy(f => f.Kind, StringComparer.Ordinal).ThenBy(f => f.RouteId, StringComparer.Ordinal)
        .ThenBy(f => f.Points?.Count > 0 ? f.Points[0].X : 0).ThenBy(f => f.Points?.Count > 0 ? f.Points[0].Y : 0).ToList();

    private static IReadOnlyList<WorldPosition> MergeGeometry(IEnumerable<WorldFeature> features)
    {
        var items=features.ToList();
        if(items.Count==1||items[0].Closed)return items[0].Geometry;
        var segments=new List<WorldPosition>();
        var seen=new HashSet<(WorldPosition A,WorldPosition B)>();
        foreach(var feature in items)
        {
            var geometry=feature.Geometry??Array.Empty<WorldPosition>();
            if(geometry.Count==1){segments.Add(geometry[0]);continue;}
            for(int i=0;i+1<geometry.Count;i+=2)
            {
                var a=geometry[i];var b=geometry[i+1];
                var key=Compare(a,b)<=0?(a,b):(b,a);
                if(!seen.Add(key))continue;
                segments.Add(a);segments.Add(b);
            }
        }
        return segments;
    }

    private static int Compare(WorldPosition a,WorldPosition b)
    {
        int x=a.X.CompareTo(b.X);if(x!=0)return x;
        int z=a.Z.CompareTo(b.Z);if(z!=0)return z;
        return a.Y.CompareTo(b.Y);
    }

    private static WorldFeature Clip(WorldFeature f,double l,double b,double r,double t)
    {
        var g=f.Geometry??Array.Empty<WorldPosition>();
        if(g.Count==0)return null;
        if(f.Closed&&g.Count>=3)
        {
            var poly=g.ToList();
            poly=ClipSide(poly,p=>p.X>=l,(a,c)=>CutX(a,c,l)); poly=ClipSide(poly,p=>p.X<=r,(a,c)=>CutX(a,c,r));
            poly=ClipSide(poly,p=>p.Z>=b,(a,c)=>CutZ(a,c,b)); poly=ClipSide(poly,p=>p.Z<=t,(a,c)=>CutZ(a,c,t));
            return poly.Count>=3?f with{Geometry=poly}:null;
        }
        if(g.Count==1)return Inside(g[0],l,b,r,t)?f:null;
        var lines=new List<WorldPosition>();
        for(int i=0;i+1<g.Count;i+=2) if(ClipLine(g[i],g[i+1],l,b,r,t,out var a,out var c)){lines.Add(a);lines.Add(c);}
        return lines.Count>0?f with{Geometry=lines}:null;
    }
    private static bool Inside(WorldPosition p,double l,double b,double r,double t)=>p.X>=l&&p.X<=r&&p.Z>=b&&p.Z<=t;
    private static bool ClipLine(WorldPosition a,WorldPosition c,double l,double b,double r,double t,out WorldPosition p,out WorldPosition q)
    {
        double dx=c.X-a.X,dz=c.Z-a.Z,u0=0,u1=1;
        bool Test(double k,double v){if(Math.Abs(k)<1e-12)return v>=0;double u=v/k;if(k<0){if(u>u1)return false;if(u>u0)u0=u;}else{if(u<u0)return false;if(u<u1)u1=u;}return true;}
        p=q=default;if(!Test(-dx,a.X-l)||!Test(dx,r-a.X)||!Test(-dz,a.Z-b)||!Test(dz,t-a.Z))return false;
        if(u1-u0<=1e-12)return false;
        p=new(a.X+u0*dx,a.Y+u0*(c.Y-a.Y),a.Z+u0*dz);q=new(a.X+u1*dx,a.Y+u1*(c.Y-a.Y),a.Z+u1*dz);return true;
    }
    private static List<WorldPosition> ClipSide(List<WorldPosition> p,Func<WorldPosition,bool> inside,Func<WorldPosition,WorldPosition,WorldPosition> intersect)
    {var o=new List<WorldPosition>();if(p.Count==0)return o;for(int i=0;i<p.Count;i++){var a=p[i];var c=p[(i+1)%p.Count];bool ia=inside(a),ic=inside(c);if(ia)o.Add(a);if(ia!=ic)o.Add(intersect(a,c));}return o;}
    private static WorldPosition CutX(WorldPosition a,WorldPosition b,double x){double u=(x-a.X)/(b.X-a.X);return new(x,a.Y+u*(b.Y-a.Y),a.Z+u*(b.Z-a.Z));}
    private static WorldPosition CutZ(WorldPosition a,WorldPosition b,double z){double u=(z-a.Z)/(b.Z-a.Z);return new(a.X+u*(b.X-a.X),a.Y+u*(b.Y-a.Y),z);}
}
