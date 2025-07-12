using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(OneWayMessageFromServer))]
    [MemoryPackUnion(1, typeof(ResponseMessageFromServer))]
    public partial interface IMessageFromServer
    {
    }

    [MemoryPackable]
    public partial class OneWayMessageFromServer : IMessageFromServer
    {
        public INetworkContext Context { get; set; }
    }

    [MemoryPackable]
    public partial class ResponseMessageFromServer : IMessageFromServer
    {
        public INetworkContext Context { get; set; }
        public int RequestId { get; set; }
    }
    
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(OneWayMessageFromClient))]
    [MemoryPackUnion(1, typeof(ResponsibleMessageFromClient))]
    public partial interface IMessageFromClient
    {
        
    }
    
    [MemoryPackable]
    public partial class OneWayMessageFromClient : IMessageFromClient
    {
        public INetworkContext Context { get; set; }
    }
    
    [MemoryPackable]
    public partial class ResponsibleMessageFromClient : IMessageFromClient
    {
        public INetworkContext Context { get; set; }
        public int RequestId { get; set; }
    }
}