using Common;
using Shared;

namespace Game.GamePlay;

public interface IBoardMinesScanner
{
    void Start(IReadOnlyLifetime lifetime);
}

public class BoardMinesScanner : IBoardMinesScanner
{
    public BoardMinesScanner(IBoard board, ValueProperty<BoardState> state)
    {
        _board = board;
        _state = state;
    }

    private readonly IBoard _board;
    private readonly ValueProperty<BoardState> _state;

    public void Start(IReadOnlyLifetime lifetime)
    {
        _board.Updated.Advise(lifetime, Recalculate);
    }

    private void Recalculate()
    {
        var target = new Dictionary<Position, int>();
        var cells = _board.Cells;

        foreach (var (position, _) in cells)
            target[position] = GetAround(_board, position);

        foreach (var (_, cell) in cells)
        {
            if (cell.Status != CellStatus.Free)
                continue;

            var freeState = cell.ToFree();

            if (freeState.MinesAround != target[cell.Position])
                freeState.UpdateMinesAround(target[cell.Position]);
        }

        _state.Update(state =>
            {
                state.Mines = GetTotalMines();
                state.Flags = GetTotalFlags();
            }
        );
        
        return;

        int GetTotalMines()
        {
            var total = 0;

            foreach (var (_, cell) in cells)
            {
                if (cell.Status != CellStatus.Taken)
                    continue;

                var takenState = cell.ToTaken();

                if (takenState.HasMine)
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

                var takenState = cell.ToTaken();

                if (takenState.IsFlagged)
                    total++;
            }

            return total;
        }
    }

    private int GetAround(IBoard board, Position position)
    {
        var count = 0;

        var neighbours = board.NeighbourPositions(position);

        foreach (var neighbour in neighbours)
        {
            var cell = board.Cells[neighbour];
            var state = cell;

            if (state.Status != CellStatus.Taken)
                continue;

            var takenState = cell.ToTaken();

            if (takenState.HasMine)
                count++;
        }

        return count;
    }
}