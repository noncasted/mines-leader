using Shared;

namespace Game.GamePlay;

public interface IBoardRevealer
{
    void Reveal(Position position);
}

public class BoardRevealer : IBoardRevealer
{
    public BoardRevealer(IBoard board)
    {
        _board = board;
    }

    private readonly IBoard _board;

    public void Reveal(Position position)
    {
        _board.OnUpdated();

        var initialTargets = _board.NeighbourPositions(position)
            .Where(t => _board.Cells[t].Status != CellStatus.Free)
            .ToList();

        Check(initialTargets);

        return;

        void Check(IReadOnlyList<Position> targets)
        {
            var cleanupTargets = new List<Position>();

            foreach (var target in targets)
            {
                if (_board.Cells[target].Status != CellStatus.Taken)
                    continue;

                if (_board.Cells[target].ToTaken().HasMine == true)
                    continue;

                if (HasInvalidFreeCell(target) == true)
                    cleanupTargets.Add(target);
            }

            foreach (var cleanupTarget in cleanupTargets)
            {
                var targetCell = _board.Cells[cleanupTarget];
                targetCell.AsTaken().ToFree();
            }

            _board.OnUpdated();

            var newTargets = new List<Position>();

            foreach (var cleanupTargetPosition in cleanupTargets)
            {
                foreach (var direction in BoardPositionsExtensions.Directions)
                {
                    var newTargetPosition = cleanupTargetPosition + direction;

                    if (_board.Cells.TryGetValue(newTargetPosition, out var cell) == false)
                        continue;

                    if (cell.Status != CellStatus.Taken)
                        continue;

                    if (newTargets.Contains(newTargetPosition) == true)
                        continue;

                    newTargets.Add(newTargetPosition);
                }
            }

            if (newTargets.Count > 0)
                Check(newTargets);
        }

        bool HasInvalidFreeCell(Position target)
        {
            foreach (var direction in BoardPositionsExtensions.Directions)
            {
                var checkPosition = target + direction;

                if (_board.Cells.TryGetValue(checkPosition, out var cell) == false)
                    continue;

                if (cell.Status != CellStatus.Free)
                    continue;

                if (cell.AsFree().MinesAround == 0)
                    return true;
            }

            return false;
        }
    }
}