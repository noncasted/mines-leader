#nullable enable annotations
using System;
using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
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
        public partial class Sonar : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> FlaggedCells { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public IReadOnlyList<OpenedCell> UpdatedFreeCells { get; set; }
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
        public partial class FortuneCookie : ICardActionData
        {
            public Guid TargetPlayer { get; set; }
            public IReadOnlyList<Position> RevealedMines { get; set; }
            public IReadOnlyList<Position> TargetCells { get; set; }
            public Position[] AffectedCells { get; set; }
            public Guid EffectId { get; set; }
        }
    }
}
