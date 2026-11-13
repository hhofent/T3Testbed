using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// Tangent mode for curve keyframes.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TangentMode
    {
        /// <summary>Linear interpolation between keyframes.</summary>
        Linear,
        /// <summary>Smooth (auto-calculated) tangents.</summary>
        Smooth,
        /// <summary>Flat tangents (zero slope).</summary>
        Flat,
        /// <summary>Broken: left and right tangents independent.</summary>
        Broken,
    }

    /// <summary>
    /// A single keyframe in an animation curve.
    /// </summary>
    public sealed class Keyframe : IEquatable<Keyframe>
    {
        public float Time { get; set; }
        public float Value { get; set; }
        public float InTangent { get; set; }
        public float OutTangent { get; set; }
        public TangentMode TangentMode { get; set; } = TangentMode.Smooth;

        public Keyframe() { }

        public Keyframe(float time, float value, TangentMode mode = TangentMode.Smooth)
        {
            Time = time; Value = value; TangentMode = mode;
        }

        public Keyframe(float time, float value, float inTangent, float outTangent)
        {
            Time = time; Value = value; InTangent = inTangent; OutTangent = outTangent; TangentMode = TangentMode.Broken;
        }

        public bool Equals(Keyframe other) =>
            other != null && Time == other.Time && Value == other.Value &&
            InTangent == other.InTangent && OutTangent == other.OutTangent && TangentMode == other.TangentMode;

        public override bool Equals(object obj) => obj is Keyframe k && Equals(k);
        public override int GetHashCode() => HashCode.Combine(Time, Value, InTangent, OutTangent, TangentMode);
    }

    /// <summary>
    /// A 1D animation curve (time -> value) with keyframe interpolation.
    /// Used for distance falloffs, density gradients, etc.
    /// </summary>
    [JsonConverter(typeof(AnimationCurveConverter))]
    public sealed class AnimationCurve : IEquatable<AnimationCurve>
    {
        public List<Keyframe> Keys { get; set; } = new();
        public float PreWrapValue { get; set; } = 0f;   // Value before first key
        public float PostWrapValue { get; set; } = 0f;  // Value after last key
        public bool Loop { get; set; } = false;         // Loop the curve

        public AnimationCurve() { }

        public AnimationCurve(params Keyframe[] keys) => Keys.AddRange(keys);

        /// <summary>Add a keyframe, keeping keys sorted by time.</summary>
        public void AddKey(Keyframe key)
        {
            int i = Keys.BinarySearch(key, KeyComparer.Instance);
            if (i < 0) i = ~i;
            Keys.Insert(i, key);
        }

        /// <summary>Evaluate the curve at time t.</summary>
        public float Evaluate(float t)
        {
            if (Keys.Count == 0) return 0f;
            if (Keys.Count == 1) return Keys[0].Value;

            if (t <= Keys[0].Time)
            {
                if (Loop)
                {
                    float duration = Keys[^1].Time - Keys[0].Time;
                    if (duration > 0) t = Keys[0].Time + (t - Keys[0].Time) % duration;
                }
                return PreWrapValue;
            }
            if (t >= Keys[^1].Time)
            {
                if (Loop)
                {
                    float duration = Keys[^1].Time - Keys[0].Time;
                    if (duration > 0) t = Keys[0].Time + (t - Keys[0].Time) % duration;
                }
                return PostWrapValue;
            }

            // Find segment
            int i = 0;
            while (i + 1 < Keys.Count && Keys[i + 1].Time <= t) i++;

            var k0 = Keys[i];
            var k1 = Keys[i + 1];
            float dt = k1.Time - k0.Time;
            if (dt <= 0) return k0.Value;

            float u = (t - k0.Time) / dt;

            // Cubic Hermite interpolation
            float m0 = GetOutTangent(k0) * dt;
            float m1 = GetInTangent(k1) * dt;

            float u2 = u * u;
            float u3 = u2 * u;
            float h00 = 2 * u3 - 3 * u2 + 1;
            float h10 = u3 - 2 * u2 + u;
            float h01 = -2 * u3 + 3 * u2;
            float h11 = u3 - u2;

            return h00 * k0.Value + h10 * m0 + h01 * k1.Value + h11 * m1;
        }

        private float GetOutTangent(Keyframe k)
        {
            return k.TangentMode switch
            {
                TangentMode.Linear => 1f,
                TangentMode.Flat => 0f,
                TangentMode.Broken => k.OutTangent,
                _ => AutoTangent(k, true), // Smooth
            };
        }

        private float GetInTangent(Keyframe k)
        {
            return k.TangentMode switch
            {
                TangentMode.Linear => 1f,
                TangentMode.Flat => 0f,
                TangentMode.Broken => k.InTangent,
                _ => AutoTangent(k, false), // Smooth
            };
        }

        private float AutoTangent(Keyframe k, bool isOut)
        {
            int idx = Keys.IndexOf(k);
            if (idx < 0) return 0f;

            if (isOut)
            {
                if (idx + 1 >= Keys.Count) return 0f;
                var next = Keys[idx + 1];
                return (next.Value - k.Value) / (next.Time - k.Time);
            }
            else
            {
                if (idx - 1 < 0) return 0f;
                var prev = Keys[idx - 1];
                return (k.Value - prev.Value) / (k.Time - prev.Time);
            }
        }

        /// <summary>Create a linear curve from (0,0) to (1,1).</summary>
        public static AnimationCurve Linear01() => new(
            new Keyframe(0, 0, TangentMode.Linear),
            new Keyframe(1, 1, TangentMode.Linear));

        /// <summary>Create a smoothstep curve (0,0) to (1,1) with zero derivatives at ends.</summary>
        public static AnimationCurve SmoothStep01() => new(
            new Keyframe(0, 0, TangentMode.Flat),
            new Keyframe(1, 1, TangentMode.Flat));

        /// <summary>Create a curve from keyframe pairs (time, value).</summary>
        public static AnimationCurve FromKeys(params (float time, float value)[] keys)
        {
            var curve = new AnimationCurve();
            foreach (var (t, v) in keys) curve.AddKey(new Keyframe(t, v));
            return curve;
        }

        public bool Equals(AnimationCurve other) =>
            other != null && PreWrapValue == other.PreWrapValue && PostWrapValue == other.PostWrapValue &&
            Loop == other.Loop && Keys.Count == other.Keys.Count &&
            Keys.SequenceEqual(other.Keys);

        public override bool Equals(object obj) => obj is AnimationCurve c && Equals(c);
        public override int GetHashCode() => HashCode.Combine(PreWrapValue, PostWrapValue, Loop, Keys.Count);

        private sealed class KeyComparer : IComparer<Keyframe>
        {
            public static readonly KeyComparer Instance = new();
            public int Compare(Keyframe x, Keyframe y) => x.Time.CompareTo(y.Time);
        }
    }

    internal class AnimationCurveConverter : JsonConverter<AnimationCurve>
    {
        public override AnimationCurve Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var curve = new AnimationCurve
            {
                PreWrapValue = root.TryGetProperty("preWrap", out var pw) ? pw.GetSingle() : 0,
                PostWrapValue = root.TryGetProperty("postWrap", out var pow) ? pow.GetSingle() : 0,
                Loop = root.TryGetProperty("loop", out var lp) && lp.GetBoolean(),
            };

            if (root.TryGetProperty("keys", out var keysArray) && keysArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var k in keysArray.EnumerateArray())
                {
                    curve.Keys.Add(new Keyframe
                    {
                        Time = k.GetProperty("time").GetSingle(),
                        Value = k.GetProperty("value").GetSingle(),
                        InTangent = k.TryGetProperty("inTangent", out var it) ? it.GetSingle() : 0,
                        OutTangent = k.TryGetProperty("outTangent", out var ot) ? ot.GetSingle() : 0,
                        TangentMode = Enum.TryParse<TangentMode>(k.GetProperty("tangentMode").GetString(), true, out var tm) ? tm : TangentMode.Smooth,
                    });
                }
            }

            return curve;
        }

        public override void Write(Utf8JsonWriter writer, AnimationCurve value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("preWrap", value.PreWrapValue);
            writer.WriteNumber("postWrap", value.PostWrapValue);
            writer.WriteBoolean("loop", value.Loop);
            writer.WritePropertyName("keys");
            writer.WriteStartArray();
            foreach (var k in value.Keys)
            {
                writer.WriteStartObject();
                writer.WriteNumber("time", k.Time);
                writer.WriteNumber("value", k.Value);
                writer.WriteNumber("inTangent", k.InTangent);
                writer.WriteNumber("outTangent", k.OutTangent);
                writer.WriteString("tangentMode", k.TangentMode.ToString());
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }
}