using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    public static partial class EntityContexts
    {
        [MemoryPackable]
        public partial class CreateRequest : INetworkContext
        {
            public int Id { get; set; }
            public IReadOnlyList<byte[]> Properties { get; set; }
            public byte[] Payload { get; set; }
        }

        [MemoryPackable]
        public partial class CreateResponse : INetworkContext
        {
            public int EntityId { get; set; }
        }
    
        [MemoryPackable]
        public partial class CreateUpdate : INetworkContext
        {
            public int OwnerId { get; set; }
            public int EntityId { get; set; }
            public IReadOnlyList<byte[]> Properties { get; set; }
            public byte[] Payload { get; set; }
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
        
        [MemoryPackable]
        public partial class UpdatePropertyRequest : INetworkContext
        {
            public int EntityId { get; set; }
            public int PropertyId { get; set; }
            public byte[] Value { get; set; }
        }
    
        [MemoryPackable]
        public partial class PropertyUpdate : INetworkContext
        {
            public int EntityId { get; set; }
            public int PropertyId { get; set; }
            public byte[] Value { get; set; }
        }
        
        [MemoryPackable]
        public partial class Event : INetworkContext
        {
            public int EntityId { get; set; }
            public byte[] Value { get; set; }
        }
        
        [MemoryPackable]
        public partial class GetServiceRequest : INetworkContext
        {
            public string Key { get; set; }
            public IReadOnlyList<byte[]> Properties { get; set; }
            public byte[] Payload { get; set; }
        }

        [MemoryPackable]
        public partial class GetServiceResponse : INetworkContext
        {
            public int EntityId { get; set; }
            public IReadOnlyList<byte[]> Properties { get; set; }
        }
    }
}