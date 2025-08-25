using MemoryPack;

namespace Shared
{
    public partial class BoardSnapshotRecord
    {
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
    }

    [MemoryPackable]
    [MemoryPackUnion(0, typeof(BoardSnapshotRecord.CellTaken))]
    [MemoryPackUnion(1, typeof(BoardSnapshotRecord.CellFree))]
    [MemoryPackUnion(2, typeof(BoardSnapshotRecord.Flag))]
    [MemoryPackUnion(3, typeof(BoardSnapshotRecord.MinesAround))]
    [MemoryPackUnion(4, typeof(BoardSnapshotRecord.Explosion))]
    public partial interface IBoardSnapshotRecord
    {
    }
}