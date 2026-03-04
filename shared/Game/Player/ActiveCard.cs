using System;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class ActiveCard
    {
        public Guid Id { get; set; }
        public CardType Type { get; set; }
    }
}
