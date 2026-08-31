using Internal;
using Shared;

namespace Flow.Loop
{
    public static class UnionInitializer
    {
        public static void Execute()
        {
            var entityPayloads = new UnionBuilder<IEntityPayload>();
            var eventPayloads = new UnionBuilder<IEventPayload>();
            var contexts = new UnionBuilder<INetworkContext>();

            entityPayloads
                .Add<MenuPlayerPayload>()
                .Add<CardCreatePayload>()
                .Add<PlayerCreatePayload>();

            contexts
                .Add<EmptyResponse>()
                .AddSharedBackend()
                .AddSharedGame()
                .AddSharedSession();

            contexts.Build();
            eventPayloads.Build();
            entityPayloads.Build();
        }
    }
}