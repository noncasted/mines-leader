using Cluster.Configs;
using Internal;
using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.GamePlay;

public class LastManStandingTurnBasedRound : Service, IGameRound
{
    public LastManStandingTurnBasedRound(
        IGameContext gameContext,
        RoundReadiness readiness,
        RoundSnapshots snapshots,
        RoundTurn turn,
        RoundTurnEnd turnEnd,
        RoundOutcome outcome,
        RoundPlayers players,
        IGameModeConfig modeOptions,
        ILogger<LastManStandingTurnBasedRound> logger,
        ISessionLogger sessionLogger,
        IAgentObservationPublisher observationPublisher,
        MatchCreateOptions matchOptions) : base("game-round")
    {
        _gameContext = gameContext;
        _readiness = readiness;
        _snapshots = snapshots;
        _turn = turn;
        _turnEnd = turnEnd;
        _outcome = outcome;
        _players = players;
        _modeOptions = modeOptions;
        _logger = logger;
        _sessionLogger = sessionLogger;
        _observationPublisher = observationPublisher;
        _matchOptions = matchOptions;
    }

    private Guid _currentPlayerId;
    private int _currentRound;

    private readonly IGameContext _gameContext;
    private readonly RoundReadiness _readiness;
    private readonly RoundSnapshots _snapshots;
    private readonly RoundTurn _turn;
    private readonly RoundTurnEnd _turnEnd;
    private readonly RoundOutcome _outcome;
    private readonly RoundPlayers _players;
    private readonly IGameModeConfig _modeOptions;
    private readonly ILogger<LastManStandingTurnBasedRound> _logger;
    private readonly ISessionLogger _sessionLogger;
    private readonly IAgentObservationPublisher _observationPublisher;
    private readonly MatchCreateOptions _matchOptions;

    private LastManStandingTurnBasedModeOptions ModeOptions => _modeOptions.Value.LastManStandingTurnBased;

    public IViewableProperty<IPlayer> CurrentPlayer => _turn.CurrentPlayer;

    public async Task<Guid> Process(IReadOnlyLifetime lifetime)
    {
        _readiness.PrepareHands();
        _turn.ListenPlayers(lifetime);

        await _readiness.AwaitGame(lifetime);

        var players = _gameContext.Players;

        _snapshots.SendChecked("round:init", snapshot => {
            foreach (var player in _gameContext.Players)
            {
                PlayerBaseBuffs.Grant(
                    player,
                    snapshot,
                    health: ModeOptions.PlayerHealth,
                    moves: ModeOptions.PlayerMoves,
                    mana: ModeOptions.PlayerStartMana);
            }

            if (_matchOptions.Fixture != null)
                AgentMatchFixtureApplier.ApplyDecks(_gameContext, _matchOptions.Fixture, snapshot);

            foreach (var player in players)
            {
                if (AgentMatchFixtureApplier.ShouldSkipRestore(player, _matchOptions.Fixture))
                    continue;

                _players.RestoreCards(player, snapshot);
            }

            if (_matchOptions.Fixture != null)
                AgentMatchFixtureApplier.Apply(_gameContext, _matchOptions.Fixture, snapshot);

            snapshot.RecordGameStarted(ModeOptions.CardMovesCost);
            snapshot.RecordLastManStandingRound(_currentPlayerId, _currentRound, secondsLeft: 0);
        });

        await _readiness.AwaitPlayers(lifetime);

        var previousPlayer = ResolvePreviousPlayer(players);
        if (previousPlayer != null)
            _turn.SetCurrent(previousPlayer);

        var roundsCount = 0;

        while (_outcome.IsOver(lifetime, players, roundsCount) == false)
        {
            var nextPlayer = players.First(t => t != _turn.CurrentPlayer.Value);
            roundsCount++;
            _sessionLogger.LogRoundStart(nextPlayer.User.Id, roundsCount);
            await ProcessRound(lifetime, nextPlayer);
            _currentRound++;
            EmitRoundSnapshot();
            _sessionLogger.LogRoundEnd(nextPlayer.User.Id, roundsCount);
        }

        var winner = _outcome.GetWinner(players);
        _sessionLogger.LogGameOver(winner, _outcome.GetWinReason(lifetime, players));

        return winner;
    }

    public void SkipTurn()
    {
        _turn.Skip();
    }

    private async Task ProcessRound(IReadOnlyLifetime lifetime, IPlayer player)
    {
        var roundForcedLifetime = _turn.Begin(lifetime);

        _currentPlayerId = player.User.Id;

        _snapshots.SendChecked("round:start", startSnapshot => {
            player.Moves.Restore(startSnapshot);
            startSnapshot.RecordLastManStandingRound(_currentPlayerId, _currentRound, secondsLeft: 0);
        });

        _turn.SetCurrent(player);
        // opponent_turn must go out after Restore, otherwise EndTurn sees IsOwnTurn
        // with MovesLeft still locked at 0. turn_start follows for waiters.
        _observationPublisher.Publish(player.User.Id, "opponent_turn", false, string.Empty);
        _observationPublisher.Publish(player.User.Id, "turn_start", false, string.Empty);

        try
        {
            await _turn.TurnsCountdown(player, roundForcedLifetime);
        }
        catch (TaskCanceledException)
        {
            // Ignore
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in round timer");
        }

        _turnEnd.Send(player, ModeOptions.MaxManaCap);

        roundForcedLifetime.Terminate();
    }

    private void EmitRoundSnapshot()
    {
        _snapshots.Send(snapshot => snapshot.RecordLastManStandingRound(_currentPlayerId, _currentRound, secondsLeft: 0));
    }

    /// <summary>
    /// The turn loop plays <c>First(p != current)</c>, so this sets the previous player.
    /// Fixture-null and HumanGoesFirst keep the bot as previous (human acts first).
    /// HumanGoesFirst=false sets the human as previous (bot acts first).
    /// </summary>
    private IPlayer? ResolvePreviousPlayer(IReadOnlyList<IPlayer> players)
    {
        var botPlayer = players.FirstOrDefault(p => p.User.IsBot);
        var humanPlayer = players.FirstOrDefault(p => p.User.IsBot == false);

        if (_matchOptions.Fixture != null && _matchOptions.Fixture.HumanGoesFirst == false)
            return humanPlayer;

        return botPlayer;
    }
}
