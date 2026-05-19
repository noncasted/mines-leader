using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class PlayerModifiersState
    {
        public List<DurationalModifierOverview> Overviews { get; set; } = new();
    }
}