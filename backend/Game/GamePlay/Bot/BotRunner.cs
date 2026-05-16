using Cluster.Configs;
using Common.Reactive;
using Game.GamePlay.Profiles;
using Game.Session;

namespace Game.GamePlay;

public interface IBotRunner
{
    Task Run(IUser user);
}

public class BotRunner : IBotRunner
{
    public BotRunner(
        IBotConfig config,
        IGameRound round,
        IBotContext botContext,
        IGameContext context,
        IBotProfileStrategyProvider profileProvider,
        ISessionLogger sessionLogger)
    {
        _config = config;
        _round = round;
        _botContext = botContext;
        _context = context;
        _profileProvider = profileProvider;
        _sessionLogger = sessionLogger;
    }

    private readonly IBotConfig _config;
    private readonly IGameRound _round;
    private readonly IGameContext _context;
    private readonly IBotContext _botContext;
    private readonly ISessionLogger _sessionLogger;
    private readonly IBotProfileStrategyProvider _profileProvider;

    public async Task Run(IUser user)
    {
        _context.GameStarted.Advise(user.Lifetime, () => {
            var bot = _context.Players.First(t => t.User.Id == user.Id);
            var opponent = _context.GetOpponent(bot);

            _botContext.Construct(bot, opponent);
        });

        _round.CurrentPlayer.ViewNotNull(user.Lifetime, (roundLifetime, player) => {
            if (player.User.Id != user.Id)
                return;

            // Skip false trigger during initialization (moves not yet restored)
            if (player.Moves.IsAvailable == false)
                return;

            var profile = _profileProvider.GetStrategy(_config.Value.CurrentProfile);
            Task.Run(() => profile.ExecuteTurn(roundLifetime));
        });
    }
}
