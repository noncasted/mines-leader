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
    }

    [MemoryPackUnion(35, typeof(GameCheatContexts.CardAdd))]
    [MemoryPackUnion(36, typeof(GameCheatContexts.CardRemove))]
    [MemoryPackUnion(37, typeof(GameCheatContexts.ChangeMana))]
    [MemoryPackUnion(38, typeof(GameCheatContexts.ChangeMaxMana))]
    [MemoryPackUnion(39, typeof(GameCheatContexts.ChangeHealth))]
    [MemoryPackUnion(40, typeof(GameCheatContexts.ChangeMaxHealth))]
    [MemoryPackUnion(41, typeof(GameCheatContexts.ChangeMoves))]
    [MemoryPackUnion(42, typeof(GameCheatContexts.ChangeMaxMoves))]
    [MemoryPackUnion(43, typeof(GameCheatContexts.RestoreMana))]
    [MemoryPackUnion(44, typeof(GameCheatContexts.RestoreHealth))]
    [MemoryPackUnion(45, typeof(GameCheatContexts.SetMaxMana))]
    [MemoryPackUnion(46, typeof(GameCheatContexts.SetMaxHealth))]
    [MemoryPackUnion(47, typeof(GameCheatContexts.SetMaxMoves))]
    [MemoryPackUnion(48, typeof(GameCheatContexts.RestoreMoves))]
    [MemoryPackUnion(49, typeof(GameCheatContexts.EndMatch))]
    public partial interface INetworkContext
    {
    }
}
