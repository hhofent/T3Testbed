using System;
using System.Collections.Generic;
using System.Linq;

namespace CsharpSpawner;

/// <summary>One-mile bucket index for map features with mile-based coordinates.</summary>
internal sealed class FeatureSpatialIndex
{
    private readonly Dictionary<(int X, int Y), List<MapFeature>> _buckets = new();
    private readonly List<MapFeature> _unbucketed = new();

    public FeatureSpatialIndex(IEnumerable<MapFeature> features)
    {
        foreach (var feature in features ?? Array.Empty<MapFeature>())
        {
            var points = feature?.Points;
            if (points == null || points.Count == 0) { _unbucketed.Add(feature); continue; }
            double minX = points.Min(p => p.X), maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);
            if (!double.IsFinite(minX) || !double.IsFinite(maxX) || !double.IsFinite(minY) || !double.IsFinite(maxY)) continue;
            int left = (int)Math.Floor(minX), right = (int)Math.Floor(maxX);
            int top = (int)Math.Floor(minY), bottom = (int)Math.Floor(maxY);
            // Avoid exploding the index for malformed/world-spanning features.
            if ((long)(right - left + 1) * (bottom - top + 1) > 4096) { _unbucketed.Add(feature); continue; }
            for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++)
            {
                if (!_buckets.TryGetValue((x, y), out var bucket)) _buckets[(x, y)] = bucket = new List<MapFeature>();
                bucket.Add(feature);
            }
        }
    }

    public List<MapFeature> QueryChunk(int chunkX, int chunkY, double margin)
    {
        double left = chunkX - margin, right = chunkX + 1 + margin;
        double top = chunkY - margin, bottom = chunkY + 1 + margin;
        var candidates = new HashSet<MapFeature>(_unbucketed);
        int x0 = (int)Math.Floor(left), x1 = (int)Math.Floor(right);
        int y0 = (int)Math.Floor(top), y1 = (int)Math.Floor(bottom);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            if (_buckets.TryGetValue((x, y), out var bucket)) candidates.UnionWith(bucket);
        return candidates.Where(f => f?.Points is { Count: > 0 } points &&
            points.Max(p => p.X) >= left && points.Min(p => p.X) <= right &&
            points.Max(p => p.Y) >= top && points.Min(p => p.Y) <= bottom).ToList();
    }
}
