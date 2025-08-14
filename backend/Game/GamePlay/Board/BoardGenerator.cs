using Microsoft.Extensions.Options;
using Shared;

namespace Game.GamePlay;

public interface IBoardGenerator
{
    void Generate(Position from);
}

public class BoardGenerator : IBoardGenerator
{
    public BoardGenerator(IBoard board, IOptions<BoardOptions> options)
    {
        _board = board;
        _options = options;
    }

    private readonly IBoard _board;
    private readonly IOptions<BoardOptions> _options;

    public void Generate(Position from)
    {
        var ignored = _board.NeighbourPositions(from);
        ignored.Add(from);

        var minesSpawned = 0;
        var requiredMines = _options.Value.Mines;
        var size = _options.Value.Size;
        
        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                var position = new Position(x, y);
                _board.SetCell(new TakenCell(position, _board));
            }
        }
        
        while (minesSpawned < requiredMines)
        {
            var random = _board.RandomPosition();

            if (ignored.Contains(random))
                continue;

            _board.Cells[random].ToTaken().SetMine();
            ignored.Add(random);

            minesSpawned++;
        }
    }
}