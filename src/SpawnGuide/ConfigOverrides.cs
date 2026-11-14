using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace SpawnGuide
{
    /// <summary>Strict, culture-independent CLI overrides; never repairs user input.</summary>
    public static class ConfigOverrides
    {
        public static void ApplyArguments(SpawnConfig config, string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != "--param") continue;
                if (++i == args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("--param requires path=value.");
                // Sensitivity mode also uses --param path to select its sweep axis.
                if (args.Contains("--analyze-sensitivity") && !args[i].Contains('=')) continue;
                Apply(config, args[i]);
            }
            config.Validate();
        }

        public static void Apply(SpawnConfig config, string expression)
        {
            int split = expression.IndexOf('=');
            if (split <= 0) throw new ArgumentException("--param requires path=value: " + expression);
            string path = expression.Substring(0, split).Trim();
            string value = expression.Substring(split + 1);
            try { Set(config, path.Split('.'), 0, value, path); }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException || ex is JsonException)
            { throw new ArgumentException("Invalid --param '" + path + "': " + ex.Message); }
        }

        static object Set(object owner, string[] parts, int index, string text, string path)
        {
            if (owner is IntDistribution integerDistribution &&
                (parts[index].Equals("Min", StringComparison.OrdinalIgnoreCase) || parts[index].Equals("Max", StringComparison.OrdinalIgnoreCase)))
            {
                bool maximum = parts[index].Equals("Max", StringComparison.OrdinalIgnoreCase);
                int bound = int.Parse(text, CultureInfo.InvariantCulture);
                string converted = ((double)bound + (maximum ? 1 : 0)).ToString(CultureInfo.InvariantCulture);
                return new IntDistribution((FloatDistribution)Set(integerDistribution.FloatDist, parts, index, converted, path));
            }
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            MemberInfo member = (MemberInfo?)owner.GetType().GetProperty(parts[index], flags)
                ?? owner.GetType().GetField(parts[index], flags)
                ?? throw new ArgumentException("Unknown setting path '" + path + "'.");
            Type type = member is PropertyInfo p ? p.PropertyType : ((FieldInfo)member).FieldType;
            object? old = member is PropertyInfo prop ? prop.GetValue(owner) : ((FieldInfo)member).GetValue(owner);
            object next;
            if (index + 1 < parts.Length)
            {
                if (old == null || type == typeof(string)) throw new ArgumentException("Cannot traverse '" + parts[index] + "'.");
                next = Set(old, parts, index + 1, text, path);
            }
            else
            {
                if (type == typeof(string)) next = text;
                else if (type.IsEnum)
                {
                    next = Enum.Parse(type, text, true);
                    if (!Enum.IsDefined(type, next)) throw new ArgumentException("Unknown " + type.Name + " value.");
                }
                else if (type.IsPrimitive || type == typeof(decimal)) next = Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
                else next = JsonSerializer.Deserialize(text, type, ConfigSerialization.JsonOptions)
                    ?? throw new ArgumentException("A setting cannot be null.");
                if (next is float f && (float.IsNaN(f) || float.IsInfinity(f)) || next is double d && (double.IsNaN(d) || double.IsInfinity(d)))
                    throw new ArgumentException("Value must be finite.");
                var range = member.GetCustomAttribute<RangeAttribute>();
                if (range != null && next is IConvertible && type != typeof(string) && !type.IsEnum)
                {
                    double number = Convert.ToDouble(next, CultureInfo.InvariantCulture);
                    if (number < range.Min || number > range.Max)
                        throw new ArgumentException($"Value must be in [{range.Min}, {range.Max}].");
                }
            }
            if (member is PropertyInfo property && property.CanWrite) property.SetValue(owner, next);
            else if (member is FieldInfo field && !field.IsInitOnly) field.SetValue(owner, next);
            else if (owner is FloatDistribution distribution)
            {
                object Read(string name) => string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase)
                    ? next : typeof(FloatDistribution).GetField(name)!.GetValue(distribution)!;
                return new FloatDistribution((DistributionShape)Read("Shape"), (float)Read("Min"), (float)Read("Max"),
                    (float)Read("Mean"), (float)Read("StdDev"), (float)Read("Mode"), (float)Read("Lambda"),
                    (float)Read("Alpha"), (float)Read("BetaParam"), (float)Read("Weight1"), (float)Read("Value1"),
                    (float)Read("Value2"), (float[])Read("Concentrations"));
            }
            else if (owner is FloatRange fr) return new FloatRange(member.Name == "Min" ? (float)next : fr.Min, member.Name == "Max" ? (float)next : fr.Max);
            else if (owner is IntRange ir) return new IntRange(member.Name == "Min" ? (int)next : ir.Min, member.Name == "Max" ? (int)next : ir.Max);
            else if (owner is IntDistribution && member.Name == "FloatDist") return new IntDistribution((FloatDistribution)next);
            else throw new ArgumentException("Read-only setting; override its containing range/distribution as JSON.");
            return owner;
        }
    }
}
