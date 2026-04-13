using Common.Reactive;
using Shared;

namespace Game.GamePlay;

public interface IMoves
{
    int Left { get; }
    int Max { get; }
    bool IsAvailable { get; }

    IViewableDelegate Updated { get; }

    void SetCurrent(int value);
    void SetMax(int value);
    void OnUsed();
    void Restore();
    void Lock();
    void Refresh();
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

    private int Bonus => (int)_modifiers.Get(PlayerModifier.AdditionalMoves);

    private readonly ViewableDelegate _updated = new();

    public IViewableDelegate Updated => _updated;
    public int Left => _rawLeft + Bonus;
    public int Max => _maxTurns + Bonus;
    public bool IsAvailable => _isAvailable;

    public void SetCurrent(int value)
    {
        if (value < 0)
            value = 0;

        if (value > Max)
            value = Max;

        _rawLeft = value - Bonus;
        _updated.Invoke();
    }

    public void SetMax(int value)
    {
        _maxTurns = value;

        if (_rawLeft > _maxTurns)
            _rawLeft = _maxTurns;

        if (Left < 0)
            throw new InvalidOperationException("Turns cannot be less than zero.");

        _updated.Invoke();
    }

    public void OnUsed()
    {
        _rawLeft -= 1;

        if (Left < 0)
            throw new InvalidOperationException("Turns cannot be less than zero.");

        _updated.Invoke();
    }

    public void Restore()
    {
        _rawLeft = _maxTurns;
        _isAvailable = true;
        _updated.Invoke();
    }

    public void Lock()
    {
        _rawLeft = 0;
        _isAvailable = false;
        _updated.Invoke();
    }

    public void Refresh()
    {
        _updated.Invoke();
    }
}