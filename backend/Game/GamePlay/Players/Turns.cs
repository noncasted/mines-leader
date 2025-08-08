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
    }

    private readonly ValueProperty<PlayerMovesState> _state;
    private readonly int _maxTurns;

    public int Left => _state.Value.Left;

    public void OnUsed()
    {
        _state.Update(state =>
        {
            state.Left = state.Left - 1;
            
            if (state.Left < 0)
                throw new InvalidOperationException("Turns cannot be less than zero.");
        });
    }

    public void Restore()
    {
        _state.Update(state =>
        {
            state.Left = _maxTurns;
        });
    }
}