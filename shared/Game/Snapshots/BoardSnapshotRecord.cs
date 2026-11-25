using System;
using MemoryPack;

namespace Shared
{
    public partial class BoardSnapshotRecord
    {
        /// <summary>
        /// The opponent board is generated - until then it is empty and cards cannot be played
        /// on it. The client keeps a flag of availability of such cards by this record.
        /// </summary>
        [MemoryPackable]
        public partial class Generated : IBoardSnapshotRecord
        {
        }

        [MemoryPackable]
        public partial class CellTaken : IBoardSnapshotRecord
        {
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class CellFree : IBoardSnapshotRecord
        {
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class Flag : IBoardSnapshotRecord
        {
            public Position Position { get; set; }
            public bool IsFlagged { get; set; }
        }

        [MemoryPackable]
        public partial class MinesAround : IBoardSnapshotRecord
        {
            public Position Position { get; set; }
            public int Count { get; set; }
        }

        [MemoryPackable]
        public partial class Explosion : IBoardSnapshotRecord
        {
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class EffectAdded : IBoardSnapshotRecord
        {
            public Position Position { get; set; }
            public CellEffectType Type { get; set; }

            [MemoryPackAllowSerialize]
            public Guid EffectId { get; set; }
        }

        [MemoryPackable]
        public partial class EffectRemoved : IBoardSnapshotRecord
        {
            public Position Position { get; set; }

            [MemoryPackAllowSerialize]
            public Guid EffectId { get; set; }
        }
    }

    [MemoryPackable]
    [MemoryPackUnion(0, typeof(BoardSnapshotRecord.CellTaken))]
    [MemoryPackUnion(1, typeof(BoardSnapshotRecord.CellFree))]
    [MemoryPackUnion(2, typeof(BoardSnapshotRecord.Flag))]
    [MemoryPackUnion(3, typeof(BoardSnapshotRecord.MinesAround))]
    [MemoryPackUnion(4, typeof(BoardSnapshotRecord.Explosion))]
    [MemoryPackUnion(5, typeof(BoardSnapshotRecord.EffectAdded))]
    [MemoryPackUnion(6, typeof(BoardSnapshotRecord.EffectRemoved))]
    [MemoryPackUnion(7, typeof(BoardSnapshotRecord.Generated))]
    public partial interface IBoardSnapshotRecord
    {
    }
}