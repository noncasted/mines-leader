using Shared;

namespace Common;

public static class SharedExtensions
{
    public static void AddSharedContexts()
    {
        var builder = new UnionBuilder<INetworkContext>();

        builder
            .AddSharedBackend()
            .AddSharedGame()
            .AddSharedSession();
        
        builder.Build();
    }
}