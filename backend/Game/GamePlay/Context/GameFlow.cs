using Backend.Matches;
using Infrastructure.Orleans;
using Shared;

namespace Game.GamePlay;

public interface IGameFlow
{
    Task GameEnd(Guid winner);
}

public class GameFlow : Service, IGameFlow
{
    public GameFlow(
        IOrleans orleans,
        IGameContext context,
        ISessionData sessionData,
        IRematchAwaiter rematchAwaiter) : base("game-flow")
    {
        _orleans = orleans;
        _context = context;
        _sessionData = sessionData;
        _rematchAwaiter = rematchAwaiter;
        BindProperty(_state);
    }

    private readonly IOrleans _orleans;
    private readonly IGameContext _context;
    private readonly ISessionData _sessionData;
    private readonly IRematchAwaiter _rematchAwaiter;
    private readonly ValueProperty<GameFlowState> _state = new(1);

    public async Task GameEnd(Guid winner)
    {
        _state.Update(state => state.Winner = winner);

        var match = _orleans.GetGrain<IMatch>(_sessionData.Id);
        await _orleans.InTransaction(() => match.OnComplete(winner));
        
        var shouldRematch = await _rematchAwaiter.ShouldRematch(Lifetime, TimeSpan.FromSeconds(30));
        
        
    }
}