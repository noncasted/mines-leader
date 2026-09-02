using Common.Reactive;
using Shared;

namespace Game.GamePlay;

public interface IMoves
{
    int Left { get; }
    int BaseMax { get; }
    int ResultMax { get; }
    bool IsAvailable { get; }

    IViewableDelegate Updated { get; }

    void SetCurrent(MoveSnapshot snapshot, int value);
    void SetMax(MoveSnapshot snapshot, int value);
    void OnUsed(MoveSnapshot snapshot, int cost = 1);
    void Restore(MoveSnapshot snapshot);
    void Lock(MoveSnapshot snapshot);
    void Refresh(MoveSnapshot snapshot);
}

public class Moves : IMoves
{
    public Moves(IModifiers modifiers)
    {
        _modifiers = modifiers;
    }

    private readonly IModifiers _modifiers;

    private int _maxTurns;
    private int _rawLeft;
    private bool _isAvailable;
    private IPlayer? _owner;

    private int Bonus => (int)_modifiers.Get(PlayerModifier.AdditionalMoves);

    /// <summary>
    /// Положительный модификатор докидывает ходы поверх остатка, отрицательный только
    /// урезает потолок: дебафф забирает запас, а не уже доступный ход. При максимуме 5
    /// и остатке 4 после -1 остаётся 4 из 4, а не 3 из 4.
    /// </summary>
    private int Gain => Bonus > 0 ? Bonus : 0;

    private readonly ViewableDelegate _updated = new();

    public IViewableDelegate Updated => _updated;
    public int Left => Math.Min(_rawLeft + Gain, ResultMax);
    public int BaseMax => _maxTurns;
    public int ResultMax => Math.Max(0, _maxTurns + Bonus);
    public bool IsAvailable => _isAvailable;

    public void BindOwner(IPlayer owner)
    {
        _owner = owner;
    }

    public void SetCurrent(MoveSnapshot snapshot, int value)
    {
        if (value < 0)
            value = 0;

        if (value > ResultMax)
            value = ResultMax;

        _rawLeft = value - Gain;

        if (_rawLeft > _maxTurns)
            _rawLeft = _maxTurns;

        _updated.Invoke();
        Record(snapshot);
    }

    public void SetMax(MoveSnapshot snapshot, int value)
    {
        _maxTurns = value;

        if (_rawLeft > _maxTurns)
            _rawLeft = _maxTurns;

        if (Left < 0)
            throw new InvalidOperationException("Turns cannot be less than zero.");

        _updated.Invoke();
        Record(snapshot);
    }

    public void OnUsed(MoveSnapshot snapshot, int cost = 1)
    {
        if (cost <= 0)
            return;

        _rawLeft -= cost;

        if (Left < 0)
            throw new InvalidOperationException("Turns cannot be less than zero.");

        _updated.Invoke();
        Record(snapshot);
    }

    public void Restore(MoveSnapshot snapshot)
    {
        _rawLeft = _maxTurns;
        _isAvailable = true;
        _updated.Invoke();
        Record(snapshot);
    }

    public void Lock(MoveSnapshot snapshot)
    {
        _rawLeft = 0;
        _isAvailable = false;
        _updated.Invoke();
        Record(snapshot);
    }

    public void Refresh(MoveSnapshot snapshot)
    {
        _updated.Invoke();
        Record(snapshot);
    }

    private void Record(MoveSnapshot snapshot)
    {
        if (_owner != null)
            snapshot.RecordMovesUpdate(_owner);
    }
}