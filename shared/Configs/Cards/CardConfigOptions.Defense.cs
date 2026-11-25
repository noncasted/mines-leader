using MemoryPack;

namespace Shared
{
    public partial class CardConfigOptions
    {
        [MemoryPackable]
        public partial class Medic : ICardConfig, IHealCardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Defense;
            public int ManaCost { get; set; } = 4;
            public int Heal { get; set; } = 1;
            public CardTarget Target => CardTarget.Self;
        }

        [MemoryPackable]
        public partial class Purge : ICardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Defense;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Self;
        }

        [MemoryPackable]
        public partial class Shield : ICardConfig
        {
            public CardType Type { get; set; }
            public CardGroup Group => CardGroup.Defense;
            public int ManaCost { get; set; } = 2;
            public CardTarget Target => CardTarget.Self;
        }
    }
}
