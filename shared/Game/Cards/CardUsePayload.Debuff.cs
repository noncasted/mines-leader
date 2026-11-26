using MemoryPack;

namespace Shared
{
    public partial class CardUsePayload
    {
        [MemoryPackable]
        public partial class Siphon : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class HandScramble : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Lockdown : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Embargo : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class CardThief : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class SabotageDeck : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Dud : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class SoulLink : ICardUsePayload
        {
            public CardType Type { get; set; }
        }
    }
}
