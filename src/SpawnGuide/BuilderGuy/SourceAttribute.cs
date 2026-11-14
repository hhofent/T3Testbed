using System;

namespace SpawnGuide
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class SourceAttribute : Attribute
    {
        public string Source { get; }
        public SourceAttribute(string source)
        {
            if (source != "CRG" && source != "Census" && source != "user" && source != "placeholder")
                throw new ArgumentException("Unknown setting source.", nameof(source));
            Source = source;
        }
    }
}
