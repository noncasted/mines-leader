using Common;
using Microsoft.Extensions.Options;
using Shared;

namespace Game.GamePlay;

public interface IBoard
{
    public Guid OwnerId { get; }
    
    IBoardGenerator Generator { get; }
    IBoardRevealer Revealer { get; }
    IBoardEvents Events { get; }
    
    IReadOnlyDictionary<Position, ICell> Cells { get; }
    IViewableDelegate Updated { get; }

    void SetCell(ICell cell);
}

public class Board : IBoard
{
    public Board(Guid ownerId, IOptions<BoardOptions> options)
    {
        OwnerId = ownerId;
        Events = new BoardEvents();
        Generator = new BoardGenerator(this, options);
        Revealer = new BoardRevealer(this);
    }

    private readonly Dictionary<Position, ICell> _cells = new();
    private readonly ViewableDelegate _updated = new();

    public Guid OwnerId { get; }
    
    public IBoardGenerator Generator { get; }
    public IBoardRevealer Revealer { get; }
    public IBoardEvents Events { get; }
    public IReadOnlyDictionary<Position, ICell> Cells => _cells;
    public IViewableDelegate Updated => _updated;

    public void SetCell(ICell cell)
    {
        Events.SetCell(cell);
        _cells[cell.Position] = cell;
    }
}