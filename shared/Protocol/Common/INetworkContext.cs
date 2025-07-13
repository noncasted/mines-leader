using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [MemoryPackUnion(10, typeof(EmptyResponse))]
    [MemoryPackUnion(21, typeof(PingContext.Request))]
    [MemoryPackUnion(22, typeof(PingContext.Response))]
    
    [MemoryPackUnion(1000, typeof(BackendUserContexts.ProfileProjection))]
    [MemoryPackUnion(1001, typeof(BackendUserContexts.ProgressionProjection))]
    [MemoryPackUnion(1002, typeof(BackendUserContexts.DeckProjection))]
    [MemoryPackUnion(1003, typeof(BackendUserContexts.UpdateDeckRequest))]

    [MemoryPackUnion(2000, typeof(MatchmakingContexts.Search))]
    [MemoryPackUnion(2001, typeof(MatchmakingContexts.CancelSearch))]
    [MemoryPackUnion(2002, typeof(MatchmakingContexts.Create))]
    [MemoryPackUnion(2003, typeof(MatchmakingContexts.GameResult))]
    [MemoryPackUnion(2004, typeof(MatchmakingContexts.LobbyResult))]
    
    [MemoryPackUnion(3000, typeof(UserContexts.LocalUpdate))]
    [MemoryPackUnion(3001, typeof(UserContexts.RemoteUpdate))]
    [MemoryPackUnion(3002, typeof(UserContexts.RemoteDisconnect))]
    
    [MemoryPackUnion(4000, typeof(ObjectContexts.SetProperty))]
    [MemoryPackUnion(4001, typeof(ObjectContexts.PropertyUpdate))]
    [MemoryPackUnion(4002, typeof(ObjectContexts.Event))]
    
    [MemoryPackUnion(5000, typeof(EntityContexts.CreateRequest))]
    [MemoryPackUnion(5001, typeof(EntityContexts.CreateResponse))]
    [MemoryPackUnion(5002, typeof(EntityContexts.CreatedOverview))]
    [MemoryPackUnion(5003, typeof(EntityContexts.Destroy))]
    [MemoryPackUnion(5004, typeof(EntityContexts.DestroyUpdate))]
    
    [MemoryPackUnion(6000, typeof(ServiceContexts.GetRequest))]
    [MemoryPackUnion(6001, typeof(ServiceContexts.GetResponse))]
    [MemoryPackUnion(6002, typeof(ServiceContexts.Overview))]

    [MemoryPackUnion(7000, typeof(GameConnectionAuth.Request))]
    [MemoryPackUnion(7001, typeof(GameConnectionAuth.Response))]
    [MemoryPackUnion(7002, typeof(BackendConnectionAuth.Request))]
    [MemoryPackUnion(7003, typeof(BackendConnectionAuth.Response))]
    public partial interface INetworkContext
    {
    }
}