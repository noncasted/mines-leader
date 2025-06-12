using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [MemoryPackUnion(1000, typeof(BackendUserContexts.ProfileProjection))]
    [MemoryPackUnion(1001, typeof(BackendUserContexts.ProgressionProjection))]
    [MemoryPackUnion(1002, typeof(BackendUserContexts.DeckProjection))]

    [MemoryPackUnion(2000, typeof(MatchmakingContexts.GameResult))]
    [MemoryPackUnion(2001, typeof(MatchmakingContexts.LobbyResult))]
    
    [MemoryPackUnion(3000, typeof(UserContexts.LocalUpdate))]
    [MemoryPackUnion(3001, typeof(UserContexts.RemoteUpdate))]
    [MemoryPackUnion(3002, typeof(UserContexts.RemoteDisconnect))]
    
    [MemoryPackUnion(4000, typeof(ObjectContexts.SetProperty))]
    [MemoryPackUnion(4001, typeof(ObjectContexts.PropertyUpdate))]
    [MemoryPackUnion(4002, typeof(ObjectContexts.Event))]
    
    [MemoryPackUnion(5000, typeof(EntityContexts.CreateRequest))]
    [MemoryPackUnion(5001, typeof(EntityContexts.CreateResponse))]
    [MemoryPackUnion(5002, typeof(EntityContexts.Overview))]
    [MemoryPackUnion(5003, typeof(EntityContexts.Destroy))]
    [MemoryPackUnion(5006, typeof(EntityContexts.DestroyUpdate))]
    
    [MemoryPackUnion(6000, typeof(ServiceContexts.GetRequest))]
    [MemoryPackUnion(6001, typeof(ServiceContexts.GetResponse))]
    [MemoryPackUnion(6002, typeof(ServiceContexts.GetResponse))]
    public partial interface INetworkContext
    {
    }
}