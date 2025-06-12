using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(ServerEmptyResponse))]
    [MemoryPackUnion(1, typeof(ServerFullResponse))]
    public partial interface IServerResponse
    {
        
    }
}