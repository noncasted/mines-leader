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

    [MemoryPackUnion(30, typeof(SharedSessionObject.SetProperty))]
    [MemoryPackUnion(31, typeof(SharedSessionObject.PropertyUpdate))]
    [MemoryPackUnion(32, typeof(SharedSessionObject.Event))]
    public partial class NetworkContextUnion
    {
    }
}
