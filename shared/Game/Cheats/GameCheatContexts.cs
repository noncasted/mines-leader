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
            public Guid CardId { get; set; }
        }

        [MemoryPackable]
        public partial class ChangeMana : INetworkContext
        {
            public int Value { get; set; }
        }

        [MemoryPackable]
        public partial class ChangeMaxMana : INetworkContext
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
        public partial class ChangeMaxHealth : INetworkContext
        {
            public int Value { get; set; }
        }

        [MemoryPackable]
        public partial class ChangeMaxMoves : INetworkContext
        {
            public int Value { get; set; }
        }

        [MemoryPackable]
        public partial class SetMaxMana : INetworkContext
        {
            public int Value { get; set; }
        }

        [MemoryPackable]
        public partial class SetMaxHealth : INetworkContext
        {
            public int Value { get; set; }
        }

        [MemoryPackable]
        public partial class SetMaxMoves : INetworkContext
        {
            public int Value { get; set; }
        }

        [MemoryPackable]
        public partial class RestoreMana : INetworkContext
        {
        }

        [MemoryPackable]
        public partial class RestoreHealth : INetworkContext
        {
        }

        [MemoryPackable]
        public partial class RestoreMoves : INetworkContext
        {
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
                   .Add<ChangeMaxMana>()
                   .Add<ChangeHealth>()
                   .Add<ChangeMaxHealth>()
                   .Add<ChangeMoves>()
                   .Add<ChangeMaxMoves>()
                   .Add<RestoreMana>()
                   .Add<RestoreHealth>()
                   .Add<SetMaxMana>()
                   .Add<SetMaxHealth>()
                   .Add<SetMaxMoves>()
                   .Add<RestoreMoves>()
                   .Add<EndMatch>();
        }
    }
}