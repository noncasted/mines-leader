using MemoryPack;

namespace Shared
{
    public partial class ObjectContexts
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
        }

        [MemoryPackable]
        public partial class Event : INetworkContext
        {
            public int ObjectId { get; set; }
            public byte[] Value { get; set; }
        }
    }
}