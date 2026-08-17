using Internal;
using Menu.Social;
using Network;
using Shared;

namespace Flow
{
    public class UnionInitializer : EnvPreprocessor
    {
        public override void Execute()
        {
            var entityPayloads = new UnionBuilder<IEntityPayload>();
            var eventPayloads = new UnionBuilder<IEventPayload>();
            var contexts = new UnionBuilder<INetworkContext>();

            entityPayloads
                .Add<MenuPlayerPayload>()
                .Add<CardCreatePayload>()
                .Add<PlayerCreatePayload>();

            eventPayloads
                .Add<MenuChatMessagePayload>();

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