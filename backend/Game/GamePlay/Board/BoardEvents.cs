using Common;
using Shared;

namespace Game.GamePlay;

public interface IBoardEvents
{
    IViewableDelegate<ICell> CellSet { get; }
    IViewableDelegate<ICell, bool> Flag { get; }
    IViewableDelegate<ICell, int> Mines { get; }
    IViewableDelegate<IBoardSnapshotRecord> Record { get; }

    void SetCell(ICell cell);
    void SetFlag(ICell cell, bool isFlagged);
    void SetMinesAround(ICell cell, int minesCount);

    void ForceRecord(IBoardSnapshotRecord record);

    void Lock();
    void Unlock();
}

public class BoardEvents : IBoardEvents
{
    private readonly ViewableDelegate<ICell> _cellSet = new();
    private readonly ViewableDelegate<ICell, bool> _flag = new();
    private readonly ViewableDelegate<ICell, int> _mines = new();
    private readonly ViewableDelegate<IBoardSnapshotRecord> _record = new();

    private bool _isLocked;
    
    public IViewableDelegate<ICell> CellSet => _cellSet;
    public IViewableDelegate<ICell, bool> Flag => _flag;
    public IViewableDelegate<ICell, int> Mines => _mines;
    public IViewableDelegate<IBoardSnapshotRecord> Record => _record;

    public void SetCell(ICell cell)
    {
        if (_isLocked == true)
            return;
        
        _cellSet.Invoke(cell);
    }

    public void SetFlag(ICell cell, bool isFlagged)
    {
        if (_isLocked == true)
            return;

        _flag.Invoke(cell, isFlagged);
    }

    public void SetMinesAround(ICell cell, int minesCount)
    {
        if (_isLocked == true)
            return;
        
        _mines.Invoke(cell, minesCount);
    }

    public void ForceRecord(IBoardSnapshotRecord record)
    {
        _record.Invoke(record);
    }

    public void Lock()
    {
        _isLocked = true;
    }

    public void Unlock()
    {
        _isLocked = false;
    }
}