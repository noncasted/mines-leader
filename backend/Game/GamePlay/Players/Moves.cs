using Shared;

namespace Game.GamePlay;

public interface IMoves
{
    int Left { get; }

    void SetMax(int value);
    void OnUsed();
    void Restore();
    void Lock();
}

public class Moves : IMoves
{
    public Moves(ValueProperty<PlayerMovesState> state)
    {
        _state = state;
    }

    private readonly ValueProperty<PlayerMovesState> _state;
    
    private int _maxTurns;

    public int Left => _state.Value.Left;

    public void SetMax(int value)
    {
        _maxTurns = value;
    }

    public void OnUsed()
    {
        _state.Update(state =>
        {
            state.Left -= 1;
            state.Max = _maxTurns;
            
            if (state.Left < 0)
                throw new InvalidOperationException("Turns cannot be less than zero.");
        });
    }

    public void Restore()
    {
        _state.Update(state =>
        {
            state.Left = _maxTurns;
            state.Max = _maxTurns;
            state.IsAvailable = true;
        });
    }

    public void Lock()
    {
        _state.Update(state =>
        {
            state.Left = 0;
            state.IsAvailable = false;
        });
    }
}