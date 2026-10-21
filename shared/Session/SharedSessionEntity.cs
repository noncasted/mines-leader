using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    public static partial class SharedSessionEntity
    {
        [MemoryPackable]
        public partial class CreateRequest : INetworkContext
        {
            public int Id { get; set; }
            public IReadOnlyList<SharedSessionObject.PropertyUpdate> Properties { get; set; }
            public IEntityPayload Payload { get; set; }
        }

        [MemoryPackable]
        public partial class CreateResponse : INetworkContext
        {
            public int EntityId { get; set; }
        }

        [MemoryPackable]
        public partial class CreatedOverview : INetworkContext
        {
            public int OwnerId { get; set; }
            public int EntityId { get; set; }
            public IReadOnlyList<SharedSessionObject.PropertyUpdate> Properties { get; set; }
            public IEntityPayload Payload { get; set; }
        }

        [MemoryPackable]
        public partial class Destroy : INetworkContext
        {
            public int EntityId { get; set; }
        }

        [MemoryPackable]
        public partial class DestroyUpdate : INetworkContext
        {
            public int EntityId { get; set; }
        }
    }

    [MemoryPackUnion(20, typeof(SharedSessionEntity.CreateRequest))]
    [MemoryPackUnion(21, typeof(SharedSessionEntity.CreateResponse))]
    [MemoryPackUnion(22, typeof(SharedSessionEntity.CreatedOverview))]
    [MemoryPackUnion(23, typeof(SharedSessionEntity.Destroy))]
    [MemoryPackUnion(24, typeof(SharedSessionEntity.DestroyUpdate))]
    public partial class NetworkContextUnion
    {
    }
}
