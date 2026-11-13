using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// Operation type for conditional rule actions.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RuleOperation
    {
        Set,
        Multiply,
        Add,
        Clamp,
    }

    /// <summary>
    /// Comparison operator for conditional rule conditions.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RuleOperator
    {
        GreaterThanOrEqual,
        LessThanOrEqual,
        Equal,
        NotEqual,
        GreaterThan,
        LessThan,
        In,
        NotIn,
    }

    /// <summary>
    /// A conditional rule that modifies config parameters based on other parameter values.
    /// Format: If <parameter> <operator> <value> Then <targetParameter> <operation> <value>
    /// Example: If "SmallTown.Tier" >= 2 Then "SmallTown.CivicDensity" *= 1.5
    /// </summary>
    public sealed class ConditionalRule
    {
        /// <summary>Parameter path to check (e.g., "SmallTown.Tier", "World.TownLayout").</summary>
        public string IfParameter { get; set; } = "";

        /// <summary>Comparison operator.</summary>
        public RuleOperator Operator { get; set; } = RuleOperator.GreaterThanOrEqual;

        /// <summary>Value to compare against. For "in"/"notin", comma-separated list.</summary>
        public string Value { get; set; } = "";

        /// <summary>Target parameter path to modify.</summary>
        public string ThenParameter { get; set; } = "";

        /// <summary>Operation to apply.</summary>
        public RuleOperation ThenOperation { get; set; } = RuleOperation.Multiply;

        /// <summary>Value for the operation.</summary>
        public float ThenValue { get; set; } = 1f;

        /// <summary>Optional: minimum value for clamp operation.</summary>
        public float? ClampMin { get; set; }

        /// <summary>Optional: maximum value for clamp operation.</summary>
        public float? ClampMax { get; set; }

        public ConditionalRule() { }

        public ConditionalRule(string ifParam, RuleOperator op, string value, string thenParam, RuleOperation thenOp, float thenValue)
        {
            IfParameter = ifParam; Operator = op; Value = value;
            ThenParameter = thenParam; ThenOperation = thenOp; ThenValue = thenValue;
        }

        public override string ToString() =>
            $"If {IfParameter} {Operator} {Value} Then {ThenParameter} {ThenOperation} {ThenValue}";
    }

    /// <summary>
    /// Engine for evaluating and applying conditional rules to a configuration.
    /// </summary>
    public static class ConfigRuleEngine
    {
        /// <summary>
        /// Apply all rules to the configuration. Rules are evaluated in order.
        /// </summary>
        public static void ApplyRules(SpawnConfig config, IEnumerable<ConditionalRule> rules)
        {
            foreach (var rule in rules)
            {
                try { ApplyRule(config, rule); }
                catch (Exception ex)
                {
                    Console.WriteLine($"Rule failed: {rule} - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Apply a single rule to the configuration.
        /// </summary>
        public static void ApplyRule(SpawnConfig config, ConditionalRule rule)
        {
            if (string.IsNullOrEmpty(rule.IfParameter) || string.IsNullOrEmpty(rule.ThenParameter))
                return;

            object ifValue = GetValue(config, rule.IfParameter);
            if (ifValue == null) return;

            bool conditionMet = EvaluateCondition(ifValue, rule.Operator, rule.Value);
            if (!conditionMet) return;

            ApplyOperation(config, rule.ThenParameter, rule.ThenOperation, rule.ThenValue, rule.ClampMin, rule.ClampMax);
        }

        private static object GetValue(object root, string path)
        {
            string[] parts = path.Split('.');
            object current = root;

            foreach (string part in parts)
            {
                if (current == null) return null;

                var type = current.GetType();
                var prop = type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    current = prop.GetValue(current);
                    continue;
                }

                var field = type.GetField(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    current = field.GetValue(current);
                    continue;
                }

                // Try indexer for arrays/lists
                if (int.TryParse(part, out int index) && current is System.Collections.IList list)
                {
                    if (index >= 0 && index < list.Count) current = list[index];
                    else return null;
                    continue;
                }

                return null;
            }

            return current;
        }

        private static bool EvaluateCondition(object value, RuleOperator op, string target)
        {
            // Handle enums
            if (value is Enum)
            {
                string enumStr = value.ToString();
                var targets = target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return op switch
                {
                    RuleOperator.Equal => targets.Any(t => t.Equals(enumStr, StringComparison.OrdinalIgnoreCase)),
                    RuleOperator.NotEqual => !targets.Any(t => t.Equals(enumStr, StringComparison.OrdinalIgnoreCase)),
                    RuleOperator.In => targets.Any(t => t.Equals(enumStr, StringComparison.OrdinalIgnoreCase)),
                    RuleOperator.NotIn => !targets.Any(t => t.Equals(enumStr, StringComparison.OrdinalIgnoreCase)),
                    _ => false,
                };
            }

            // Handle numeric
            if (!TryConvertToFloat(value, out float fval)) return false;
            if (!TryConvertToFloat(target, out float tval)) return false;

            return op switch
            {
                RuleOperator.GreaterThanOrEqual => fval >= tval,
                RuleOperator.LessThanOrEqual => fval <= tval,
                RuleOperator.GreaterThan => fval > tval,
                RuleOperator.LessThan => fval < tval,
                RuleOperator.Equal => Math.Abs(fval - tval) < 0.0001f,
                RuleOperator.NotEqual => Math.Abs(fval - tval) >= 0.0001f,
                _ => false,
            };
        }

        private static bool TryConvertToFloat(object value, out float result)
        {
            result = 0;
            return value switch
            {
                float f => (result = f, true).Item2,
                double d => (result = (float)d, true).Item2,
                int i => (result = i, true).Item2,
                long l => (result = l, true).Item2,
                uint u => (result = u, true).Item2,
                short s => (result = s, true).Item2,
                byte b => (result = b, true).Item2,
                decimal dec => (result = (float)dec, true).Item2,
                string str => float.TryParse(str, out result),
                _ => false,
            };
        }

        private static void ApplyOperation(object root, string path, RuleOperation op, float value, float? clampMin, float? clampMax)
        {
            string[] parts = path.Split('.');
            object current = root;
            object parent = null;
            string lastPart = parts[^1];

            for (int i = 0; i < parts.Length - 1; i++)
            {
                parent = current;
                var type = current.GetType();
                var prop = type.GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null) { current = prop.GetValue(current); continue; }
                var field = type.GetField(parts[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field != null) { current = field.GetValue(current); continue; }
                return; // Path not found
            }

            if (current == null) return;

            var targetType = current.GetType();
            var targetProp = targetType.GetProperty(lastPart, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var targetField = targetType.GetField(lastPart, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (targetProp != null && targetProp.CanWrite)
            {
                float currentVal = GetFloatValue(targetProp.GetValue(current));
                float newVal = ComputeNewValue(currentVal, op, value, clampMin, clampMax);
                if (targetProp.PropertyType == typeof(FloatDistribution))
                    targetProp.SetValue(current, new FloatDistribution(DistributionShape.Uniform, newVal, newVal));
                else if (targetProp.PropertyType == typeof(IntDistribution))
                    targetProp.SetValue(current, new IntDistribution(new FloatDistribution(DistributionShape.Uniform, newVal, newVal)));
                else
                    targetProp.SetValue(current, Convert.ChangeType(newVal, targetProp.PropertyType));
            }
            else if (targetField != null)
            {
                float currentVal = GetFloatValue(targetField.GetValue(current));
                float newVal = ComputeNewValue(currentVal, op, value, clampMin, clampMax);
                if (targetField.FieldType == typeof(FloatDistribution))
                    targetField.SetValue(current, new FloatDistribution(DistributionShape.Uniform, newVal, newVal));
                else if (targetField.FieldType == typeof(IntDistribution))
                    targetField.SetValue(current, new IntDistribution(new FloatDistribution(DistributionShape.Uniform, newVal, newVal)));
                else
                    targetField.SetValue(current, Convert.ChangeType(newVal, targetField.FieldType));
            }
        }

        private static float ComputeNewValue(float current, RuleOperation op, float value, float? clampMin, float? clampMax)
        {
            float result = op switch
            {
                RuleOperation.Set => value,
                RuleOperation.Multiply => current * value,
                RuleOperation.Add => current + value,
                RuleOperation.Clamp => Math.Clamp(current, clampMin ?? float.MinValue, clampMax ?? float.MaxValue),
                _ => current,
            };

            if (op != RuleOperation.Clamp)
            {
                if (clampMin.HasValue) result = Math.Max(result, clampMin.Value);
                if (clampMax.HasValue) result = Math.Min(result, clampMax.Value);
            }

            return result;
        }

        private static float GetFloatValue(object value)
        {
            if (value == null) return 0f;
            if (value is FloatDistribution fd) return fd.Mean;
            if (value is IntDistribution id) return id.FloatDist.Mean;
            if (value is float fVal) return fVal;
            if (value is int i) return i;
            if (value is double d) return (float)d;
            if (value is long l) return (float)l;
            if (value is uint u) return (float)u;
            if (value is decimal dec) return (float)dec;
            if (value is string s && float.TryParse(s, out var fParsed)) return fParsed;
            return 0f;
        }

    public static class BuiltInRules
    {
        public static readonly ConditionalRule[] DefaultRules = new[]
        {
            // Higher tier towns get more civic density
            new ConditionalRule { IfParameter = "SmallTown.Tier", Operator = RuleOperator.GreaterThanOrEqual, Value = "2",
                ThenParameter = "SmallTown.CivicDensity", ThenOperation = RuleOperation.Multiply, ThenValue = 1.5f },

            // Straddling towns (Both layout) get more residential
            new ConditionalRule { IfParameter = "World.TownLayout", Operator = RuleOperator.Equal, Value = "Both",
                ThenParameter = "SmallTown.ResidentialDensity", ThenOperation = RuleOperation.Multiply, ThenValue = 1.2f },

            // Large rural lots -> fewer outbuildings
            new ConditionalRule { IfParameter = "Rural.LotWidth", Operator = RuleOperator.GreaterThan, Value = "250",
                ThenParameter = "Rural.OutbuildingDensity", ThenOperation = RuleOperation.Multiply, ThenValue = 0.8f },

            // High farm density -> smaller farmsteads
            new ConditionalRule { IfParameter = "World.FarmDensity", Operator = RuleOperator.GreaterThan, Value = "0.2",
                ThenParameter = "Rural.LotWidth", ThenOperation = RuleOperation.Multiply, ThenValue = 0.9f,
                ClampMin = 150, ClampMax = 350 },

            // Rail towns get industrial boost
            new ConditionalRule { IfParameter = "World.TownRailProximityMiles", Operator = RuleOperator.LessThanOrEqual, Value = "5",
                ThenParameter = "SmallTown.IndustrialDensity", ThenOperation = RuleOperation.Multiply, ThenValue = 1.3f },

            // High commercial density -> wider commercial band
            new ConditionalRule { IfParameter = "SmallTown.CommercialDensity", Operator = RuleOperator.GreaterThan, Value = "1.5",
                ThenParameter = "SmallTown.BandDepth", ThenOperation = RuleOperation.Multiply, ThenValue = 1.1f,
                ClampMax = 300 },
        };
}
    }
}
