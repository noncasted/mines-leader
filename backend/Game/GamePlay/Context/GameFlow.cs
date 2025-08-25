using Backend.Matches;
using Common;
using Shared;

namespace Game.GamePlay;

public interface IGameFlow
{
    Task GameEnd(Guid winner);
}

public class GameFlow : Service, IGameFlow
{
    public GameFlow(IOrleans orleans, ISessionData sessionData) : base("game-flow")
    {
        _orleans = orleans;
        _sessionData = sessionData;
        BindProperty(_state);
    }

    private readonly IOrleans _orleans;
    private readonly ISessionData _sessionData;
    private readonly ValueProperty<GameFlowState> _state = new(1);

    public async Task GameEnd(Guid winner)
    {
        _state.Update(state => state.Winner = winner);

        var match = _orleans.GetGrain<IMatch>(_sessionData.Id);
        await _orleans.InTransaction(() => match.OnComplete(winner));
    }
}