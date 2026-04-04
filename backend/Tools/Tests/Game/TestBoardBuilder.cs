using Common.Reactive;
using Game.GamePlay;
using Game.Session;
using Microsoft.Extensions.Options;
using Shared;

namespace Tests.Game;

/// <summary>
/// No-op sender for ValueProperty in tests — no network sync needed.
/// </summary>
public class NoOpUpdateSender : IPropertyUpdateSender
{
    public static readonly NoOpUpdateSender Instance = new();

    public void Send(int objectId, IObjectProperty property)
    {
    }
}

public static class TestValuePropertyExtensions
{
    public static ValueProperty<T> ForTest<T>(this ValueProperty<T> property) where T : new()
    {
        property.Construct(NoOpUpdateSender.Instance, 0);
        return property;
    }
}

/// <summary>
/// Builds a game board with deterministic cell layout for testing.
///
/// Usage:
///   var board = new TestBoardBuilder(8)
///       .WithMinesAt((1,1), (2,3), (5,5))
///       .WithFreeAt((4,4))              // opened cells
///       .Build();
///
/// By default all cells are Taken (closed), no mines.
/// </summary>
public class TestBoardBuilder
{
    public TestBoardBuilder(int size = 8)
    {
        _size = size;
    }

    private readonly int _size;
    private readonly HashSet<Position> _mines = [];
    private readonly HashSet<Position> _freeCells = [];
    private readonly HashSet<Position> _flaggedCells = [];
    private Guid _ownerId = Guid.NewGuid();

    public TestBoardBuilder WithOwner(Guid ownerId)
    {
        _ownerId = ownerId;
        return this;
    }

    public TestBoardBuilder WithMinesAt(params (int x, int y)[] positions)
    {
        foreach (var (x, y) in positions)
            _mines.Add(new Position(x, y));
        return this;
    }

    public TestBoardBuilder WithFreeAt(params (int x, int y)[] positions)
    {
        foreach (var (x, y) in positions)
            _freeCells.Add(new Position(x, y));
        return this;
    }

    public TestBoardBuilder WithFlagAt(params (int x, int y)[] positions)
    {
        foreach (var (x, y) in positions)
            _flaggedCells.Add(new Position(x, y));
        return this;
    }

    public IBoard Build()
    {
        var options = Options.Create(new BoardOptions { Size = _size, Mines = _mines.Count });
        var state = new ValueProperty<BoardState>(0).ForTest();
        var board = new Board(state, _ownerId, options);

        // Create all cells as Taken first
        for (var x = 0; x < _size; x++)
        {
            for (var y = 0; y < _size; y++)
            {
                var pos = new Position(x, y);
                var cell = new TakenCell(pos, board);

                if (_mines.Contains(pos))
                    cell.SetMine();

                board.SetCell(cell);
            }
        }

        // Open free cells
        foreach (var pos in _freeCells)
        {
            if (board.Cells.TryGetValue(pos, out var cell) && cell is ITakenCell taken)
                taken.ToFree();
        }

        // Set flags
        foreach (var pos in _flaggedCells)
        {
            if (board.Cells.TryGetValue(pos, out var cell) && cell is ITakenCell taken)
                taken.SetFlag();
        }

        // Start mines scanner to calculate MinesAround
        var lifetime = new Lifetime();
        board.MinesScanner.Start(lifetime);
        board.OnUpdated();

        return board;
    }
}