using System;
using MemoryPack;

namespace Shared
{
    public partial class CardUsePayload
    {
        [MemoryPackable]
        public partial class ZipZap : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
            public Guid CardId { get; set; }
        }

        [MemoryPackable]
        public partial class Bloodhound : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class ErosionDozer : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class MinefieldScout : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class Sonar : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class Excavator : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class ThermalVision : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class ChaosDiamond : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class ChaosScout : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class FortuneCookie : ICardUsePayload
        {
            public CardType Type { get; set; }
        }
    }
}
