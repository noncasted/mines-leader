using Shared;

namespace Game.GamePlay;

public partial class MoveSnapshot
{
    public void RecordBoardGenerated(IBoard board)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.Generated());
    }

    public void RecordCellTaken(IBoard board, Position position)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.CellTaken { Position = position });
    }

    public void RecordCellFree(IBoard board, Position position)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.CellFree { Position = position });
    }

    public void RecordFlag(IBoard board, Position position, bool isFlagged)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.Flag { Position = position, IsFlagged = isFlagged });
    }

    public void RecordMines(IBoard board, Position position, int count)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.MinesAround { Position = position, Count = count });
    }

    public void RecordMines(IBoard board, IReadOnlyList<BoardSnapshotRecord.MinesAround> records)
    {
        foreach (var record in records)
            AppendBoardRecord(board, record);
    }

    public void RecordExplosion(IBoard board, Position position)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.Explosion { Position = position });
    }

    public void RecordEffectAdded(IBoard board, Position position, CellEffectType type, Guid effectId)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.EffectAdded
        {
            Position = position,
            Type = type,
            EffectId = effectId
        });
    }

    public void RecordEffectRemoved(IBoard board, Position position, Guid effectId)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.EffectRemoved
        {
            Position = position,
            EffectId = effectId
        });
    }

    /// <summary>
    /// Подорванные мины входят в общее число: их уже нет на поле, но флагом они не закрыты,
    /// поэтому счётчик оставшихся мин обязан их показывать.
    /// </summary>
    public void RecordBoardStateUpdate(IBoard board)
    {
        Append(new PlayerSnapshotRecord.BoardStateUpdate
        {
            PlayerId = board.OwnerId,
            Mines = board.MinesScanner.Mines + board.DetonatedMines,
            Flags = board.MinesScanner.Flags
        });
    }
}
