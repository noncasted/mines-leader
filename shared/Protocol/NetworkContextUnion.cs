using MemoryPack;

namespace Shared
{
    // The union over INetworkContext: the framework types and the types of this game. The tags of the game types are
    // declared next to them, in partial declarations of this class (ranges: see NetworkContextTags of the framework).
    [MemoryPackUnionFormatter(typeof(INetworkContext))]
    [MemoryPackUnion(NetworkContextTags.EmptyResponse, typeof(EmptyResponse))]
    [MemoryPackUnion(NetworkContextTags.ErrorResponse, typeof(ErrorResponse))]
    public partial class NetworkContextUnion
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
        }
    }
}
