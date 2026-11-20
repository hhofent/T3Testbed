using System;
namespace SpawnGuide
{

/// <summary>Top-down point. X is east, Z runs front-to-back; the street/front face is at minimum Z.</summary>
public readonly struct Vec2
{
    public readonly float X;
    public readonly float Z;

    public Vec2(float x, float z) { X = x; Z = z; }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Z + b.Z);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Z - b.Z);
    public static Vec2 operator *(Vec2 a, float s) => new(a.X * s, a.Z * s);

    public float Length() => MathF.Sqrt(X * X + Z * Z);
    public Vec2 Normalized()
    {
        float len = Length();
        return len < 1e-6f ? new Vec2(0, 0) : new Vec2(X / len, Z / len);
    }
}
}
