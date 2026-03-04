using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class PlayerHandState
    {
        public List<ActiveCard> Entries { get; } = new();
    }
}