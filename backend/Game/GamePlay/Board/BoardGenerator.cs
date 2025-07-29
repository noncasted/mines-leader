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

        while (minesSpawned < requiredMines)
        {
            var random = _board.RandomPosition();

            if (ignored.Contains(random))
                continue;

            var taken = new TakenCell(random, _board);
            taken.SetMine();
            _board.SetCell(taken);

            minesSpawned++;
        }
    }
}