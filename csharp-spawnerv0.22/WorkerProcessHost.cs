using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CsharpSpawner;

namespace csharp_spawnerv0._1;

internal static class SpawnerProcessHost
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };
    public static WorldGenerationSettings Settings { get; set; } = new();
    public static Action<string> Diagnostic { get; set; }
    public static Task LoadElevationAsync(int seed, IReadOnlyList<ChunkCoordinate> chunks, Action<IReadOnlyList<ViewerScene>> cb, CancellationToken ct) => Run("--elevation-worker", new ElevationRequest { Seed=seed, Settings=Settings, Chunks=chunks.ToList() }, cb, ct);
    public static Task LoadWaterAsync(int seed, IReadOnlyList<ViewerScene> chunks, Action<IReadOnlyList<WaterChunk>> cb, CancellationToken ct) => Run("--water-worker", new WaterRequest { Seed=seed, Settings=Settings, Chunks=chunks.Select(c=>new WaterChunkInput { ChunkX=c.ChunkX, ChunkY=c.ChunkY, SeaLevelFt=Settings.SeaLevelFeet, WaterGrid=c.WaterGrid, RiverBeds=c.RiverBeds }).ToList() }, cb, ct);
    public static Task LoadPopulationAsync(int seed, IReadOnlyList<PopulationInput> chunks, IReadOnlyList<MapFeature> water, IReadOnlyList<MapFeature> roads, Action<IReadOnlyList<PopulationChunk>> cb, CancellationToken ct) => Run("--population-worker", new PopulationRequest { Seed=seed, Settings=Settings, Chunks=chunks.ToList(), WaterFeatures=water.ToList(), ExistingRoads=roads.ToList() }, cb, ct);
    public static Task LoadTransportAsync(int seed, IReadOnlyList<TransportInput> chunks, IReadOnlyList<MapFeature> water, IReadOnlyList<MapFeature> towns, IReadOnlyList<MapFeature> roads, Action<IReadOnlyList<TransportChunk>> cb, CancellationToken ct) => Run("--transport-worker", new TransportRequest { Seed=seed, Settings=Settings, Chunks=chunks.ToList(), WaterFeatures=water.ToList(), MajorTowns=towns.ToList(), MainRoads=roads.ToList() }, cb, ct);
    public static Task LoadRoadsAsync(int seed, IReadOnlyList<TownInput> chunks, IReadOnlyList<MapFeature> water, IReadOnlyList<MapFeature> mainRoads, Action<IReadOnlyList<TransportChunk>> cb, CancellationToken ct) => Run("--road-worker", new RoadRequest { Seed=seed, Settings=Settings, Chunks=chunks.ToList(), WaterFeatures=water.ToList(), MainRoadFeatures=mainRoads.ToList() }, cb, ct);
    public static Task LoadRuralAsync(int seed, IReadOnlyList<TownInput> chunks, IReadOnlyList<MapFeature> roads, IReadOnlyList<MapFeature> water, Action<IReadOnlyList<TransportChunk>> cb, CancellationToken ct) =>
        LoadRuralAsync(seed,chunks,roads,water,Array.Empty<MapFeature>(),Array.Empty<MapFeature>(),cb,ct);
    public static Task LoadRuralAsync(int seed,IReadOnlyList<TownInput> chunks,IReadOnlyList<MapFeature> roads,IReadOnlyList<MapFeature> water,
        IReadOnlyList<MapFeature> rails,IReadOnlyList<MapFeature> settlements,Action<IReadOnlyList<TransportChunk>> cb,CancellationToken ct) =>
        Run("--rural-worker",new RuralRequest{Seed=seed,Settings=Settings,Chunks=chunks.ToList(),Roads=roads.ToList(),WaterFeatures=water.ToList(),
            RailFeatures=rails.ToList(),Settlements=settlements.ToList()},cb,ct);
    public static Task LoadTownsAsync(int seed, IReadOnlyList<TownInput> chunks, IReadOnlyList<MapFeature> sites, IReadOnlyList<Settler> settlers, IReadOnlyList<MapFeature> rails, IReadOnlyList<MapFeature> roads, IReadOnlyList<MapFeature> water, IReadOnlyList<MapFeature> rural, Action<IReadOnlyList<TransportChunk>> cb, CancellationToken ct) => Run("--town-worker", new TownRequest { Seed=seed, Settings=Settings, Chunks=chunks.ToList(), Settlements=sites.ToList(), Settlers=settlers.ToList(), RailFeatures=rails.ToList(), RoadFeatures=roads.ToList(), WaterFeatures=water.ToList(), RuralFeatures=rural.ToList() }, cb, ct);
    public static Task LoadTreesAsync(int seed,IReadOnlyList<TreeInput> chunks,IReadOnlyList<MapFeature> rural,IReadOnlyList<MapFeature> towns,
        IReadOnlyList<MapFeature> roads,IReadOnlyList<MapFeature> rails,IReadOnlyList<MapFeature> water,Action<IReadOnlyList<TreeChunk>> cb,CancellationToken ct,
        IReadOnlyList<MapFeature> reservations=null) =>
        Run("--tree-worker",new TreeRequest{Seed=seed,Settings=Settings,Chunks=chunks.ToList(),RuralFeatures=rural.ToList(),TreeReservations=(reservations??Array.Empty<MapFeature>()).ToList(),TownFeatures=towns.ToList(),
            RoadFeatures=roads.ToList(),RailFeatures=rails.ToList(),WaterFeatures=water.ToList()},cb,ct);
    public static Task LoadWallPlansAsync(int seed,IReadOnlyList<WallInput> chunks,IReadOnlyList<MapFeature> towns,
        Action<IReadOnlyList<WallChunk>> cb,CancellationToken ct) =>
        Run("--wall-worker",new WallRequest{Seed=seed,Chunks=chunks.ToList(),TownFeatures=towns.ToList()},cb,ct);

    private static async Task Run<TRequest,TResult>(string argument, TRequest request, Action<IReadOnlyList<TResult>> callback, CancellationToken token)
    {
        var totalWatch = Stopwatch.StartNew();
        var inputBytes = 0L;
        var outputBytes = 0L;
        var outputChunks = 0;
        var callbackTicks = 0L;
        const int batchSize = 64;
        var batch = new List<TResult>(batchSize);
        string exe=Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate viewer executable.");
        var start=new ProcessStartInfo(exe) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardInput=true, RedirectStandardOutput=true, RedirectStandardError=true };
        start.ArgumentList.Add(argument);
        using var process=new Process { StartInfo=start };
        if(!process.Start()) throw new InvalidOperationException("Could not start worker "+argument);
        var errors=process.StandardError.ReadToEndAsync();
        try {
            var countedInput = new CountingWriteStream(process.StandardInput.BaseStream);
            await JsonSerializer.SerializeAsync(countedInput,request,JsonOptions,token).ConfigureAwait(false);
            inputBytes = countedInput.BytesWritten;
            process.StandardInput.Close();
            string line;
            while((line=await process.StandardOutput.ReadLineAsync(token).ConfigureAwait(false))!=null) {
                token.ThrowIfCancellationRequested();
                outputBytes += System.Text.Encoding.UTF8.GetByteCount(line) + 1;
                var item=JsonSerializer.Deserialize<TResult>(line,JsonOptions) ?? throw new InvalidOperationException("Worker returned empty data.");
                batch.Add(item);
                outputChunks++;
                if (batch.Count >= batchSize) FlushBatch();
            }
            if (batch.Count > 0) FlushBatch();
            await process.WaitForExitAsync(token).ConfigureAwait(false);
        } catch { try { if(!process.HasExited) process.Kill(true); } catch{} throw; }
        token.ThrowIfCancellationRequested();
        string errorText = await errors.ConfigureAwait(false);
        if(process.ExitCode!=0) throw new InvalidOperationException("Worker failed: "+errorText.Trim());
        double workerMs = 0;
        foreach (var errorLine in errorText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            if (errorLine.StartsWith("WORKER_PROFILE_MS=", StringComparison.Ordinal) &&
                double.TryParse(errorLine.Substring("WORKER_PROFILE_MS=".Length), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed)) workerMs = parsed;
        totalWatch.Stop();
        var resultType = typeof(TResult).Name;
        Diagnostic?.Invoke($"Profile {argument}: {outputChunks} chunks, payload in/out {inputBytes:N0}/{outputBytes:N0} B, worker reported {workerMs:N0} ms, round-trip {totalWatch.Elapsed.TotalMilliseconds:N0} ms, deserialize+callback/apply {TimeSpan.FromTicks(callbackTicks * TimeSpan.TicksPerSecond / Stopwatch.Frequency).TotalMilliseconds:N0} ms, result {resultType}.");

        void FlushBatch()
        {
            token.ThrowIfCancellationRequested();
            var items = batch.ToArray();
            batch.Clear();
            var applyWatch = Stopwatch.StartNew();
            callback(items);
            callbackTicks += applyWatch.ElapsedTicks;
        }
    }

    private sealed class CountingWriteStream : System.IO.Stream
    {
        private readonly System.IO.Stream _inner;
        public long BytesWritten { get; private set; }
        public CountingWriteStream(System.IO.Stream inner) => _inner = inner;
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken token) => _inner.FlushAsync(token);
        public override void Write(byte[] buffer, int offset, int count) { _inner.Write(buffer, offset, count); BytesWritten += count; }
        public override void Write(ReadOnlySpan<byte> buffer) { _inner.Write(buffer); BytesWritten += buffer.Length; }
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token) { await _inner.WriteAsync(buffer, offset, count, token).ConfigureAwait(false); BytesWritten += count; }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default) { await _inner.WriteAsync(buffer, token).ConfigureAwait(false); BytesWritten += buffer.Length; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
