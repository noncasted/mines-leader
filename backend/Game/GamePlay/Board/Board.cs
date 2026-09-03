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
    /// Доска существует только после первого хода владельца: до генерации клеток нет,
    /// и играть по ней карты нельзя.
    /// </summary>
    bool IsGenerated { get; }

    /// <summary>
    /// Мины, подорванные владельцем доски при открытии клетки. Клетка становится Free,
    /// мина с поля исчезает. На победу по флагам не влияет: важно только состояние
    /// оставшихся Taken-клеток. HP = 0 проверяется отдельно.
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