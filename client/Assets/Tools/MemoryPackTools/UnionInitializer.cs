using Common.Network;
using Common.Network.Common;
using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Players;
using Global.Network.Initialization;
using MemoryPack;
using MemoryPack.Formatters;
using Menu;

namespace Tools.MemoryPackTools
{
    public class UnionInitializer : UnionInitializerBase
    {
        public override void Init()
        {
            var payloads = new DynamicUnionFormatter<IEntityPayload>(
                (0, typeof(TestEntityPayload)),
                (1, typeof(BoardCreatePayload)),
                (2, typeof(CardCreatePayload)),
                (3, typeof(GamePlayerCreatePayload)),
                
                // (3, typeof(GamePlayerCreatePayload)),
                (10000, typeof(MenuPlayerPayload)));
            
            var events = new DynamicUnionFormatter<IEventPayload>(
                (0, typeof(MenuChatMessagePayload)));


            MemoryPackFormatterProvider.Register(payloads);
            MemoryPackFormatterProvider.Register(events);
        }
    }
}