using System;
using CsharpSpawner;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace csharp_spawnerv0._1
{
    internal static class HeadlessRunner
    {
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AttachConsole(int processId);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AllocConsole();
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool FreeConsole();
        private const int AttachParent = -1;

        public static async Task<int> RunAsync()
        {
            if (!AttachConsole(AttachParent)) AllocConsole();
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
            using var stop = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; stop.Cancel(); };
            Console.CancelKeyPress += cancel;
            try
            {
                Console.WriteLine("C# Spawner: headless mode; Ctrl+C cancels generation.");
                var chunks = new System.Collections.Generic.List<ChunkCoordinate>();
                for (int y = -2; y < 3; y++) for (int x = -2; x < 3; x++) chunks.Add(new ChunkCoordinate(x, y));
                var saved = new System.Collections.Generic.List<ViewerScene>();
                await SpawnerProcessHost.LoadElevationAsync(33, chunks, batch => saved.AddRange(batch), stop.Token);
                var waterSaved = new System.Collections.Generic.List<WaterChunk>();
                await SpawnerProcessHost.LoadWaterAsync(33, saved, batch => waterSaved.AddRange(batch), stop.Token);
                var populationSaved = new System.Collections.Generic.List<PopulationChunk>();
                var populationInput = saved.Select(s => new PopulationInput
                {
                    ChunkX = s.ChunkX, ChunkY = s.ChunkY, TerrainGrid = s.TerrainGrid
                }).ToList();
                await SpawnerProcessHost.LoadPopulationAsync(33, populationInput, waterSaved.SelectMany(c => c.Features).ToList(),
                    System.Array.Empty<MapFeature>(), batch => populationSaved.AddRange(batch), stop.Token);
                var transportSaved = new System.Collections.Generic.List<TransportChunk>();
                var transportInput = saved.Select(s => new TransportInput
                {
                    ChunkX = s.ChunkX, ChunkY = s.ChunkY, TerrainGrid = s.TerrainGrid
                }).ToList();
                await SpawnerProcessHost.LoadTransportAsync(33, transportInput, waterSaved.SelectMany(c => c.Features).ToList(),
                    populationSaved.SelectMany(c => c.Features).Where(f => f.Kind == "settlement" && f.IsMajorTown).ToList(),
                    populationSaved.SelectMany(c => c.Features).Where(f => f.Kind == "main_road").ToList(),
                    batch => transportSaved.AddRange(batch), stop.Token);
                var roadSaved = new System.Collections.Generic.List<TransportChunk>();
                var roadInputs = saved.Select(s => new TownInput { ChunkX = s.ChunkX, ChunkY = s.ChunkY, TerrainGrid = s.TerrainGrid }).ToList();
                await SpawnerProcessHost.LoadRoadsAsync(33, roadInputs, waterSaved.SelectMany(c => c.Features).ToList(),
                    populationSaved.SelectMany(c => c.Features).Where(f => f.Kind == "main_road").ToList(),
                    batch => roadSaved.AddRange(batch), stop.Token);
                var ruralSaved = new System.Collections.Generic.List<TransportChunk>();
                var ruralRoads = populationSaved.SelectMany(c => c.Features).Concat(roadSaved.SelectMany(c => c.Features)).ToList();
                await SpawnerProcessHost.LoadRuralAsync(33, roadInputs, ruralRoads, waterSaved.SelectMany(c => c.Features).ToList(),
                    batch => ruralSaved.AddRange(batch), stop.Token);
                var townChunks = new System.Collections.Generic.List<TransportChunk>();
                var settlementFeatures = populationSaved.SelectMany(c => c.Features).Where(f => f.Kind == "settlement").ToList();
                var railFeatures = transportSaved.SelectMany(c => c.Features).Where(f => f.Kind == "rail").ToList();
                await SpawnerProcessHost.LoadTownsAsync(33, roadInputs, settlementFeatures, populationSaved.SelectMany(c => c.Settlers).ToList(), railFeatures,
                    ruralRoads, waterSaved.SelectMany(c => c.Features).ToList(), ruralSaved.SelectMany(c => c.Features).ToList(),
                    batch => townChunks.AddRange(batch), stop.Token);
                var treeChunks = new System.Collections.Generic.List<TreeChunk>();
                await SpawnerProcessHost.LoadTreesAsync(33, roadInputs.Select(c => new TreeInput { ChunkX = c.ChunkX, ChunkY = c.ChunkY }).ToList(),
                    ruralSaved.SelectMany(c => c.Features).ToList(), townChunks.SelectMany(c => c.Features).ToList(),
                    ruralRoads, railFeatures, waterSaved.SelectMany(c => c.Features).ToList(), batch => treeChunks.AddRange(batch), stop.Token);
                int people = populationSaved.Sum(c => c.Settlers.Count);
                int settlements = populationSaved.Sum(c => c.Features.Count(f => f.Kind == "settlement"));
                int towns = townChunks.Sum(c => c.Features.Count(f => f.Kind == "town"));
                Console.WriteLine($"Ready: elevation, water, {people} settlers at {settlements} settlement sites, rail for {transportSaved.Count} chunks, {roadSaved.Count} road chunks, {ruralSaved.Sum(c => c.Features.Count(f => f.Kind == "rural_lot"))} rural lots, {towns} towns, and {treeChunks.Sum(c => c.Features.Count)} trunk markers.");
                return 0;
            }
            catch (OperationCanceledException) { Console.WriteLine("Generation cancelled."); return 130; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            finally { Console.CancelKeyPress -= cancel; FreeConsole(); }
        }
    }
}



