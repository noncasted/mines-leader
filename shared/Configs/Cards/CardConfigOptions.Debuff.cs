using MemoryPack;

namespace Shared
{
    public partial class CardConfigOptions
    {
        [MemoryPackable]
        public partial class Siphon : ICardConfig, IDrainAmountCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Debuff;
            public int ManaCost { get; set; } = 2;
            public int DrainAmount { get; set; } = 1;
            public CardTarget Target => CardTarget.Opponent;
        }

        [MemoryPackable]
        public partial class HandScramble : ICardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Debuff;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Opponent;
        }

        [MemoryPackable]
        public partial class Lockdown : ICardConfig, IDurationalCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Debuff;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Opponent;
            public int TurnsDuration { get; set; } = 2;
            public int MovesReduction { get; set; } = 1;
        }

        [MemoryPackable]
        public partial class Embargo : ICardConfig, ICostIncreaseCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Debuff;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Opponent;
            public int CostIncrease { get; set; } = 1;
        }

        [MemoryPackable]
        public partial class CardThief : ICardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Debuff;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Opponent;
        }

        [MemoryPackable]
        public partial class SabotageDeck : ICardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Debuff;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Opponent;
        }

        [MemoryPackable]
        public partial class Dud : ICardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Debuff;
            public int ManaCost { get; set; } = 99;
            public CardTarget Target => CardTarget.Self;
        }

        [MemoryPackable]
        public partial class SoulLink : ICardConfig, IDurationalCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Debuff;
            public int ManaCost { get; set; } = 3;
            public CardTarget Target => CardTarget.Opponent;
            public int TurnsDuration { get; set; } = 2;
        }
    }
}
