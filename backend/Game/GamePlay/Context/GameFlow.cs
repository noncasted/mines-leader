using Shared;

namespace Game.GamePlay;

public interface IGameFlow
{
    void GameEnd(Guid winner);
}

public class GameFlow : Service, IGameFlow
{
    public GameFlow() : base("game-flow")
    {
        BindProperty(_state);
    }
    
    private readonly ValueProperty<GameFlowState> _state = new(1);

    public void GameEnd(Guid winner)
    {
        _state.Update(state => state.Winner = winner);
    }
}