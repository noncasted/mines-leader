using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class PlayerHandState
    {
        public List<CardType> Entries { get; } = new();
    }
}