using System;

namespace SpawnGuide
{
    /// <summary>
    /// Categorizes a configuration parameter by functional concern (cross-cutting).
    /// Parameters belong to ONE System (their containing class) but can have MULTIPLE Concerns.
    /// Concerns: Density, Placement, Visual, Noise, Distribution, Geometry, Infrastructure, Vegetation, Civic, Commercial, Residential, Industrial
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class CategoryAttribute : Attribute
    {
        public enum Concern
        {
            Density,
            Placement,
            Visual,
            Noise,
            Distribution,
            Geometry,
            Infrastructure,
            Vegetation,
            Civic,
            Commercial,
            Residential,
            Industrial,
            Historical,
            Geological,
        }

        public Concern Category { get; }

        public CategoryAttribute(Concern category) => Category = category;
    }

    /// <summary>
    /// Documents the design rationale for a parameter — why this default, what it represents, tuning guidance.
    /// These comments appear in the JSON output and parameter explorer tooltips.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class RationaleAttribute : Attribute
    {
        public string Text { get; }

        public RationaleAttribute(string text) => Text = text;
    }

    /// <summary>
    /// Specifies the expected distribution shape for a parameter that uses FloatRange/IntRange.
    /// Used by tooling to suggest appropriate DistributionShape when upgrading to FloatDistribution.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class DistributionAttribute : Attribute
    {
        public DistributionShape Shape { get; }
        public string Note { get; }

        public DistributionAttribute(DistributionShape shape, string note = "") => (Shape, Note) = (shape, note);
    }

    /// <summary>
    /// Marks a parameter as deprecated with a replacement suggestion.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class DeprecatedAttribute : Attribute
    {
        public string Replacement { get; }
        public string Reason { get; }

        public DeprecatedAttribute(string replacement, string reason = "") => (Replacement, Reason) = (replacement, reason);
    }

    /// <summary>
    /// Specifies the valid range for a parameter (for validation and UI sliders).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class RangeAttribute : Attribute
    {
        public float Min { get; }
        public float Max { get; }

        public RangeAttribute(float min, float max) => (Min, Max) = (min, max);
    }

    /// <summary>
    /// Units of measurement for a parameter (feet, miles, percent, count, etc.).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class UnitsAttribute : Attribute
    {
        public string Units { get; }

        public UnitsAttribute(string units) => Units = units;
    }
}