using Cluster.Configs;
using Game.Session;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace Game.GamePlay;

public class AgentObservationPublisher : IAgentObservationPublisher
{
    public AgentObservationPublisher(
        IGameContext gameContext,
        IServiceProvider serviceProvider,
        IObservationEventBuffer eventBuffer,
        MatchCreateOptions matchOptions,
        ICardConfigs cardConfigs,
        RoundPlayers roundPlayers)
    {
        _gameContext = gameContext;
        _serviceProvider = serviceProvider;
        _eventBuffer = eventBuffer;
        _matchOptions = matchOptions;
        _cardConfigs = cardConfigs;
        _roundPlayers = roundPlayers;
    }

    private readonly IGameContext _gameContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly IObservationEventBuffer _eventBuffer;
    private readonly MatchCreateOptions _matchOptions;
    private readonly ICardConfigs _cardConfigs;
    private readonly RoundPlayers _roundPlayers;
    private readonly Dictionary<Guid, int> _lastCursor = new();
    private int _sequence;

    public void Publish(Guid viewerId, string trigger, bool hasError, string error)
    {
        Publish(viewerId, trigger, hasError, error, oracle: false);
    }

    public void Publish(Guid viewerId, string trigger, bool hasError, string error, bool oracle)
    {
        if (_matchOptions.Type != GameMatchType.LastManStandingTurnBased)
            return;

        var user = FindUser(viewerId);
        if (user == null || user.IsBot)
            return;

        if (_lastCursor.TryGetValue(viewerId, out var cursor) == false)
            cursor = -1;

        var events = _eventBuffer.TakeAfter(cursor);
        _lastCursor[viewerId] = _eventBuffer.Cursor;

        var currentPlayerId = _serviceProvider.GetService<IGameRound>()?.CurrentPlayer.Value?.User.Id;

        var useOracle = false;
        if (oracle)
        {
            var modeConfig = _serviceProvider.GetService<IGameModeConfig>();
            useOracle = modeConfig?.Value?.LastManStandingTurnBased.IncludeOracle == true;
        }

        var observation = AgentObservationBuilder.Build(
            _gameContext,
            viewerId,
            events,
            trigger,
            useOracle,
            hasError,
            error,
            currentPlayerId,
            _cardConfigs.Value);

        if (observation.GameOver && observation.WinnerId == Guid.Empty)
        {
            var flagWinner = _roundPlayers.GetFlagWinner();
            if (flagWinner != Guid.Empty)
            {
                observation.WinnerId = flagWinner;
                if (string.IsNullOrEmpty(observation.WinReason))
                    observation.WinReason = "All opponent mines flagged";
            }
        }

        observation.Sequence = ++_sequence;
        observation.EventCursor = _eventBuffer.Cursor;
        user.Send(observation);
    }

    private IUser? FindUser(Guid viewerId)
    {
        foreach (var (user, _) in _gameContext.UserToPlayer)
        {
            if (user.Id == viewerId)
                return user;
        }

        return null;
    }
}
