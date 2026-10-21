using MemoryPack;

namespace Shared
{
    // The union over INetworkContext: the framework types and the types of this game. The tags of the game types are
    // declared next to them, in partial declarations of this class (ranges: see NetworkContextTags of the framework).
    [MemoryPackUnionFormatter(typeof(INetworkContext))]
    [MemoryPackUnion(NetworkContextTags.EmptyResponse, typeof(EmptyResponse))]
    [MemoryPackUnion(NetworkContextTags.ErrorResponse, typeof(ErrorResponse))]
    [MemoryPackUnion(NetworkContextTags.SessionAuthRequest, typeof(SharedSessionAuth.Request))]
    [MemoryPackUnion(NetworkContextTags.SessionAuthResponse, typeof(SharedSessionAuth.Response))]
    [MemoryPackUnion(NetworkContextTags.EntityCreateRequest, typeof(SharedSessionEntity.CreateRequest))]
    [MemoryPackUnion(NetworkContextTags.EntityCreateResponse, typeof(SharedSessionEntity.CreateResponse))]
    [MemoryPackUnion(NetworkContextTags.EntityCreatedOverview, typeof(SharedSessionEntity.CreatedOverview))]
    [MemoryPackUnion(NetworkContextTags.EntityDestroy, typeof(SharedSessionEntity.Destroy))]
    [MemoryPackUnion(NetworkContextTags.EntityDestroyUpdate, typeof(SharedSessionEntity.DestroyUpdate))]
    [MemoryPackUnion(NetworkContextTags.ObjectSetProperty, typeof(SharedSessionObject.SetProperty))]
    [MemoryPackUnion(NetworkContextTags.ObjectPropertyUpdate, typeof(SharedSessionObject.PropertyUpdate))]
    [MemoryPackUnion(NetworkContextTags.ObjectEvent, typeof(SharedSessionObject.Event))]
    [MemoryPackUnion(NetworkContextTags.ObjectSetProperties, typeof(SharedSessionObject.SetProperties))]
    [MemoryPackUnion(NetworkContextTags.PlayerLocalUpdate, typeof(SharedSessionPlayer.LocalUpdate))]
    [MemoryPackUnion(NetworkContextTags.PlayerRemoteUpdate, typeof(SharedSessionPlayer.RemoteUpdate))]
    [MemoryPackUnion(NetworkContextTags.PlayerRemoteDisconnect, typeof(SharedSessionPlayer.RemoteDisconnect))]
    [MemoryPackUnion(NetworkContextTags.PlayerRemoteAway, typeof(SharedSessionPlayer.RemoteAway))]
    [MemoryPackUnion(NetworkContextTags.PlayerLeave, typeof(SharedSessionPlayer.Leave))]
    [MemoryPackUnion(NetworkContextTags.ServiceGetRequest, typeof(SharedSessionService.GetRequest))]
    [MemoryPackUnion(NetworkContextTags.ServiceGetResponse, typeof(SharedSessionService.GetResponse))]
    [MemoryPackUnion(NetworkContextTags.ServiceOverview, typeof(SharedSessionService.Overview))]
    [MemoryPackUnion(NetworkContextTags.LatencyPing, typeof(SharedSessionLatency.Ping))]
    [MemoryPackUnion(NetworkContextTags.LatencyPong, typeof(SharedSessionLatency.Pong))]
    [MemoryPackUnion(NetworkContextTags.LatencyPeerProbe, typeof(SharedSessionLatency.PeerProbe))]
    [MemoryPackUnion(NetworkContextTags.LatencyPeerEcho, typeof(SharedSessionLatency.PeerEcho))]
    public partial class NetworkContextUnion
    {
    }

    // The union over IEntityPayload (the payloads of the network entities of this game): the tags are declared next to
    // the payload types.
    [MemoryPackUnionFormatter(typeof(IEntityPayload))]
    public partial class EntityPayloadUnion
    {
    }

    // The generated formatter must be registered before the first message. The backend gets it from the module
    // initializer of this assembly and from the explicit call at the start of every service; Unity has no module
    // initializers, the player registers it before the first scene, the editor on every domain reload.
    public static class NetworkProtocol
    {
#if UNITY_5_3_OR_NEWER
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        public static void Register()
        {
            NetworkContextUnionInitializer.RegisterFormatter();
            EntityPayloadUnionInitializer.RegisterFormatter();
        }
    }
}
