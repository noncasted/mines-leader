using System;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(CardUsePayload.ZipZap))]
    [MemoryPackUnion(1, typeof(CardUsePayload.Bloodhound))]
    [MemoryPackUnion(2, typeof(CardUsePayload.ErosionDozer))]
    [MemoryPackUnion(3, typeof(CardUsePayload.Gravedigger))]
    [MemoryPackUnion(4, typeof(CardUsePayload.Trebuchet))]
    [MemoryPackUnion(5, typeof(CardUsePayload.TrebuchetAimer))]
    [MemoryPackUnion(6, typeof(CardUsePayload.OpponentBomb))]
    [MemoryPackUnion(7, typeof(CardUsePayload.OpponentFlagErase))]
    [MemoryPackUnion(8, typeof(CardUsePayload.OpponentFlagReshuffle))]
    [MemoryPackUnion(9, typeof(CardUsePayload.Smoke))]
    [MemoryPackUnion(10, typeof(CardUsePayload.Medic))]
    [MemoryPackUnion(11, typeof(CardUsePayload.MinefieldScout))]
    [MemoryPackUnion(13, typeof(CardUsePayload.Siphon))]
    [MemoryPackUnion(16, typeof(CardUsePayload.ChainReaction))]
    [MemoryPackUnion(17, typeof(CardUsePayload.Overclock))]
    [MemoryPackUnion(18, typeof(CardUsePayload.FogOfWar))]
    [MemoryPackUnion(19, typeof(CardUsePayload.Scavenger))]
    [MemoryPackUnion(20, typeof(CardUsePayload.HandScramble))]
    [MemoryPackUnion(21, typeof(CardUsePayload.Lockdown))]
    [MemoryPackUnion(23, typeof(CardUsePayload.Sonar))]
    [MemoryPackUnion(24, typeof(CardUsePayload.Purge))]
    public partial interface ICardUsePayload
    {
        CardType Type { get; set; }
    }

    public interface IBoardCardUsePayload : ICardUsePayload
    {
        Position Position { get; set; }
    }

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
        public partial class Gravedigger : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Trebuchet : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class TrebuchetAimer : ICardUsePayload
        {
            public CardType Type { get; set; }
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
        public partial class Medic : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class MinefieldScout : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class Siphon : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class ChainReaction : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class Overclock : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class FogOfWar : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class Scavenger : ICardUsePayload
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
        public partial class Sonar : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class Purge : ICardUsePayload
        {
            public CardType Type { get; set; }
        }
    }
}