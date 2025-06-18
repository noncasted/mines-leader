using System;
using Common.Network;
using MemoryPack;

namespace Menu.Social
{
    [MemoryPackable]
    public partial class MenuPlayerPayload : IEntityPayload
    {
        public Guid PlayerId { get; set; }
    }
}