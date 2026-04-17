using Shared;

namespace Game.GamePlay;

public interface IBoardMinesScanner
{
    int Mines { get; }
    int Flags { get; }

    IReadOnlyList<BoardSnapshotRecord.MinesAround> Recalculate(MoveSnapshot? snapshot = null);
}

public class BoardMinesScanner : IBoardMinesScanner
{
    public BoardMinesScanner(IBoard board)
    {
        _board = board;
    }

    private readonly IBoard _board;

    public int Mines { get; private set; }
    public int Flags { get; private set; }

    public IReadOnlyList<BoardSnapshotRecord.MinesAround> Recalculate(MoveSnapshot? snapshot = null)
    {
        var cells = _board.Cells;
        List<BoardSnapshotRecord.MinesAround>? changed = null;

        foreach (var (position, cell) in cells)
        {
            if (cell.Status != CellStatus.Free)
                continue;

            var free = cell.AsFree();
            var newMines = GetAround(_board, position);

            if (free.MinesAround == newMines)
                continue;

            free.UpdateMinesAround(newMines);

            (changed ??= new List<BoardSnapshotRecord.MinesAround>())
                .Add(new BoardSnapshotRecord.MinesAround { Position = position, Count = newMines });
        }

        var newTotalMines = GetTotalMines();
        var newTotalFlags = GetTotalFlags();

        var stateChanged = newTotalMines != Mines || newTotalFlags != Flags;

        Mines = newTotalMines;
        Flags = newTotalFlags;

        if (stateChanged && snapshot != null)
            snapshot.RecordBoardStateUpdate(_board.OwnerId, Mines, Flags);

        return changed ??
               (IReadOnlyList<BoardSnapshotRecord.MinesAround>)Array.Empty<BoardSnapshotRecord.MinesAround>();

        int GetTotalMines()
        {
            var total = 0;

            foreach (var (_, cell) in cells)
            {
                if (cell.Status != CellStatus.Taken)
                    continue;

                if (cell.AsTaken().HasMine)
                    total++;
            }

            return total;
        }

        int GetTotalFlags()
        {
            var total = 0;

            foreach (var (_, cell) in cells)
            {
                if (cell.Status != CellStatus.Taken)
                    continue;

                if (cell.AsTaken().IsFlagged)
                    total++;
            }

            return total;
        }
    }

    private static int GetAround(IBoard board, Position position)
    {
        var count = 0;
        var neighbours = board.NeighbourPositions(position);

        foreach (var neighbour in neighbours)
        {
            var cell = board.Cells[neighbour];

            if (cell.Status != CellStatus.Taken)
                continue;

            if (cell.AsTaken().HasMine)
                count++;
        }

        return count;
    }
}