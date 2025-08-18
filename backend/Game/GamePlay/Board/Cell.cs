using Shared;

namespace Game.GamePlay;

public interface ICell
{
    IBoard Source { get; }
    CellStatus Status { get; }
    Position Position { get; }

    ITakenCell ToTaken();
    IFreeCell ToFree();
}

public enum CellStatus
{
    Free,
    Taken,
}

public interface IFreeCell : ICell
{
    int MinesAround { get; }

    void UpdateMinesAround(int minesCount);
}

public interface ITakenCell : ICell
{
    bool IsFlagged { get; }
    bool HasMine { get; }

    void SetFlag();
    void RemoveFlag();

    void Explode();
    void SetMine();
}

public class FreeCell : IFreeCell
{
    public FreeCell(Position position, IBoard board)
    {
        Position = position;
        MinesAround = 0;
        Source = board;
    }

    public IBoard Source { get; }
    public CellStatus Status => CellStatus.Free;
    public Position Position { get; }

    public int MinesAround { get; private set; }

    public void UpdateMinesAround(int minesCount)
    {
        MinesAround = minesCount;
        Source.Events.SetMinesAround(this, minesCount);
    }

    public ITakenCell ToTaken()
    {
        var taken = new TakenCell(Position, Source);
        Source.SetCell(taken);
        return taken;
    }

    public IFreeCell ToFree()
    {
        return this;
    }
}

public class TakenCell : ITakenCell
{
    public TakenCell(Position position, IBoard board)
    {
        Position = position;
        IsFlagged = false;
        HasMine = false;
        Source = board;
    }

    public IBoard Source { get; }
    public CellStatus Status => CellStatus.Taken;
    public Position Position { get; }
    public bool IsFlagged { get; private set; }
    public bool HasMine { get; private set; }

    public void SetFlag()
    {
        IsFlagged = true;
        Source.Events.SetFlag(this, true);
    }

    public void RemoveFlag()
    {
        IsFlagged = false;
        Source.Events.SetFlag(this, false);
    }

    public void Explode()
    {
        Source.Events.SetExplosion(this);
    }

    public void SetMine()
    {
        HasMine = true;
    }

    public ITakenCell ToTaken()
    {
        return this;
    }

    public IFreeCell ToFree()
    {
        var free = new FreeCell(Position, Source);
        Source.SetCell(free);
        return free;
    }
}