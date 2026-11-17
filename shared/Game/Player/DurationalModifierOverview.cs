using System;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class DurationalModifierOverview : IModifierOverview
    {
        public Guid SourceId { get; set; }
        public PlayerModifier Type { get; set; }
        public float Value { get; set; }
        public string Key { get; set; } = string.Empty;
        public int TurnsToEnd { get; set; }

        public override bool Equals(object obj)
        {
            return obj is DurationalModifierOverview other && SourceId == other.SourceId;
        }

        public override int GetHashCode()
        {
            return SourceId.GetHashCode();
        }
    }
}