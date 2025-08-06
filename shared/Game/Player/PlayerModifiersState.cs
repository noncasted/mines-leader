using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class PlayerModifiersState
    {
        public IReadOnlyDictionary<PlayerModifier, float> Values { get; set; }
    }
}