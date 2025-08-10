using Shared;

namespace Game.GamePlay;

public interface IMoves
{
    int Left { get; }

    void OnUsed();
    void Restore();
}

public class Moves : IMoves
{
    public Moves(ValueProperty<PlayerMovesState> state, int maxTurns)
    {
        _state = state;
        _maxTurns = maxTurns;
        
        state.Set(new PlayerMovesState()
        {
            Left = maxTurns,
            Max = maxTurns
        });
    }

    private readonly ValueProperty<PlayerMovesState> _state;
    private readonly int _maxTurns;

    public int Left => _state.Value.Left;

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
        });
    }
}