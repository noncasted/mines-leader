using MemoryPack;

namespace Shared
{
    public partial class CardUsePayload
    {
        [MemoryPackable]
        public partial class Medic : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Purge : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Shield : ICardUsePayload
        {
            public CardType Type { get; set; }
        }
    }
}
