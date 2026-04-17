using Game.Session;
using Microsoft.Extensions.Options;
using Shared;

namespace Game.GamePlay;

public interface IBoard
{
    public Guid OwnerId { get; }

    IBoardGenerator Generator { get; }
    IBoardRevealer Revealer { get; }
    IBoardMinesScanner MinesScanner { get; }
    IBoardEvents Events { get; }
    Position Size { get; }

    IReadOnlyDictionary<Position, ICell> Cells { get; }

    void SetCell(ICell cell);
}

public class Board : IBoard
{
    public Board(Guid ownerId, IOptions<BoardOptions> options)
    {
        OwnerId = ownerId;
        Events = new BoardEvents();
        Generator = new BoardGenerator(this, options);
        MinesScanner = new BoardMinesScanner(this);
        Revealer = new BoardRevealer(this, MinesScanner);
        Size = new Position(options.Value.Size, options.Value.Size);
    }

    private readonly Dictionary<Position, ICell> _cells = new();

    public Guid OwnerId { get; }

    public IBoardGenerator Generator { get; }
    public IBoardRevealer Revealer { get; }
    public IBoardMinesScanner MinesScanner { get; }
    public IBoardEvents Events { get; }
    public Position Size { get; }
    public IReadOnlyDictionary<Position, ICell> Cells => _cells;

    public void SetCell(ICell cell)
    {
        _cells[cell.Position] = cell;
        Events.SetCell(cell);
    }
}
