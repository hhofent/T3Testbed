using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CsharpSpawner
{
    public static class WaterSpawner
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { IncludeFields = true };

        /// <summary>In-process fill of saved river beds and below-sea basins.</summary>
        public static WaterChunk GenerateChunk(WaterChunkInput input) => Fill(input);
        public static IReadOnlyList<WorldFeature> GenerateWorldFeatures(WaterChunkInput input) =>
            LegacyMapFeatureConverter.ToWorldFeatures(Fill(input).Features);

        public static async Task<int> RunWorkerAsync()
        {
            try
            {
                var request = JsonSerializer.Deserialize<WaterRequest>(await Console.In.ReadToEndAsync().ConfigureAwait(false), JsonOptions)
                    ?? throw new InvalidOperationException("Missing water worker request.");
                using var stdout = new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                foreach (var input in request.Chunks ?? new List<WaterChunkInput>())
                {
                    var chunk = Fill(input);
                    await stdout.WriteLineAsync(JsonSerializer.Serialize(chunk, JsonOptions)).ConfigureAwait(false);
                }
                return 0;
            }
            catch (Exception ex) { await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false); return 1; }
        }

        private static WaterChunk Fill(WaterChunkInput input)
        {
            var result = new WaterChunk { ChunkX = input.ChunkX, ChunkY = input.ChunkY };
            double sea = input.SeaLevelFt, left = input.ChunkX * 5280.0, top = input.ChunkY * 5280.0;
            double right = left + 5280, bottom = top + 5280;
            var grid = input.WaterGrid;
            if (grid != null && grid.Values.Count > 1)
            {
                for (int y = 0; y + 1 < grid.Values.Count; y++)
                for (int x = 0; x + 1 < grid.Values[y].Count; x++)
                {
                    double x0 = grid.OriginXFt + x * grid.StepFt, y0 = grid.OriginYFt + y * grid.StepFt;
                    double x1 = x0 + grid.StepFt, y1 = y0 + grid.StepFt;
                    if (x1 <= left || x0 >= right || y1 <= top || y0 >= bottom) continue;
                    var corners = new List<(double X, double Y, double H)>
                    {
                        (x0,y0,grid.Values[y][x]), (x1,y0,grid.Values[y][x+1]),
                        (x1,y1,grid.Values[y+1][x+1]), (x0,y1,grid.Values[y+1][x])
                    };
                    var polygon = ClipBelowSea(corners, sea);
                    if (polygon.Count < 3) continue;
                    polygon = ClipRect(polygon, left, top, right, bottom);
                    if (polygon.Count < 3) continue;
                    var lake = new MapFeature { Kind = "lakes", Closed = true, Disjoint = false, DepthFt = Math.Max(0, sea - corners.Min(p => p.H)) };
                    foreach (var p in polygon) lake.Points.Add(new MapPoint { X = p.X / 5280, Y = p.Y / 5280 });
                    result.Features.Add(lake);
                }
            }
            foreach (var bed in input.RiverBeds ?? new List<MapFeature>())
            {
                result.Features.Add(new MapFeature
                {
                    Kind = "rivers", Width = bed.Width, Disjoint = bed.Disjoint,
                    Points = ClipSavedRiverBed(bed, input.ChunkX, input.ChunkY), StreamOrder = bed.StreamOrder,
                    WaveApplied = true
                });
            }
            return result;
        }

        private static List<MapPoint> ClipSavedRiverBed(MapFeature bed, int chunkX, int chunkY)
        {
            var output = new List<MapPoint>(bed.Points?.Count ?? 0);
            if (bed.Points == null) return output;
            for (int i = 0; i + 1 < bed.Points.Count; i += 2)
                if (ClipRiverSegment(bed.Points[i], bed.Points[i + 1], chunkX, chunkY, out var a, out var b))
                { output.Add(a); output.Add(b); }
            return output;
        }

        private static bool ClipRiverSegment(MapPoint a, MapPoint b, int chunkX, int chunkY, out MapPoint clippedA, out MapPoint clippedB)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, t0 = 0, t1 = 1;
            bool Test(double p, double q)
            {
                if (Math.Abs(p) < 1e-12) return q >= 0;
                double r = q / p;
                if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
                else { if (r < t0) return false; if (r < t1) t1 = r; }
                return true;
            }
            if (!Test(-dx, a.X - chunkX) || !Test(dx, chunkX + 1 - a.X) ||
                !Test(-dy, a.Y - chunkY) || !Test(dy, chunkY + 1 - a.Y))
            { clippedA = clippedB = null; return false; }
            clippedA = new MapPoint { X = a.X + t0 * dx, Y = a.Y + t0 * dy };
            clippedB = new MapPoint { X = a.X + t1 * dx, Y = a.Y + t1 * dy };
            return t1 >= t0;
        }

        private static List<(double X, double Y, double H)> ClipBelowSea(List<(double X, double Y, double H)> polygon, double sea)
        {
            var output = new List<(double, double, double)>();
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                bool insideA = a.H <= sea, insideB = b.H <= sea;
                if (insideA) output.Add(a);
                if (insideA != insideB)
                {
                    double t = (sea - a.H) / (b.H - a.H);
                    output.Add((a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y), sea));
                }
            }
            return output;
        }

        private static List<(double X, double Y, double H)> ClipRect(List<(double X, double Y, double H)> points, double left, double top, double right, double bottom)
        {
            var a = ClipSide(points, p => p.X >= left, (p, q) => IntersectX(p, q, left));
            var b = ClipSide(a, p => p.X <= right, (p, q) => IntersectX(p, q, right));
            var c = ClipSide(b, p => p.Y >= top, (p, q) => IntersectY(p, q, top));
            return ClipSide(c, p => p.Y <= bottom, (p, q) => IntersectY(p, q, bottom));
        }

        private static List<(double X, double Y, double H)> ClipSide(List<(double X, double Y, double H)> points,
            Func<(double X, double Y, double H), bool> inside, Func<(double X, double Y, double H), (double X, double Y, double H), (double X, double Y, double H)> intersect)
        {
            var output = new List<(double, double, double)>();
            if (points.Count == 0) return output;
            for (int i = 0; i < points.Count; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Count]; bool ia = inside(a), ib = inside(b);
                if (ia) output.Add(a);
                if (ia != ib) output.Add(intersect(a, b));
            }
            return output;
        }

        private static (double X, double Y, double H) IntersectX((double X, double Y, double H) a, (double X, double Y, double H) b, double x)
        { double t = (x - a.X) / (b.X - a.X); return (x, a.Y + (b.Y - a.Y) * t, a.H + (b.H - a.H) * t); }
        private static (double X, double Y, double H) IntersectY((double X, double Y, double H) a, (double X, double Y, double H) b, double y)
        { double t = (y - a.Y) / (b.Y - a.Y); return (a.X + (b.X - a.X) * t, y, a.H + (b.H - a.H) * t); }
    }
}

