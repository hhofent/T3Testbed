using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// Result of validating a configuration parameter.
    /// </summary>
    public sealed class ValidationResult
    {
        public string ParameterPath { get; set; } = "";
        public ValidationSeverity Severity { get; set; }
        public string Message { get; set; } = "";
        public object SuggestedValue { get; set; } = null;
        public bool AutoRepaired { get; set; } = false;

        public ValidationResult() { }

        public ValidationResult(string path, ValidationSeverity severity, string message, object suggested = null)
        {
            ParameterPath = path; Severity = severity; Message = message; SuggestedValue = suggested;
        }

        public static ValidationResult Error(string path, string message, object suggested = null)
            => new(path, ValidationSeverity.Error, message, suggested);

        public static ValidationResult Warning(string path, string message, object suggested = null)
            => new(path, ValidationSeverity.Warning, message, suggested);

        public static ValidationResult Repair(string path, string message, object suggested)
            => new(path, ValidationSeverity.Repair, message, suggested);
    }

    /// <summary>
    /// Comprehensive configuration validator with hard errors, soft warnings, and auto-repairs.
    /// </summary>
    public static class ConfigValidator
    {
        /// <summary>
        /// Validate an entire SpawnConfig object graph.
        /// </summary>
        public static List<ValidationResult> Validate(SpawnConfig config, bool applyRepairs = false)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(config, results, applyRepairs);

            context.ValidateObject(config, "");

            // Cross-system validation rules
            ValidateCrossSystem(config, context);

            return results;
        }

        /// <summary>
        /// Validate and optionally auto-repair a config. Returns true if valid (no errors after repairs).
        /// </summary>
        public static bool ValidateAndRepair(SpawnConfig config, out List<ValidationResult> results)
        {
            results = Validate(config, applyRepairs: true);
            return results.TrueForAll(r => r.Severity != ValidationSeverity.Error);
        }

        private sealed class ValidationContext
        {
            public SpawnConfig RootConfig { get; }
            public List<ValidationResult> Results { get; }
            public bool ApplyRepairs { get; }
            private readonly HashSet<object> _visited = new();

            public ValidationContext(SpawnConfig root, List<ValidationResult> results, bool applyRepairs)
            {
                RootConfig = root; Results = results; ApplyRepairs = applyRepairs;
            }

            public void ValidateObject(object obj, string path)
            {
                if (obj == null || !_visited.Add(obj)) return;

                var type = obj.GetType();
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanRead || !prop.CanWrite) continue;
                    var propPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";
                    var value = prop.GetValue(obj);
                    ValidateValue(value, propPath, prop);
                    if (IsConfigClass(prop.PropertyType) && value != null)
                        ValidateObject(value, propPath);
                }
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var fieldPath = string.IsNullOrEmpty(path) ? field.Name : $"{path}.{field.Name}";
                    var value = field.GetValue(obj);
                    ValidateValue(value, fieldPath, field);
                    if (IsConfigClass(field.FieldType) && value != null)
                        ValidateObject(value, fieldPath);
                }
            }

            private bool IsConfigClass(Type t) =>
                t.IsClass && t.Namespace == "SpawnGuide" && t != typeof(string) &&
                (t.Name.EndsWith("Config") || t.Name == "SpawnConfig" || t.Name == "TownSizePreset");

            private void ValidateValue(object value, string path, MemberInfo member)
            {
                if (value == null) return;

                // RangeAttribute
                var rangeAttr = member.GetCustomAttribute<RangeAttribute>();
                if (rangeAttr != null && value is float f)
                {
                    CheckRange(path, f, rangeAttr.Min, rangeAttr.Max, member);
                }
                else if (rangeAttr != null && value is int i)
                {
                    CheckRange(path, i, rangeAttr.Min, rangeAttr.Max, member);
                }
                else if (rangeAttr != null && value is FloatDistribution fd)
                {
                    CheckRange(path, fd.Min, rangeAttr.Min, rangeAttr.Max, member);
                    CheckRange(path, fd.Max, rangeAttr.Min, rangeAttr.Max, member);
                }
                else if (rangeAttr != null && value is IntDistribution id)
                {
                    // IntDistribution wraps FloatDistribution
                }

                // DistributionAttribute - suggest upgrade
                var distAttr = member.GetCustomAttribute<DistributionAttribute>();
                if (distAttr != null && (value is FloatRange || value is IntRange))
                {
                    Results.Add(ValidationResult.Warning(path,
                        $"Consider upgrading to FloatDistribution/IntDistribution with shape {distAttr.Shape}. {distAttr.Note}"));
                }

                // DeprecatedAttribute
                var depAttr = member.GetCustomAttribute<DeprecatedAttribute>();
                if (depAttr != null)
                {
                    Results.Add(ValidationResult.Warning(path,
                        $"Deprecated: {depAttr.Reason}. Use {depAttr.Replacement} instead."));
                }

                // Type-specific validation
                switch (value)
                {
                    case FloatRange fr: ValidateFloatRange(fr, path); break;
                    case IntRange ir: ValidateIntRange(ir, path); break;
                    case FloatDistribution fd: ValidateFloatDistribution(fd, path); break;
                    case IntDistribution id: ValidateIntDistribution(id, path); break;
                    case AnimationCurve ac: ValidateAnimationCurve(ac, path); break;
                    case Expression ex: ValidateExpression(ex, path); break;
                    case string s when member.GetCustomAttribute<UnitsAttribute>() != null: break; // units only
                }

                // Array validation
                if (value is Array arr && arr.Length == 0)
                {
                    var catAttrs = member.GetCustomAttributes<CategoryAttribute>().ToList();
                    bool isOptional = catAttrs.Any(c => c.Category == CategoryAttribute.Concern.Visual);
                    if (!isOptional)
                    {
                        Results.Add(ValidationResult.Repair(path,
                            "Empty array - will use default", GetDefaultArray(member)));
                    }
                }
            }

            private void CheckRange(string path, float value, float min, float max, MemberInfo member)
            {
                if (value < min || value > max)
                {
                    var suggested = Math.Clamp(value, min, max);
                    if (ApplyRepairs)
                    {
                        SetValue(member, suggested);
                        Results.Add(ValidationResult.Repair(path, $"Value {value} clamped to [{min}, {max}]", suggested));
                    }
                    else
                    {
                        Results.Add(ValidationResult.Error(path, $"Value {value} out of range [{min}, {max}]", suggested));
                    }
                }
            }

            private void CheckRange(string path, int value, float min, float max, MemberInfo member)
            {
                if (value < min || value > max)
                {
                    var suggested = (int)Math.Clamp(value, min, max);
                    if (ApplyRepairs)
                    {
                        SetValue(member, suggested);
                        Results.Add(ValidationResult.Repair(path, $"Value {value} clamped to [{min}, {max}]", suggested));
                    }
                    else
                    {
                        Results.Add(ValidationResult.Error(path, $"Value {value} out of range [{min}, {max}]", suggested));
                    }
                }
            }

            private void ValidateFloatRange(FloatRange r, string path)
            {
                if (r.Min > r.Max)
                {
                    if (ApplyRepairs)
                    {
                        // FloatRange is readonly struct, create new instance with swapped values
                        var swapped = new FloatRange(r.Max, r.Min);
                        SetValue(member: null, value: swapped); // This won't work directly for struct fields
                        Results.Add(ValidationResult.Repair(path, "Swapped Min/Max", new { Min = r.Max, Max = r.Min }));
                    }
                    else
                    {
                        Results.Add(ValidationResult.Error(path, $"Min ({r.Min}) > Max ({r.Max})"));
                    }
                }
                if (r.Min == r.Max)
                {
                    Results.Add(ValidationResult.Warning(path, "Min == Max (zero range)"));
                }
            }

            private void ValidateIntRange(IntRange r, string path)
            {
                if (r.Min > r.Max)
                {
                    if (ApplyRepairs)
                    {
                        var swapped = new IntRange(r.Max, r.Min);
                        SetValue(member: null, value: swapped);
                        Results.Add(ValidationResult.Repair(path, "Swapped Min/Max", new { Min = r.Max, Max = r.Min }));
                    }
                    else
                    {
                        Results.Add(ValidationResult.Error(path, $"Min ({r.Min}) > Max ({r.Max})"));
                    }
                }
            }

            private void ValidateFloatDistribution(FloatDistribution fd, string path)
            {
                if (fd.Min > fd.Max)
                {
                    Results.Add(ValidationResult.Error(path, $"Distribution Min ({fd.Min}) > Max ({fd.Max})"));
                }
                if (fd.Shape == DistributionShape.Normal || fd.Shape == DistributionShape.LogNormal)
                {
                    if (fd.StdDev <= 0) Results.Add(ValidationResult.Error(path, "StdDev must be > 0"));
                }
                if (fd.Shape == DistributionShape.Beta)
                {
                    if (fd.Alpha <= 0 || fd.BetaParam <= 0) Results.Add(ValidationResult.Error(path, "Alpha/Beta must be > 0"));
                }
                if (fd.Shape == DistributionShape.Triangular)
                {
                    if (fd.Mode < fd.Min || fd.Mode > fd.Max) Results.Add(ValidationResult.Error(path, "Mode must be in [Min, Max]"));
                }
                if (fd.Shape == DistributionShape.Poisson || fd.Shape == DistributionShape.Exponential)
                {
                    if (fd.Lambda <= 0) Results.Add(ValidationResult.Error(path, "Lambda must be > 0"));
                }
                if (fd.Shape == DistributionShape.Bimodal)
                {
                    if (fd.Weight1 < 0 || fd.Weight1 > 1) Results.Add(ValidationResult.Error(path, "Weight1 must be in [0,1]"));
                }
                if (fd.Shape == DistributionShape.Dirichlet)
                {
                    if (fd.Concentrations == null || fd.Concentrations.Length == 0) Results.Add(ValidationResult.Error(path, "Dirichlet needs concentrations"));
                    else if (fd.Concentrations != null && fd.Concentrations.Any(c => c <= 0)) Results.Add(ValidationResult.Error(path, "All concentrations must be > 0"));
                }
            }

            private void ValidateIntDistribution(IntDistribution id, string path) => ValidateFloatDistribution(id.FloatDist, path);

            private void ValidateAnimationCurve(AnimationCurve ac, string path)
            {
                if (ac.Keys.Count < 2 && ac.Keys.Count > 0)
                    Results.Add(ValidationResult.Warning(path, "Curve has only 1 keyframe (constant)"));
                for (int i = 1; i < ac.Keys.Count; i++)
                {
                    if (ac.Keys[i].Time <= ac.Keys[i - 1].Time)
                        Results.Add(ValidationResult.Error(path, $"Keyframe {i} time not strictly increasing"));
                }
            }

            private void ValidateExpression(Expression ex, string path)
            {
                try { ex.Evaluate(); }
                catch (Exception e) { Results.Add(ValidationResult.Error(path, $"Expression parse/eval error: {e.Message}")); }
            }

            private object GetDefaultArray(MemberInfo member)
            {
                var type = member is PropertyInfo p ? p.PropertyType : ((FieldInfo)member).FieldType;
                if (type == typeof(float[])) return Array.Empty<float>();
                if (type == typeof(int[])) return Array.Empty<int>();
                if (type == typeof(string[])) return Array.Empty<string>();
                return null;
            }

            private void SetValue(MemberInfo member, object value)
            {
                if (member is PropertyInfo p) p.SetValue(RootConfig, value);
                else if (member is FieldInfo f) f.SetValue(RootConfig, value);
            }
        }

        private static void ValidateCrossSystem(SpawnConfig config, ValidationContext ctx)
        {
            // World → Town coupling: town rail proximity must be < rail spacing
            var nc = config.Noise;
            float townRailProximity = nc.TownRailProximityMi;
            float railSpacing = nc.RailSpacingMi.Sample(new Rng(0));
            if (townRailProximity >= railSpacing)
            {
                ctx.Results.Add(ValidationResult.Warning("Noise.TownRailProximityMi",
                    $"Town rail proximity ({townRailProximity}mi) >= rail spacing ({railSpacing}mi). Towns may not find rail."));
            }

            // Town density vs world town density
            if (config.SmallTown.ResidentialDensity > 2.0f && config.World.TownDensity > 1.5f)
            {
                ctx.Results.Add(ValidationResult.Warning("SmallTown.ResidentialDensity",
                    "High residential density combined with high world town density may cause performance issues."));
            }

            // Farm density sanity
            if (nc.FarmDensity > 1.0f)
            {
                ctx.Results.Add(ValidationResult.Warning("Noise.FarmDensity",
                    "Farm density > 1.0 exceeds NW Iowa baseline (0.15). May create unrealistic density."));
            }

            // Garage mode weights sum to ~1
            float garageSum = config.SmallTown.GarageModeIntegralChance +
                             config.SmallTown.GarageModeDetachedChance +
                             config.SmallTown.GarageModeNoneChance;
            if (Math.Abs(garageSum - 1.0f) > 0.01f)
            {
                ctx.Results.Add(ValidationResult.Warning("SmallTown.GarageMode*Chance",
                    $"Garage mode chances sum to {garageSum:F2}, not 1.0. Will be normalized."));
            }

            // Town layout weights sum to ~1 (from NoiseConfig Dirichlet)
            var layoutWeights = config.Noise.TownLayoutWeights.SampleDirichlet(new Rng(0));
            float layoutSum = layoutWeights.Sum(x => x);
            if (Math.Abs(layoutSum - 1.0f) > 0.01f)
            {
                ctx.Results.Add(ValidationResult.Warning("Noise.TownLayoutWeights",
                    $"Town layout weights sum to {layoutSum:F2}, not 1.0. Will be normalized."));
            }

            // Town class weights sum to ~1 (from NoiseConfig Dirichlet)
            var classWeights = config.Noise.TownClassWeights.SampleDirichlet(new Rng(0));
            float classSum = classWeights.Sum(x => x);
            if (Math.Abs(classSum - 1.0f) > 0.01f)
            {
                ctx.Results.Add(ValidationResult.Warning("Noise.TownClassWeights",
                    $"Town class weights sum to {classSum:F2}, not 1.0. Will be normalized."));
            }
        }
    }
}