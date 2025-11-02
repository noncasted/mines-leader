using Shared;

namespace Game.GamePlay;

public static class CellExtensions
{
    extension(ICell cell)
    {
        public bool IsTaken()
        {
            return cell.Status == CellStatus.Taken;
        }

        public bool IsFree()
        {
            return cell.Status == CellStatus.Free;
        }

        public ITakenCell AsTaken()
        {
            if (cell is not ITakenCell taken)
                throw new InvalidCastException($"Cell at position {cell.Position} is not a taken cell.");

            return taken;
        }

        public IFreeCell AsFree()
        {
            if (cell is not IFreeCell free)
                throw new InvalidCastException($"Cell at position {cell.Position} is not a free cell.");

            return free;
        }
    }

    extension(IPattenShape shape)
    {
        public IReadOnlyList<ITakenCell> SelectTaken(IBoard board, Position center)
        {
            return shape.Select(board, center, cell => cell.IsTaken()).Select(t => t.ToTaken()).ToList();
        }

        public IReadOnlyList<ICell> SelectFree(IBoard board, Position center)
        {
            return shape.Select(board, center, cell => cell.IsFree());
        }

        public IReadOnlyList<ICell> SelectAll(IBoard board, Position center)
        {
            return shape.Select(board, center, _ => true);
        }

        public IReadOnlyList<ICell> Select(
            IBoard board,
            Position center,
            Func<ICell, bool> filter)
        {
            if (center == new Position(-1, -1))
                return [];

            var size = shape.Positions.Count;
            var halfSize = size / 2;
            var start = center - new Position(halfSize, halfSize);

            var selected = new List<ICell>();

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (shape.Positions[y][x] == false)
                        continue;

                    var position = start + new Position(x, y);

                    if (board.Cells.TryGetValue(position, out var cell) == false)
                        continue;

                    if (filter(cell) == false)
                        continue;

                    selected.Add(board.Cells[position]);
                }
            }

            return selected;
        }
    }
}