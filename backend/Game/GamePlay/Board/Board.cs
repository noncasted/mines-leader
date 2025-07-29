using Common;
using Microsoft.Extensions.Options;
using Shared;

namespace Game.GamePlay;

public interface IBoard
{
    IBoardGenerator Generator { get; }
    IBoardRevealer Revealer { get; }
    
    IReadOnlyDictionary<Position, ICell> Cells { get; }
    IViewableDelegate Updated { get; }

    void SetCell(ICell cell);
}

public class Board : IBoard
{
    public Board(IOptions<BoardOptions> options)
    {
        _options = options;
        
        Generator = new BoardGenerator(this, options);
        Revealer = new BoardRevealer(this);
    }

    private readonly IOptions<BoardOptions> _options;
    private readonly Dictionary<Position, ICell> _cells = new();
    private readonly ViewableDelegate _updated = new();

    public IBoardGenerator Generator { get; }
    public IBoardRevealer Revealer { get; }
    public IReadOnlyDictionary<Position, ICell> Cells => _cells;
    public IViewableDelegate Updated => _updated;
    

    public void SetCell(ICell cell)
    {
        _cells[cell.Position] = cell;
    }
}