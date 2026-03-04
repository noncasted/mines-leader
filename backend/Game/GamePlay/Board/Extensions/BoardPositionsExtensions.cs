using Shared;

namespace Game.GamePlay;

public static class BoardPositionsExtensions
{
    public static readonly IReadOnlyList<Position> Directions = new List<Position>()
    {
        new(0, 1),
        new(1, 1),
        new(1, 0),
        new(1, -1),
        new(0, -1),
        new(-1, -1),
        new(-1, 0),
        new(-1, 1),
    };

    extension(IBoard board)
    {
        public HashSet<Position> NeighbourPositions(Position position)
        {
            var bounds = board.Size;

            var neighbours = new HashSet<Position>
            {
                new(position.x - 1, position.y),
                new(position.x + 1, position.y),
                new(position.x, position.y - 1),
                new(position.x, position.y + 1),
                new(position.x - 1, position.y - 1),
                new(position.x + 1, position.y + 1),
                new(position.x - 1, position.y + 1),
                new(position.x + 1, position.y - 1)
            };

            neighbours.RemoveWhere(neighbour => neighbour.x < 0 ||
                                                neighbour.x >= bounds.x ||
                                                neighbour.y < 0 ||
                                                neighbour.y >= bounds.y
            );

            return neighbours;
        }

        public void IterateNeighbours(Position position, Action<Position> action)
        {
            var neighbours = board.NeighbourPositions(position);

            foreach (var neighbour in neighbours)
            {
                if (board.Cells.TryGetValue(neighbour, out var cell) == false)
                    continue;

                action(neighbour);
            }
        }

        public Position RandomPosition()
        {
            var bounds = board.Size;
            return new Position(Random.Shared.Next(0, bounds.x), Random.Shared.Next(0, bounds.y));
        }

        public bool HasMinesAround(Position position)
        {
            var neighbours = board.NeighbourPositions(position);

            foreach (var neighbour in neighbours)
            {
                var cell = board.Cells[neighbour];

                if (cell.Status != CellStatus.Taken)
                    continue;

                var taken = cell.AsTaken();

                if (taken.HasMine == true)
                    return true;
            }

            return false;
        }
    }

    public static void CleanupAround(this IReadOnlyList<ICell> cells)
    {
        var board = cells.First().Source;

        var passed = new HashSet<Position>();

        foreach (var cell in cells)
            passed.Add(cell.Position);

        foreach (var cell in cells)
            Check(cell.Position);

        foreach (var target in passed)
        {
            var cell = board.Cells[target];
            cell.AsTaken().ToFree();
        }

        void Check(Position target)
        {
            var containsInvalidFreeCell = false;

            foreach (var direction in Directions)
            {
                var checkPosition = target + direction;

                if (board.Cells.TryGetValue(checkPosition, out var cell) == false)
                    continue;

                if (cell.Status != CellStatus.Taken)
                    continue;

                if (passed.Contains(checkPosition) == false)
                    continue;

                foreach (var checkFreeDirection in Directions)
                {
                    var checkFreePosition = checkPosition + checkFreeDirection;

                    if (board.Cells.TryGetValue(checkFreePosition, out var checkFreeCell) == false)
                        continue;

                    if (checkFreeCell.Status != CellStatus.Free)
                        continue;

                    var minesAroundCount = GetMinesAroundCount(checkFreePosition);

                    if (minesAroundCount == 0)
                    {
                        containsInvalidFreeCell = true;
                        break;
                    }
                }
            }

            if (containsInvalidFreeCell == false)
                return;

            passed.Add(target);

            foreach (var direction in Directions)
            {
                var checkPosition = target + direction;

                if (board.Cells.TryGetValue(checkPosition, out var cell) == false)
                    continue;

                if (cell.Status != CellStatus.Taken)
                    continue;

                Check(checkPosition);
            }
        }

        int GetMinesAroundCount(Position targetPosition)
        {
            var count = 0;

            foreach (var direction in Directions)
            {
                var checkPosition = targetPosition + direction;

                if (board.Cells.TryGetValue(checkPosition, out var cell) == false)
                    continue;

                if (cell.Status != CellStatus.Taken)
                    continue;

                var taken = cell.AsTaken();

                if (taken.HasMine == true)
                    count++;
            }

            return count;
        }
    }

    public static IReadOnlyList<ICell> GetClosedShape(this IBoard board, Position start)
    {
        if (start == new Position(-1, -1))
            return [];

        var cells = board.Cells;

        var selected = new HashSet<Position>();
        var checkedCache = new HashSet<Position>();

        Check(start);

        var result = new List<ICell>();

        foreach (var position in selected)
        {
            if (cells.TryGetValue(position, out var cell) == false)
                throw new KeyNotFoundException();

            result.Add(cell);
        }

        return result;

        bool Check(Position position)
        {
            if (cells.TryGetValue(position, out var cell) == false)
                return false;

            if (cell.Status == CellStatus.Free)
                return true;

            if (checkedCache.Contains(position) == true)
                return false;

            checkedCache.Add(position);

            foreach (var direction in Directions)
            {
                var next = position + direction;

                if (Check(next) == true)
                    selected.Add(position);
            }

            return false;
        }
    }
}