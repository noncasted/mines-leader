using MemoryPack;

namespace Shared
{
    public partial class CardUsePayload
    {
        [MemoryPackable]
        public partial class Trebuchet : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class OpponentBomb : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class OpponentFlagErase : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class OpponentFlagReshuffle : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class Smoke : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class ChainReaction : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class FogOfWar : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class MineCluster : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class CarpetBomb : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class FortuneBlast : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class ChaosFog : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class Frost : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class Blackout : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class DimensionRift : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }
    }
}
