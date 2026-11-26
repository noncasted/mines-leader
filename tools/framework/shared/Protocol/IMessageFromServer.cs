using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(OneWayMessageFromServer))]
    [MemoryPackUnion(1, typeof(ResponseMessageFromServer))]
    [MemoryPackUnion(2, typeof(RequestMessageFromServer))]
    public partial interface IMessageFromServer
    {
    }
}