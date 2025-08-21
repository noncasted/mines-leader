using Backend.Matches;
using Common;
using Infrastructure.Orleans;
using Shared;

namespace Game.GamePlay;

public interface IGameFlow
{
    Task<MatchTransitionResult> Process();
}

public class GameFlow : Service, IGameFlow
{
    public GameFlow(
        IOrleans orleans,
        IGameContext context,
        ISessionUsers users,
        IPlayerFactory playerFactory,
        ISessionData sessionData,
        IGameRound gameRound,
        IRematchAwaiter rematchAwaiter) : base("game-flow")
    {
        _orleans = orleans;
        _context = context;
        _users = users;
        _playerFactory = playerFactory;
        _sessionData = sessionData;
        _gameRound = gameRound;
        _rematchAwaiter = rematchAwaiter;
        BindProperty(_state);
    }

    private readonly IOrleans _orleans;
    private readonly IGameContext _context;
    private readonly ISessionUsers _users;
    private readonly IPlayerFactory _playerFactory;
    private readonly ISessionData _sessionData;
    private readonly IGameRound _gameRound;
    private readonly IRematchAwaiter _rematchAwaiter;
    private readonly ValueProperty<GameFlowState> _state = new(1);

    public async Task<MatchTransitionResult> Process()
    {
        foreach (var user in _users)
        {
            var player = _playerFactory.Create(user);
            _context.AddPlayer(player);
        }
        
        var winner = await _gameRound.Process(_sessionData.Lifetime);
        _state.Update(state => state.Winner = winner);

        var match = _orleans.GetGrain<IMatch>(_sessionData.Id);
        await _orleans.InTransaction(() => match.OnComplete(winner));

        var shouldRematch = await _rematchAwaiter.ShouldRematch(_sessionData.Lifetime, TimeSpan.FromSeconds(30));

        if (shouldRematch == true)
            return MatchTransitionResult.Rematch;

        return MatchTransitionResult.End;
    }
}