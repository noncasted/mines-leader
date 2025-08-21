using System;
using MemoryPack;

namespace Shared
{
    public partial class GameCheatContexts
    {
        [MemoryPackable]
        public partial class CardAdd : INetworkContext
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class CardRemove : INetworkContext
        {
            public int EntityId { get; set; }
            public CardType Type { get; set; }
        }
        
        [MemoryPackable]
        public partial class ChangeMana : INetworkContext
        {
            public int Value { get; set; }
        }
        
        [MemoryPackable]
        public partial class ChangeHealth : INetworkContext
        {
            public int Value { get; set; }
        }

        [MemoryPackable]
        public partial class ChangeMoves : INetworkContext
        {
            public int Value { get; set; }
        }
        
        [MemoryPackable]
        public partial class EndMatch : INetworkContext
        {
            public Guid Winner { get; set; }
        }

        public static IUnionBuilder<INetworkContext> Register(IUnionBuilder<INetworkContext> builder)
        {
            return builder
                .Add<CardAdd>()
                .Add<CardRemove>()
                .Add<ChangeMana>()
                .Add<ChangeHealth>()
                .Add<ChangeMoves>()
                .Add<EndMatch>();
        }
    }
}