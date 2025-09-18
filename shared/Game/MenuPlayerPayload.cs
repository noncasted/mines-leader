using System;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class MenuPlayerPayload : IEntityPayload
    {
        public Guid PlayerId { get; set; }
    }
}