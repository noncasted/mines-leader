using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(OneWayMessageFromClient))]
    [MemoryPackUnion(1, typeof(RequestMessageFromClient))]
    [MemoryPackUnion(2, typeof(ResponseMessageFromClient))]
    public partial interface IMessageFromClient
    {
    }
}
