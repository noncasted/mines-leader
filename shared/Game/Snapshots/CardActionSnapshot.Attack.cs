#nullable enable annotations
using System;
using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    public partial class CardActionSnapshot
    {
        [MemoryPackable]
        public partial class Trebuchet : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<Position> TakenCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
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
        public partial class ChainReaction : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> SpawnedMines { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<Position> TakenCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
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
