using Shared;

namespace Tests.Network;

// Same order as ProjectsSetupExtensions.AddMemoryPack: the INetworkContext union is registered at runtime.
public static class NetworkContextsSetup
{
    private static readonly Lazy<bool> Registered = new(() => {
        var contexts = new UnionBuilder<INetworkContext>();

        contexts
            .Add<EmptyResponse>()
            .AddSharedBackend()
            .AddSharedGame()
            .AddSharedSession();

        contexts.Build();
        return true;
    });

    public static void Register()
    {
        _ = Registered.Value;
    }
}
