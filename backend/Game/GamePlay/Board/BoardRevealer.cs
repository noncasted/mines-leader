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
        var passed = new HashSet<Position> { position };

        Check(position);

        foreach (var target in passed)
        {
            var cell = _board.Cells[target];
            cell.ToFree();
        }

        return;

        void Check(Position target)
        {
            var neighbours = _board.NeighbourPositions(target);

            neighbours.RemoveWhere(t => passed.Contains(t));
            neighbours.RemoveWhere(t => _board.Cells[t].Status == CellStatus.Free);

            foreach (var neighbour in neighbours)
            {
                var cell = _board.Cells[neighbour];

                if (cell.Status != CellStatus.Taken)
                    continue;

                var taken = cell.ToTaken();

                if (taken.HasMine == true)
                    return;
            }

            foreach (var neighbour in neighbours)
            {
                passed.Add(neighbour);
                Check(neighbour);
            }

            var board = PrintBoard();
            Console.WriteLine(board);
            return;

            string PrintBoard()
            {
                var result = string.Empty;

                for (var x = 0; x < _board.Size.x; x++)
                {
                    for (var y = 0; y < _board.Size.y; y++)
                    {
                        var check = new Position(x, y);
                        var cell = _board.Cells[check];

                        if (check == position)
                        {
                            result += "S";
                        }
                        else
                        {
                            result += cell.Status switch
                            {
                                CellStatus.Free => "F",
                                CellStatus.Taken => passed.Contains(check) == true ? "O" : cell.ToTaken().HasMine == true ? "M" : "T",
                                _ => "X"
                            };
                        }
                        
                        result += " ";
                    }

                    result += "\n";
                }

                return result;
            }
        }
    }
}