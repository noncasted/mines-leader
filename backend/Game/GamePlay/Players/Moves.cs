using Game.Session;
using Shared;

namespace Game.GamePlay;

public interface IMoves
{
    int Left { get; }
    int Max { get; }

    void SetCurrent(int value);
    void SetMax(int value);
    void OnUsed();
    void Restore();
    void Lock();
    void Refresh();
}

public class Moves : IMoves
{
    public Moves(ValueProperty<PlayerMovesState> state, IModifiers modifiers)
    {
        _state = state;
        _modifiers = modifiers;
    }

    private readonly ValueProperty<PlayerMovesState> _state;
    private readonly IModifiers _modifiers;

    private int _maxTurns;
    private int _rawLeft;
    private bool _isAvailable;

    private int Bonus => (int)_modifiers.Get(PlayerModifier.AdditionalMoves);

    public int Left => _rawLeft + Bonus;
    public int Max => _maxTurns + Bonus;

    public void SetCurrent(int value)
    {
        if (value < 0)
            value = 0;

        if (value > Max)
            value = Max;

        _rawLeft = value - Bonus;
        SyncState();
    }

    public void SetMax(int value)
    {
        _maxTurns = value;

        if (_rawLeft > _maxTurns)
            _rawLeft = _maxTurns;

        if (Left < 0)
            throw new InvalidOperationException("Turns cannot be less than zero.");

        SyncState();
    }

    public void OnUsed()
    {
        _rawLeft -= 1;

        if (Left < 0)
            throw new InvalidOperationException("Turns cannot be less than zero.");

        SyncState();
    }

    public void Restore()
    {
        _rawLeft = _maxTurns;
        _isAvailable = true;
        SyncState();
    }

    public void Lock()
    {
        _rawLeft = 0;
        _isAvailable = false;
        SyncState();
    }

    public void Refresh()
    {
        SyncState();
    }

    private void SyncState()
    {
        _state.Set(new PlayerMovesState
        {
            Left = Left,
            Max = Max,
            IsAvailable = _isAvailable
        });
    }
}
