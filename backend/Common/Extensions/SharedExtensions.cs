using Shared;

namespace Common;

public static class SharedExtensions
{
    public static void AddSharedContexts()
    {
        var builder = new UnionBuilder<INetworkContext>();

        builder
            .Add<EmptyResponse>()
            .AddSharedBackend()
            .AddSharedGame()
            .AddSharedSession();
        
        var entityPayloads = new UnionBuilder<IEntityPayload>();

        entityPayloads
            .Add<MenuPlayerPayload>()
            .Add<CardCreatePayload>()
            .Add<PlayerCreatePayload>();
        
        builder.Build();
        entityPayloads.Build();
    }
}