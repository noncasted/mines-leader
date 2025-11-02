using Game.Session;
using Infrastructure;
using Meta.Matches;
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
        MatchCreateOptions matchOptions,
        IRematchAwaiter rematchAwaiter) : base("game-flow")
    {
        _orleans = orleans;
        _context = context;
        _users = users;
        _playerFactory = playerFactory;
        _sessionData = sessionData;
        _gameRound = gameRound;
        _matchOptions = matchOptions;
        _rematchAwaiter = rematchAwaiter;
        BindProperty(_state);
    }

    private readonly IOrleans _orleans;
    private readonly IGameContext _context;
    private readonly ISessionUsers _users;
    private readonly IPlayerFactory _playerFactory;
    private readonly ISessionData _sessionData;
    private readonly IGameRound _gameRound;
    private readonly MatchCreateOptions _matchOptions;
    private readonly IRematchAwaiter _rematchAwaiter;
    private readonly ValueProperty<GameFlowState> _state = new(1);

    public async Task<MatchTransitionResult> Process()
    {
        var match = _orleans.GetGrain<IMatch>(_sessionData.Id);
        await _orleans.InTransaction(() => match.Setup(_matchOptions.Type, _users.Select(t => t.Id).ToList()));

        foreach (var user in _users)
        {
            var player = await _playerFactory.Create(user);
            _context.AddPlayer(player);
        }

        var winner = await _gameRound.Process(_sessionData.Lifetime);
        _state.Update(state => state.Winner = winner);

        await _orleans.InTransaction(() => match.OnComplete(winner));

        var shouldRematch = await _rematchAwaiter.ShouldRematch(_sessionData.Lifetime, TimeSpan.FromSeconds(30));

        if (shouldRematch == true)
            return MatchTransitionResult.Rematch;

        return MatchTransitionResult.End;
    }
}