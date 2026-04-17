using Shared;

namespace Game.GamePlay;

public interface IBoardRevealer
{
    List<Position> Reveal(IReadOnlyList<Position> positions, MoveSnapshot? snapshot = null);
}

public class BoardRevealer : IBoardRevealer
{
    public BoardRevealer(IBoard board, IBoardMinesScanner scanner)
    {
        _board = board;
        _scanner = scanner;
    }

    private readonly IBoard _board;
    private readonly IBoardMinesScanner _scanner;

    public List<Position> Reveal(IReadOnlyList<Position> positions, MoveSnapshot? snapshot = null)
    {
        var opened = new HashSet<Position>();

        foreach (var position in positions)
        {
            if (_board.Cells.TryGetValue(position, out var cell) == false)
                continue;

            if (cell.Status != CellStatus.Taken)
                continue;

            if (cell.AsTaken().HasMine == true)
                continue;

            cell.ToFree();
            opened.Add(position);
        }

        RecalculateScanner(snapshot);

        ExpandWave(opened, snapshot);

        return opened.ToList();
    }

    private void ExpandWave(HashSet<Position> opened, MoveSnapshot? snapshot)
    {
        while (true)
        {
            var candidates = new List<ITakenCell>();

            foreach (var (position, cell) in _board.Cells)
            {
                if (cell.Status != CellStatus.Taken)
                    continue;

                var taken = cell.AsTaken();

                if (taken.HasMine == true)
                    continue;

                if (HasAdjacentEmptyFree(position) == false)
                    continue;

                candidates.Add(taken);
            }

            if (candidates.Count == 0)
                return;

            foreach (var taken in candidates)
            {
                taken.ToFree();
                opened.Add(taken.Position);
            }

            RecalculateScanner(snapshot);
        }
    }

    private void RecalculateScanner(MoveSnapshot? snapshot)
    {
        var changes = _scanner.Recalculate(snapshot);

        if (snapshot != null && changes.Count > 0)
            snapshot.RecordMines(_board, changes);
    }

    private bool HasAdjacentEmptyFree(Position position)
    {
        foreach (var direction in BoardPositionsExtensions.Directions)
        {
            var neighbourPosition = position + direction;

            if (_board.Cells.TryGetValue(neighbourPosition, out var cell) == false)
                continue;

            if (cell.Status != CellStatus.Free)
                continue;

            if (cell.AsFree().MinesAround == 0)
                return true;
        }

        return false;
    }
}
