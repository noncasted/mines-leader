using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class PlayerDeckState
    {
        public List<CardType> Queue { get; set; }
    }
}