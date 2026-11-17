#nullable enable annotations
using System;
using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial struct OpenedCell
    {
        public Position Position { get; set; }
        public int MinesAround { get; set; }
    }

    [MemoryPackable]
    [MemoryPackUnion(0, typeof(CardActionSnapshot.ZipZap))]
    [MemoryPackUnion(1, typeof(CardActionSnapshot.Bloodhound))]
    [MemoryPackUnion(2, typeof(CardActionSnapshot.ErosionDozer))]
    [MemoryPackUnion(3, typeof(CardActionSnapshot.Gravedigger))]
    [MemoryPackUnion(4, typeof(CardActionSnapshot.Trebuchet))]
    [MemoryPackUnion(5, typeof(CardActionSnapshot.TrebuchetAimer))]
    [MemoryPackUnion(6, typeof(CardActionSnapshot.OpponentBomb))]
    [MemoryPackUnion(7, typeof(CardActionSnapshot.OpponentFlagErase))]
    [MemoryPackUnion(8, typeof(CardActionSnapshot.OpponentFlagReshuffle))]
    [MemoryPackUnion(9, typeof(CardActionSnapshot.Smoke))]
    [MemoryPackUnion(10, typeof(CardActionSnapshot.Medic))]
    [MemoryPackUnion(11, typeof(CardActionSnapshot.MinefieldScout))]
    [MemoryPackUnion(13, typeof(CardActionSnapshot.Siphon))]
    [MemoryPackUnion(16, typeof(CardActionSnapshot.ChainReaction))]
    [MemoryPackUnion(17, typeof(CardActionSnapshot.Overclock))]
    [MemoryPackUnion(18, typeof(CardActionSnapshot.FogOfWar))]
    [MemoryPackUnion(19, typeof(CardActionSnapshot.Scavenger))]
    [MemoryPackUnion(20, typeof(CardActionSnapshot.HandScramble))]
    [MemoryPackUnion(21, typeof(CardActionSnapshot.Lockdown))]
    [MemoryPackUnion(23, typeof(CardActionSnapshot.Sonar))]
    [MemoryPackUnion(24, typeof(CardActionSnapshot.Purge))]
    [MemoryPackUnion(25, typeof(CardActionSnapshot.Adrenaline))]
    [MemoryPackUnion(26, typeof(CardActionSnapshot.ManaSurge))]
    [MemoryPackUnion(27, typeof(CardActionSnapshot.BloodPact))]
    [MemoryPackUnion(28, typeof(CardActionSnapshot.CoinToss))]
    [MemoryPackUnion(29, typeof(CardActionSnapshot.ManaFountain))]
    [MemoryPackUnion(30, typeof(CardActionSnapshot.Focus))]
    [MemoryPackUnion(31, typeof(CardActionSnapshot.Shield))]
    [MemoryPackUnion(32, typeof(CardActionSnapshot.PowerSurge))]
    [MemoryPackUnion(33, typeof(CardActionSnapshot.Embargo))]
    [MemoryPackUnion(34, typeof(CardActionSnapshot.Recycler))]
    [MemoryPackUnion(35, typeof(CardActionSnapshot.MysticDraw))]
    [MemoryPackUnion(36, typeof(CardActionSnapshot.DoubleOrNothing))]
    [MemoryPackUnion(37, typeof(CardActionSnapshot.GamblersRuin))]
    [MemoryPackUnion(38, typeof(CardActionSnapshot.Excavator))]
    [MemoryPackUnion(39, typeof(CardActionSnapshot.ThermalVision))]
    [MemoryPackUnion(40, typeof(CardActionSnapshot.ChaosDiamond))]
    [MemoryPackUnion(41, typeof(CardActionSnapshot.ChaosScout))]
    [MemoryPackUnion(42, typeof(CardActionSnapshot.MineCluster))]
    [MemoryPackUnion(43, typeof(CardActionSnapshot.CarpetBomb))]
    [MemoryPackUnion(44, typeof(CardActionSnapshot.FortuneBlast))]
    [MemoryPackUnion(45, typeof(CardActionSnapshot.ChaosFog))]
    [MemoryPackUnion(46, typeof(CardActionSnapshot.Frost))]
    [MemoryPackUnion(47, typeof(CardActionSnapshot.Blackout))]
    [MemoryPackUnion(48, typeof(CardActionSnapshot.FortuneCookie))]
    [MemoryPackUnion(49, typeof(CardActionSnapshot.Salvage))]
    [MemoryPackUnion(50, typeof(CardActionSnapshot.CardThief))]
    [MemoryPackUnion(51, typeof(CardActionSnapshot.SabotageDeck))]
    [MemoryPackUnion(52, typeof(CardActionSnapshot.Dud))]
    [MemoryPackUnion(53, typeof(CardActionSnapshot.SoulLink))]
    [MemoryPackUnion(54, typeof(CardActionSnapshot.MirrorMatch))]
    [MemoryPackUnion(55, typeof(CardActionSnapshot.DimensionRift))]
    public partial interface ICardActionData
    {
        Guid TargetPlayer { get; set; }
    }

    public partial class CardActionSnapshot
    {
        [MemoryPackable]
        public partial class ZipZap : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<OpenedCell> OpenedCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
        }

        [MemoryPackable]
        public partial class Bloodhound : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<OpenedCell> OpenedCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
            public IReadOnlyList<Position> ExplodedCells { get; set; }
        }

        [MemoryPackable]
        public partial class ErosionDozer : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<OpenedCell> OpenedCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
            public IReadOnlyList<Position> ExplodedCells { get; set; }
        }

        [MemoryPackable]
        public partial class Gravedigger : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Trebuchet : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<Position> TakenCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
        }

        [MemoryPackable]
        public partial class TrebuchetAimer : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class OpponentBomb : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<OpenedCell> OpenedCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
            public IReadOnlyList<Position> ExplodedCells { get; set; }
        }

        [MemoryPackable]
        public partial class OpponentFlagErase : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<Position> UnflaggedCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
        }

        [MemoryPackable]
        public partial class OpponentFlagReshuffle : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<Position> FlaggedCells { get; set; }
            public IReadOnlyList<Position> UnflaggedCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
        }

        [MemoryPackable]
        public partial class Smoke : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<Position> OpenedCells { get; set; }
            public Position[] AffectedCells { get; set; }
            public Guid EffectId { get; set; }
        }

        [MemoryPackable]
        public partial class Medic : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class MinefieldScout : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> RevealedCells { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<OpenedCell> OpenedCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
            public IReadOnlyList<Position> FlaggedCells { get; set; }
        }

        [MemoryPackable]
        public partial class Siphon : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class ChainReaction : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> SpawnedMines { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<Position> TakenCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
        }

        [MemoryPackable]
        public partial class Overclock : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class FogOfWar : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public Position[] AffectedCells { get; set; }
            public Guid EffectId { get; set; }
        }

        [MemoryPackable]
        public partial class Scavenger : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class HandScramble : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Lockdown : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Sonar : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> FlaggedCells { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
        }

        [MemoryPackable]
        public partial class Purge : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class ManaSurge : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Adrenaline : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class BloodPact : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class CoinToss : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public bool IsHeads { get; set; }
        }

        [MemoryPackable]
        public partial class ManaFountain : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public int RolledAmount { get; set; }
        }

        [MemoryPackable]
        public partial class Focus : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Shield : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class PowerSurge : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Embargo : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Recycler : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class MysticDraw : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public bool IsHeads { get; set; }
        }

        [MemoryPackable]
        public partial class DoubleOrNothing : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public bool IsHeads { get; set; }
            public int ResultMana { get; set; }
        }

        [MemoryPackable]
        public partial class GamblersRuin : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public bool IsHeads { get; set; }
        }

        [MemoryPackable]
        public partial class Excavator : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<OpenedCell> OpenedCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
            public IReadOnlyList<Position> FlaggedCells { get; set; }
        }

        [MemoryPackable]
        public partial class ThermalVision : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> HighlightedMines { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public Position[] AffectedCells { get; set; }
            public Guid EffectId { get; set; }
        }

        [MemoryPackable]
        public partial class ChaosDiamond : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public int ActualSize { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<OpenedCell> OpenedCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
            public IReadOnlyList<Position> FlaggedCells { get; set; }
        }

        [MemoryPackable]
        public partial class ChaosScout : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public int ActualLength { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<OpenedCell> OpenedCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
            public IReadOnlyList<Position> FlaggedCells { get; set; }
        }

        [MemoryPackable]
        public partial class MineCluster : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<Position> TakenCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
        }

        [MemoryPackable]
        public partial class CarpetBomb : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<Position> TakenCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
        }

        [MemoryPackable]
        public partial class FortuneBlast : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public int ActualSize { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<Position> TakenCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
        }

        [MemoryPackable]
        public partial class ChaosFog : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public int ActualSize { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public Position[] AffectedCells { get; set; }
            public Guid EffectId { get; set; }
        }

        [MemoryPackable]
        public partial class Frost : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> FrozenCells { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public Position[] AffectedCells { get; set; }
            public Guid EffectId { get; set; }
        }

        [MemoryPackable]
        public partial class Blackout : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> AffectedCells { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public Guid EffectId { get; set; }
        }

        [MemoryPackable]
        public partial class FortuneCookie : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> RevealedMines { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public Position[] AffectedCells { get; set; }
            public Guid EffectId { get; set; }
        }

        [MemoryPackable]
        public partial class Salvage : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<CardType> PeekedCards { get; set; }
            public int ChosenIndex { get; set; }
        }

        [MemoryPackable]
        public partial class CardThief : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public CardType StolenCard { get; set; }
        }

        [MemoryPackable]
        public partial class SabotageDeck : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class Dud : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class SoulLink : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
        }

        [MemoryPackable]
        public partial class MirrorMatch : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public CardType CopiedCard { get; set; }
            public ICardActionData? CopiedAction { get; set; }
        }

        [MemoryPackable]
        public partial class DimensionRift : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public Guid OwnerPlayer { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }

            public IReadOnlyList<Position> OwnerTakenCells { get; set; }
            public IReadOnlyList<Position> OwnerFlaggedCells { get; set; }
            public IReadOnlyList<Position> OwnerUnflaggedCells { get; set; }
            public IReadOnlyList<OpenedCell> OwnerOpenedCells { get; set; }
            public IReadOnlyList<OpenedCell> OwnerUpdatedFreeCells { get; set; }

            public IReadOnlyList<Position> TargetTakenCells { get; set; }
            public IReadOnlyList<Position> TargetFlaggedCells { get; set; }
            public IReadOnlyList<Position> TargetUnflaggedCells { get; set; }
            public IReadOnlyList<OpenedCell> TargetOpenedCells { get; set; }
            public IReadOnlyList<OpenedCell> TargetUpdatedFreeCells { get; set; }
        }
    }
}