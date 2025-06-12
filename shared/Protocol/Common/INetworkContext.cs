using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [MemoryPackUnion(1000, typeof(BackendUserContexts.ProfileProjection))]
    [MemoryPackUnion(1001, typeof(BackendUserContexts.ProgressionProjection))]
    [MemoryPackUnion(1002, typeof(BackendUserContexts.DeckProjection))]

    [MemoryPackUnion(2000, typeof(MatchmakingContexts.GameResult))]
    [MemoryPackUnion(2001, typeof(MatchmakingContexts.LobbyResult))]
    
    [MemoryPackUnion(3000, typeof(EntityContexts.CreateRequest))]
    [MemoryPackUnion(3001, typeof(EntityContexts.CreateResponse))]
    [MemoryPackUnion(3002, typeof(EntityContexts.CreateUpdate))]
    [MemoryPackUnion(3003, typeof(EntityContexts.Destroy))]
    [MemoryPackUnion(3004, typeof(EntityContexts.UpdatePropertyRequest))]
    [MemoryPackUnion(3005, typeof(EntityContexts.PropertyUpdate))]
    [MemoryPackUnion(3006, typeof(EntityContexts.DestroyUpdate))]
    
    [MemoryPackUnion(3007, typeof(EntityContexts.GetServiceRequest))]
    [MemoryPackUnion(3008, typeof(EntityContexts.GetServiceResponse))]
    
    [MemoryPackUnion(3009, typeof(EntityContexts.Event))]
    
    [MemoryPackUnion(4000, typeof(UserContexts.LocalUpdate))]
    [MemoryPackUnion(4001, typeof(UserContexts.RemoteUpdate))]
    [MemoryPackUnion(4002, typeof(UserContexts.RemoteDisconnect))]
    
    public partial interface INetworkContext
    {
    }
}