using Common.Network;
using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Loop;
using GamePlay.Players;
using Internal;
using Menu.Social;
using Shared;

namespace Tools.MemoryPackTools
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