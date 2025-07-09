using Common.Network;
using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Loop;
using GamePlay.Players;
using Internal;
using MemoryPack;
using MemoryPack.Formatters;
using Menu.Social;

namespace Tools.MemoryPackTools
{
    public class UnionInitializer : EnvPreprocessor
    {
        public override void Execute()
        {
            var payloads = new DynamicUnionFormatter<IEntityPayload>(
                (1, typeof(BoardCreatePayload)),
                (2, typeof(CardCreatePayload)),
                (3, typeof(GamePlayerCreatePayload)),

                // (3, typeof(GamePlayerCreatePayload)),
                (10000, typeof(MenuPlayerPayload)));

            var events = new DynamicUnionFormatter<IEventPayload>(
                (0, typeof(MenuChatMessagePayload)),
                (1, typeof(GameFlowEvents.Lose)));


            MemoryPackFormatterProvider.Register(payloads);
            MemoryPackFormatterProvider.Register(events);
        }
    }
}