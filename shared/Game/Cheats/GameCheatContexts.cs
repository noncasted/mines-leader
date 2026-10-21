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

    [MemoryPackUnion(1025, typeof(GameCheatContexts.CardAdd))]
    [MemoryPackUnion(1026, typeof(GameCheatContexts.CardRemove))]
    [MemoryPackUnion(1027, typeof(GameCheatContexts.ChangeMana))]
    [MemoryPackUnion(1028, typeof(GameCheatContexts.ChangeMaxMana))]
    [MemoryPackUnion(1029, typeof(GameCheatContexts.ChangeHealth))]
    [MemoryPackUnion(1030, typeof(GameCheatContexts.ChangeMaxHealth))]
    [MemoryPackUnion(1031, typeof(GameCheatContexts.ChangeMoves))]
    [MemoryPackUnion(1032, typeof(GameCheatContexts.ChangeMaxMoves))]
    [MemoryPackUnion(1033, typeof(GameCheatContexts.RestoreMana))]
    [MemoryPackUnion(1034, typeof(GameCheatContexts.RestoreHealth))]
    [MemoryPackUnion(1035, typeof(GameCheatContexts.SetMaxMana))]
    [MemoryPackUnion(1036, typeof(GameCheatContexts.SetMaxHealth))]
    [MemoryPackUnion(1037, typeof(GameCheatContexts.SetMaxMoves))]
    [MemoryPackUnion(1038, typeof(GameCheatContexts.RestoreMoves))]
    [MemoryPackUnion(1039, typeof(GameCheatContexts.EndMatch))]
    public partial class NetworkContextUnion
    {
    }
}
