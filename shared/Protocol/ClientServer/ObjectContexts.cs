using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    public partial class ObjectContexts
    {
        [MemoryPackable]
        public partial class SetProperty : INetworkContext
        {
            public int PropertyId { get; set; }
            public IReadOnlyList<byte> Value { get; set; }
        }

        [MemoryPackable]
        public partial class PropertyUpdate : INetworkContext
        {
            public int Id { get; set; }
            public IReadOnlyList<byte> Value { get; set; }
        }

        [MemoryPackable]
        public partial class Event : INetworkContext
        {
            public int EntityId { get; set; }
            public IReadOnlyList<byte> Value { get; set; }
        }
    }
}