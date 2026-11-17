using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    public partial class SharedSessionObject
    {
        [MemoryPackable]
        public partial class SetProperty : INetworkContext
        {
            public int ObjectId { get; set; }
            public int PropertyId { get; set; }
            public byte[] Value { get; set; }
        }

        // Every property the client wrote since the last sync, in one message (NetworkPropertiesCollector): the backend
        // handles each entry as a SetProperty of its own.
        [MemoryPackable]
        public partial class SetProperties : INetworkContext
        {
            public IReadOnlyList<SetProperty> Entries { get; set; }
        }

        [MemoryPackable]
        public partial class PropertyUpdate : INetworkContext
        {
            public int ObjectId { get; set; }
            public int PropertyId { get; set; }
            public byte[] Value { get; set; }
            public int Version { get; set; }
        }

        [MemoryPackable]
        public partial class Event : INetworkContext
        {
            public int ObjectId { get; set; }
            public byte[] Value { get; set; }
        }
    }
}
