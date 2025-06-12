using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(ServerEmptyRequest))]
    [MemoryPackUnion(1, typeof(ServerFullRequest))]
    public partial interface IServerRequest
    {
        
    }
}