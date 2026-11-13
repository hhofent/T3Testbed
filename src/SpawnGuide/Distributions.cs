using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// Shape of a probability distribution for procedural generation parameters.
    /// </summary>
    public enum DistributionShape
    {
        /// <summary>Uniform distribution between Min and Max.</summary>
        Uniform,
        /// <summary>Normal (Gaussian) distribution with Mean and StdDev. Clamped to [Min, Max].</summary>
        Normal,
        /// <summary>Log-normal distribution: log(X) ~ Normal(Mean, StdDev). Clamped to [Min, Max].</summary>
        LogNormal,
        /// <summary>Poisson distribution with Lambda (mean). Returns integer, clamped to [Min, Max].</summary>
        Poisson,
        /// <summary>Beta distribution on [0,1] scaled to [Min, Max]. Shape parameters Alpha, Beta.</summary>
        Beta,
        /// <summary>Triangular distribution with Min, Mode, Max.</summary>
        Triangular,
        /// <summary>Exponential distribution with Lambda (rate). Clamped to [Min, Max].</summary>
        Exponential,
        /// <summary>Bimodal: mixture of two point masses at Value1 and Value2 with weights Weight1, Weight2.</summary>
        Bimodal,
        /// <summary>Dirichlet distribution: vector of concentrations Alpha[], returns normalized weights summing to 1.</summary>
        Dirichlet,
    }

    /// <summary>
    /// A configurable floating-point distribution for procedural parameters.
    /// Supports multiple shapes with shape-specific parameters.
    /// </summary>
    [JsonConverter(typeof(FloatDistributionConverter))]
    public readonly struct FloatDistribution : IEquatable<FloatDistribution>
    {
        public readonly DistributionShape Shape;
        public readonly float Min;
        public readonly float Max;
        public readonly float Mean;
        public readonly float StdDev;
        public readonly float Mode;
        public readonly float Lambda;
        public readonly float Alpha;
        public readonly float BetaParam;
        public readonly float Weight1;
        public readonly float Value1;
        public readonly float Value2;
        public readonly float[] Concentrations; // For Dirichlet

        public FloatDistribution(DistributionShape shape, float min = 0, float max = 1, float mean = 0, float stdDev = 1,
            float mode = 0, float lambda = 1, float alpha = 1, float beta = 1, float weight1 = 0.5f, float value1 = 0, float value2 = 1, float[] concentrations = null)
        {
            Shape = shape;
            Min = min;
            Max = max;
            Mean = mean;
            StdDev = stdDev;
            Mode = mode;
            Lambda = lambda;
            Alpha = alpha;
            BetaParam = beta;
            Weight1 = weight1;
            Value1 = value1;
            Value2 = value2;
            Concentrations = concentrations;
        }

        /// <summary>Uniform distribution between min and max.</summary>
        public static FloatDistribution Uniform(float min, float max) => new(DistributionShape.Uniform, min, max);

        /// <summary>Normal distribution with mean and stdDev, clamped to [min, max].</summary>
        public static FloatDistribution Normal(float mean, float stdDev, float min = float.MinValue, float max = float.MaxValue)
            => new(DistributionShape.Normal, min, max, mean, stdDev);

        /// <summary>Log-normal distribution: log(X) ~ Normal(mean, stdDev), clamped to [min, max].</summary>
        public static FloatDistribution LogNormal(float mean, float stdDev, float min = 0, float max = float.MaxValue)
            => new(DistributionShape.LogNormal, min, max, mean, stdDev);

        /// <summary>Poisson distribution with lambda (mean), clamped to [min, max]. Returns float for consistency.</summary>
        public static FloatDistribution Poisson(float lambda, float min = 0, float max = float.MaxValue)
            => new(DistributionShape.Poisson, min, max, lambda: lambda);

        /// <summary>Beta distribution on [0,1] scaled to [min, max] with shape parameters alpha, beta.</summary>
        public static FloatDistribution Beta(float alpha, float beta, float min = 0, float max = 1)
            => new(DistributionShape.Beta, min, max, alpha: alpha, beta: beta);

        /// <summary>Triangular distribution with min, mode, max.</summary>
        public static FloatDistribution Triangular(float min, float mode, float max)
            => new(DistributionShape.Triangular, min, max, mode: mode);

        /// <summary>Exponential distribution with lambda (rate), clamped to [min, max].</summary>
        public static FloatDistribution Exponential(float lambda, float min = 0, float max = float.MaxValue)
            => new(DistributionShape.Exponential, min, max, lambda: lambda);

        /// <summary>Bimodal: two point masses at value1 and value2 with weights weight1 and (1-weight1).</summary>
        public static FloatDistribution Bimodal(float value1, float value2, float weight1 = 0.5f)
            => new(DistributionShape.Bimodal, value1: value1, value2: value2, weight1: weight1);

        /// <summary>Dirichlet distribution with concentration parameters. Returns array of weights summing to 1.</summary>
        public static FloatDistribution Dirichlet(params float[] concentrations)
            => new(DistributionShape.Dirichlet, concentrations: concentrations);

        // Arithmetic operators for config convenience
        public static FloatDistribution operator +(FloatDistribution d, float v)
            => new(d.Shape, d.Min + v, d.Max + v, d.Mean + v, d.StdDev, d.Mode + v, d.Lambda, d.Alpha, d.BetaParam, d.Weight1, d.Value1 + v, d.Value2 + v, d.Concentrations);

        public static FloatDistribution operator -(FloatDistribution d, float v)
            => new(d.Shape, d.Min - v, d.Max - v, d.Mean - v, d.StdDev, d.Mode - v, d.Lambda, d.Alpha, d.BetaParam, d.Weight1, d.Value1 - v, d.Value2 - v, d.Concentrations);

        public static FloatDistribution operator +(float v, FloatDistribution d) => d + v;
        public static FloatDistribution operator -(float v, FloatDistribution d) => new(d.Shape, v - d.Min, v - d.Max, v - d.Mean, d.StdDev, v - d.Mode, d.Lambda, d.Alpha, d.BetaParam, d.Weight1, v - d.Value1, v - d.Value2, d.Concentrations);

        // Comparison operators with float (for config comparisons)
        public static bool operator >=(FloatDistribution d, float v) => d.Min >= v;
        public static bool operator <=(FloatDistribution d, float v) => d.Max <= v;
        public static bool operator >(FloatDistribution d, float v) => d.Min > v;
        public static bool operator <(FloatDistribution d, float v) => d.Max < v;
        public static bool operator >=(float v, FloatDistribution d) => v >= d.Min;
        public static bool operator <=(float v, FloatDistribution d) => v <= d.Max;
        public static bool operator >(float v, FloatDistribution d) => v > d.Min;
        public static bool operator <(float v, FloatDistribution d) => v < d.Max;

        /// <summary>Sample a single value from this distribution using the provided RNG.</summary>
        public float Sample(Rng rng)
        {
            return Shape switch
            {
                DistributionShape.Uniform => rng.Range(Min, Max),
                DistributionShape.Normal => Math.Clamp((float)(rng.NextGaussian() * StdDev + Mean), Min, Max),
                DistributionShape.LogNormal => Math.Clamp((float)Math.Exp(rng.NextGaussian() * StdDev + Mean), Min, Max),
                DistributionShape.Poisson => Math.Clamp((float)rng.NextPoisson(Lambda), Min, Max),
                DistributionShape.Beta => Math.Clamp(Min + (Max - Min) * SampleBeta(rng, Alpha, BetaParam), Min, Max),
                DistributionShape.Triangular => SampleTriangular(rng, Min, Mode, Max),
                DistributionShape.Exponential => Math.Clamp((float)(-Math.Log(1 - rng.NextFloat()) / Lambda), Min, Max),
                DistributionShape.Bimodal => rng.Chance(Weight1) ? Value1 : Value2,
                DistributionShape.Dirichlet => throw new InvalidOperationException("Dirichlet returns array, use SampleDirichlet"),
                _ => Min,
            };
        }

        /// <summary>Sample a Dirichlet vector (weights summing to 1).</summary>
        public float[] SampleDirichlet(Rng rng)
        {
            if (Shape != DistributionShape.Dirichlet || Concentrations == null || Concentrations.Length == 0)
                return new[] { 1f };

            int n = Concentrations.Length;
            var gammas = new float[n];
            float sum = 0;
            for (int i = 0; i < n; i++)
            {
                gammas[i] = (float)SampleGamma(rng, Concentrations[i], 1.0f);
                sum += gammas[i];
            }
            for (int i = 0; i < n; i++) gammas[i] /= sum;
            return gammas;
        }

        /// <summary>Sample an integer from this distribution (for Poisson, etc.).</summary>
        public int SampleInt(Rng rng) => (int)Math.Round(Sample(rng));

        // Simple Box-Muller for Gaussian (Rng doesn't have it, so we add extension)
        // Actually we'll add NextGaussian to Rng via extension or use a local implementation
        // For now, approximate with 12 uniform samples (Central Limit Theorem)
        private float NextGaussianLocal(Rng rng)
        {
            float sum = 0;
            for (int i = 0; i < 12; i++) sum += rng.NextFloat();
            return sum - 6f; // Approximate N(0,1)
        }

        private static float SampleBeta(Rng rng, float alpha, float beta)
        {
            // Marsaglia's method for Beta
            float a = alpha, b = beta;
            while (true)
            {
                float u1 = rng.NextFloat();
                float u2 = rng.NextFloat();
                float v1 = MathF.Pow(u1, 1f / a);
                float v2 = MathF.Pow(u2, 1f / b);
                float s = v1 + v2;
                if (s <= 1f) return v1 / s;
            }
        }

        private static float SampleTriangular(Rng rng, float min, float mode, float max)
        {
            float u = rng.NextFloat();
            float c = (mode - min) / (max - min);
            if (u < c) return min + MathF.Sqrt(u * (max - min) * (mode - min));
            return max - MathF.Sqrt((1 - u) * (max - min) * (max - mode));
        }

        private static float SampleGamma(Rng rng, float shape, float scale)
        {
            // Marsaglia & Tsang method for Gamma(shape, scale)
            if (shape >= 1)
            {
                float d = shape - 1f / 3f;
                float c = 1f / MathF.Sqrt(9f * d);
                while (true)
                {
                    float x;
                    float v;
                    do
                    {
                        x = NextGaussianStatic(rng);
                        v = 1f + c * x;
                    } while (v <= 0);
                    v = v * v * v;
                    float u = rng.NextFloat();
                    if (u < 1 - 0.0331f * x * x * x * x) return d * v * scale;
                    if (MathF.Log(u) < 0.5f * x * x + d * (1 - v + MathF.Log(v))) return d * v * scale;
                }
            }
            else // shape < 1
            {
                return SampleGamma(rng, shape + 1, scale) * MathF.Pow(rng.NextFloat(), 1f / shape);
            }
        }

        private static float NextGaussianStatic(Rng rng)
        {
            float sum = 0;
            for (int i = 0; i < 12; i++) sum += rng.NextFloat();
            return sum - 6f;
        }

        public bool Equals(FloatDistribution other) =>
            Shape == other.Shape && Min == other.Min && Max == other.Max &&
            Mean == other.Mean && StdDev == other.StdDev && Mode == other.Mode &&
            Lambda == other.Lambda && Alpha == other.Alpha && BetaParam == other.BetaParam &&
            Weight1 == other.Weight1 && Value1 == other.Value1 && Value2 == other.Value2;

        public override bool Equals(object obj) => obj is FloatDistribution d && Equals(d);
        public override int GetHashCode() => HashCode.Combine(HashCode.Combine(Shape, Min, Max, Mean, StdDev, Mode, Lambda, Alpha), HashCode.Combine(BetaParam, Weight1, Value1, Value2));
        public static bool operator ==(FloatDistribution a, FloatDistribution b) => a.Equals(b);
        public static bool operator !=(FloatDistribution a, FloatDistribution b) => !a.Equals(b);

        public override string ToString() => Shape switch
        {
            DistributionShape.Uniform => $"Uniform({Min}, {Max})",
            DistributionShape.Normal => $"Normal({Mean}, {StdDev}) [{Min}, {Max}]",
            DistributionShape.LogNormal => $"LogNormal({Mean}, {StdDev}) [{Min}, {Max}]",
            DistributionShape.Poisson => $"Poisson({Lambda}) [{Min}, {Max}]",
            DistributionShape.Beta => $"Beta({Alpha}, {BetaParam}) [{Min}, {Max}]",
            DistributionShape.Triangular => $"Triangular({Min}, {Mode}, {Max})",
            DistributionShape.Exponential => $"Exp({Lambda}) [{Min}, {Max}]",
            DistributionShape.Bimodal => $"Bimodal({Value1}:{Weight1}, {Value2}:{1-Weight1})",
            DistributionShape.Dirichlet => $"Dirichlet([{string.Join(",", Concentrations ?? Array.Empty<float>())}])",
            _ => "Unknown",
        };
    }

    /// <summary>
    /// Integer distribution (wraps FloatDistribution for integer results).
    /// </summary>
    [JsonConverter(typeof(IntDistributionConverter))]
    public readonly struct IntDistribution : IEquatable<IntDistribution>
    {
        public readonly FloatDistribution FloatDist;

        public IntDistribution(FloatDistribution fd) => FloatDist = fd;

        public static IntDistribution Uniform(int min, int max) => new(FloatDistribution.Uniform(min, max + 1)); // max exclusive in float
        public static IntDistribution Normal(float mean, float stdDev, int min = int.MinValue, int max = int.MaxValue)
            => new(FloatDistribution.Normal(mean, stdDev, min, max + 1));
        public static IntDistribution Poisson(float lambda, int min = 0, int max = int.MaxValue)
            => new(FloatDistribution.Poisson(lambda, min, max + 1));
        public static IntDistribution Triangular(int min, int mode, int max)
            => new(FloatDistribution.Triangular(min, mode, max + 1));

        public int Sample(Rng rng) => FloatDist.SampleInt(rng);
        public int SampleInt(Rng rng) => Sample(rng);

        public bool Equals(IntDistribution other) => FloatDist.Equals(other.FloatDist);
        public override bool Equals(object obj) => obj is IntDistribution d && Equals(d);
        public override int GetHashCode() => FloatDist.GetHashCode();
        public static bool operator ==(IntDistribution a, IntDistribution b) => a.Equals(b);
        public static bool operator !=(IntDistribution a, IntDistribution b) => !a.Equals(b);
        public override string ToString() => $"Int({FloatDist})";
    }

    // JSON Converters
    internal class FloatDistributionConverter : JsonConverter<FloatDistribution>
    {
        public override FloatDistribution Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
                return FloatDistribution.Uniform(reader.GetSingle(), reader.GetSingle());

            // Handle legacy array format [min, max] for backward compatibility
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                reader.Read(); // min
                float minVal = reader.GetSingle();
                reader.Read(); // max
                float maxVal = reader.GetSingle();
                reader.Read(); // end array
                return FloatDistribution.Uniform(minVal, maxVal);
            }

            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            string shapeStr = "Uniform";
            if (root.TryGetProperty("shape", out var shapeProp))
                shapeStr = shapeProp.GetString() ?? "Uniform";
            if (!Enum.TryParse<DistributionShape>(shapeStr, true, out var shape))
                shape = DistributionShape.Uniform;

            float min = root.TryGetProperty("min", out var m) ? m.GetSingle() : 0;
            float max = root.TryGetProperty("max", out var mx) ? mx.GetSingle() : 1;
            float mean = root.TryGetProperty("mean", out var mn) ? mn.GetSingle() : 0;
            float stdDev = root.TryGetProperty("stdDev", out var sd) ? sd.GetSingle() : 1;
            float mode = root.TryGetProperty("mode", out var md) ? md.GetSingle() : 0;
            float lambda = root.TryGetProperty("lambda", out var ld) ? ld.GetSingle() : 1;
            float alpha = root.TryGetProperty("alpha", out var al) ? al.GetSingle() : 1;
            float beta = root.TryGetProperty("beta", out var be) ? be.GetSingle() : 1;
            float weight1 = root.TryGetProperty("weight1", out var w1) ? w1.GetSingle() : 0.5f;
            float value1 = root.TryGetProperty("value1", out var v1) ? v1.GetSingle() : 0;
            float value2 = root.TryGetProperty("value2", out var v2) ? v2.GetSingle() : 1;

            float[] concentrations = null;
            if (root.TryGetProperty("concentrations", out var conc) && conc.ValueKind == JsonValueKind.Array)
            {
                concentrations = new float[conc.GetArrayLength()];
                for (int i = 0; i < concentrations.Length; i++) concentrations[i] = conc[i].GetSingle();
            }

            return new FloatDistribution(shape, min, max, mean, stdDev, mode, lambda, alpha, beta, weight1, value1, value2, concentrations);
        }

        public override void Write(Utf8JsonWriter writer, FloatDistribution value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("shape", value.Shape.ToString());
            writer.WriteNumber("min", value.Min);
            writer.WriteNumber("max", value.Max);
            if (value.Shape == DistributionShape.Normal || value.Shape == DistributionShape.LogNormal)
            {
                writer.WriteNumber("mean", value.Mean);
                writer.WriteNumber("stdDev", value.StdDev);
            }
            if (value.Shape == DistributionShape.Triangular)
                writer.WriteNumber("mode", value.Mode);
            if (value.Shape == DistributionShape.Poisson || value.Shape == DistributionShape.Exponential)
                writer.WriteNumber("lambda", value.Lambda);
            if (value.Shape == DistributionShape.Beta)
            {
                writer.WriteNumber("alpha", value.Alpha);
                writer.WriteNumber("beta", value.BetaParam);
            }
            if (value.Shape == DistributionShape.Bimodal)
            {
                writer.WriteNumber("weight1", value.Weight1);
                writer.WriteNumber("value1", value.Value1);
                writer.WriteNumber("value2", value.Value2);
            }
            if (value.Shape == DistributionShape.Dirichlet && value.Concentrations != null)
            {
                writer.WritePropertyName("concentrations");
                writer.WriteStartArray();
                foreach (var c in value.Concentrations) writer.WriteNumberValue(c);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
    }

    internal class IntDistributionConverter : JsonConverter<IntDistribution>
    {
        public override IntDistribution Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var fd = JsonSerializer.Deserialize<FloatDistribution>(ref reader, options);
            return new IntDistribution(fd);
        }

        public override void Write(Utf8JsonWriter writer, IntDistribution value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.FloatDist, options);
        }
    }
}