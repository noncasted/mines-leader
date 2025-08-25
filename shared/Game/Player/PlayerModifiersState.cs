using System.Collections.Generic;
using System.Linq;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class PlayerModifiersState
    {
        public IReadOnlyDictionary<PlayerModifier, float> Values { get; set; } =
            PlayerModifierExtensions.All.ToDictionary(t => t, t => 0f);
    }
}