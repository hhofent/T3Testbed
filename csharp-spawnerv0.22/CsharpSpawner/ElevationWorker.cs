using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CsharpSpawner
{
    public static class ElevationWorker
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { IncludeFields = true };

        /// <summary>In-process deterministic elevation generation for one routing chunk.</summary>
        public static ViewerScene GenerateChunk(int seed, int chunkX, int chunkNorthY, CancellationToken token = default) =>
            WorldGenerator.GenerateChunk(seed, chunkX, chunkNorthY, token);
        public static ViewerScene GenerateChunk(int seed, int chunkX, int chunkNorthY, WorldGenerationSettings settings, CancellationToken token = default)
        { WorldGenerator.Configure(settings); return WorldGenerator.GenerateChunk(seed, chunkX, chunkNorthY, token); }

        public static async Task<int> RunWorkerAsync()
        {
            try
            {
                var request = JsonSerializer.Deserialize<ElevationRequest>(await Console.In.ReadToEndAsync().ConfigureAwait(false), JsonOptions)
                    ?? throw new InvalidOperationException("Missing elevation worker request.");
                WorldGenerator.Configure(request.Settings);
                using var stdout = new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                foreach (var coordinate in request.Chunks ?? new System.Collections.Generic.List<ChunkCoordinate>())
                {
                    var scene = await Task.Run(() => WorldGenerator.GenerateChunk(request.Seed, coordinate.X, coordinate.Y)).ConfigureAwait(false);
                    string json = JsonSerializer.Serialize(scene, JsonOptions);
                    await stdout.WriteLineAsync(json).ConfigureAwait(false);
                }
                return 0;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false);
                return 1;
            }
        }
    }
}

