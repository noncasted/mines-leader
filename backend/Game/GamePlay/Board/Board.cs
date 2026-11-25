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

    /// <summary>
    /// The board exists only after the owner's first move: there are no cells before generation,
    /// and cards cannot be played on it.
    /// </summary>
    bool IsGenerated { get; }

    /// <summary>
    /// Mines blown up by the board owner when opening a cell. The cell becomes Free,
    /// the mine disappears from the field. Does not affect the flag victory: only the state of
    /// the remaining Taken cells matters. HP = 0 is checked separately.
    /// </summary>
    int DetonatedMines { get; }

    IReadOnlyDictionary<Position, ICell> Cells { get; }

    void SetCell(ICell cell);

    void RegisterDetonatedMine();
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
    public int DetonatedMines { get; private set; }
    public bool IsGenerated => _cells.Count != 0;
    public IReadOnlyDictionary<Position, ICell> Cells => _cells;

    public void SetCell(ICell cell)
    {
        _cells[cell.Position] = cell;
        Events.SetCell(cell);
    }

    public void RegisterDetonatedMine()
    {
        DetonatedMines++;
    }
}