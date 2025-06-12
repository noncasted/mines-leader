using System;
using Common.Network;
using MemoryPack;

namespace Menu
{
    [MemoryPackable]
    public partial class MenuPlayerPayload : IEntityPayload
    {
        public Guid PlayerId { get; set; }
    }
}