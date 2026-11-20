using System;
using System.Collections.Generic;
namespace SpawnGuide
{

/// <summary>
/// Deterministic PRNG (mulberry32). Independent of System.Random so plans are
/// stable across runtimes and portable to Unity.
/// </summary>
public sealed class Rng
{
    private uint _state;

    public Rng(uint seed) => _state = seed;

    public uint NextUInt()
    {
        _state += 0x6D2B79F5u;
        uint t = _state;
        t = (t ^ (t >> 15)) * (t | 1);
        t ^= t + ((t ^ (t >> 7)) * (t | 61));
        return t ^ (t >> 14);
    }

    public float NextFloat() => NextUInt() / 4294967296f;

    public float Range(float min, float max) => min + NextFloat() * (max - min);

    public int RangeInt(int minInclusive, int maxExclusive) =>
        minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));

    public bool Chance(float probability) => NextFloat() < probability;

    public T Pick<T>(IReadOnlyList<T> items) => items[RangeInt(0, items.Count)];

    /// <summary>Generate a Gaussian (normal) random value using Box-Muller transform.</summary>
    public float NextGaussian()
    {
        // Box-Muller transform
        float u1 = NextFloat();
        float u2 = NextFloat();
        float mag = MathF.Sqrt(-2f * MathF.Log(u1));
        return mag * MathF.Cos(2f * MathF.PI * u2);
    }

    /// <summary>Generate a Poisson random value using Knuth's algorithm.</summary>
    public int NextPoisson(float lambda)
    {
        if (lambda < 20f)
        {
            // Knuth's algorithm for small lambda
            float L = MathF.Exp(-lambda);
            float p = 1f;
            int k = 0;
            do
            {
                k++;
                p *= NextFloat();
            } while (p > L);
            return k - 1;
        }
        else
        {
            // Normal approximation for large lambda
            float g = NextGaussian();
            return (int)MathF.Max(0, MathF.Round(lambda + MathF.Sqrt(lambda) * g));
        }
    }
}
}
