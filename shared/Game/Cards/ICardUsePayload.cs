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
    [MemoryPackUnion(25, typeof(CardUsePayload.Adrenaline))]
    [MemoryPackUnion(26, typeof(CardUsePayload.ManaSurge))]
    [MemoryPackUnion(27, typeof(CardUsePayload.BloodPact))]
    [MemoryPackUnion(28, typeof(CardUsePayload.CoinToss))]
    [MemoryPackUnion(29, typeof(CardUsePayload.ManaFountain))]
    [MemoryPackUnion(30, typeof(CardUsePayload.Focus))]
    [MemoryPackUnion(31, typeof(CardUsePayload.Shield))]
    [MemoryPackUnion(32, typeof(CardUsePayload.PowerSurge))]
    [MemoryPackUnion(33, typeof(CardUsePayload.Embargo))]
    [MemoryPackUnion(34, typeof(CardUsePayload.Recycler))]
    [MemoryPackUnion(35, typeof(CardUsePayload.MysticDraw))]
    [MemoryPackUnion(36, typeof(CardUsePayload.DoubleOrNothing))]
    [MemoryPackUnion(37, typeof(CardUsePayload.GamblersRuin))]
    [MemoryPackUnion(38, typeof(CardUsePayload.Excavator))]
    [MemoryPackUnion(39, typeof(CardUsePayload.ThermalVision))]
    [MemoryPackUnion(40, typeof(CardUsePayload.ChaosDiamond))]
    [MemoryPackUnion(41, typeof(CardUsePayload.ChaosScout))]
    [MemoryPackUnion(42, typeof(CardUsePayload.MineCluster))]
    [MemoryPackUnion(43, typeof(CardUsePayload.CarpetBomb))]
    [MemoryPackUnion(44, typeof(CardUsePayload.FortuneBlast))]
    [MemoryPackUnion(45, typeof(CardUsePayload.ChaosFog))]
    [MemoryPackUnion(46, typeof(CardUsePayload.Frost))]
    [MemoryPackUnion(47, typeof(CardUsePayload.Blackout))]
    [MemoryPackUnion(48, typeof(CardUsePayload.FortuneCookie))]
    [MemoryPackUnion(49, typeof(CardUsePayload.Salvage))]
    [MemoryPackUnion(50, typeof(CardUsePayload.CardThief))]
    [MemoryPackUnion(51, typeof(CardUsePayload.SabotageDeck))]
    [MemoryPackUnion(52, typeof(CardUsePayload.Dud))]
    [MemoryPackUnion(53, typeof(CardUsePayload.SoulLink))]
    [MemoryPackUnion(54, typeof(CardUsePayload.MirrorMatch))]
    [MemoryPackUnion(55, typeof(CardUsePayload.DimensionRift))]
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
        [MemoryPackable]
        public partial class ManaSurge : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Adrenaline : ICardUsePayload
        {
            public CardType Type { get; set; }
        }
        [MemoryPackable]
        public partial class BloodPact : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class CoinToss : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class ManaFountain : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Focus : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Shield : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class PowerSurge : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Embargo : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Recycler : ICardUsePayload
        {
            public CardType Type { get; set; }
            public Guid DiscardCardId { get; set; }
        }

        [MemoryPackable]
        public partial class MysticDraw : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class DoubleOrNothing : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class GamblersRuin : ICardUsePayload
        {
            public CardType Type { get; set; }
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
        public partial class FortuneCookie : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Salvage : ICardUsePayload
        {
            public CardType Type { get; set; }
            public int ChosenIndex { get; set; }
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

        [MemoryPackable]
        public partial class MirrorMatch : ICardUsePayload
        {
            public CardType Type { get; set; }
        }

        [MemoryPackable]
        public partial class DimensionRift : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }
    }
}