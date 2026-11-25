using Cluster.Configs;
using Internal;
using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.GamePlay;

public class LastManStandingRound : Service, IGameRound
{
    public LastManStandingRound(
        IGameContext gameContext,
        RoundReadiness readiness,
        RoundSnapshots snapshots,
        RoundTurn turn,
        RoundTurnEnd turnEnd,
        RoundOutcome outcome,
        RoundPlayers players,
        IGameModeConfig modeOptions,
        ILogger<TimeLimitedRound> logger,
        ISessionLogger sessionLogger) : base("game-round")
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
    }

    private Guid _currentPlayerId;
    private int _currentRound;
    private int _secondsLeft;

    private readonly IGameContext _gameContext;
    private readonly RoundReadiness _readiness;
    private readonly RoundSnapshots _snapshots;
    private readonly RoundTurn _turn;
    private readonly RoundTurnEnd _turnEnd;
    private readonly RoundOutcome _outcome;
    private readonly RoundPlayers _players;
    private readonly IGameModeConfig _modeOptions;
    private readonly ILogger<TimeLimitedRound> _logger;
    private readonly ISessionLogger _sessionLogger;

    private LastManStandingModeOptions ModeOptions => _modeOptions.Value.LastManStanding;

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

            foreach (var player in players)
                _players.RestoreCards(player, snapshot);

            snapshot.RecordGameStarted(ModeOptions.CardMovesCost);
            snapshot.RecordLastManStandingRound(_currentPlayerId, _currentRound, _secondsLeft);
        });

        await _readiness.AwaitPlayers(lifetime);

        var botPlayer = players.FirstOrDefault(p => p.User.IsBot);

        if (botPlayer != null)
            _turn.SetCurrent(botPlayer);

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
            startSnapshot.RecordLastManStandingRound(_currentPlayerId, _currentRound, _secondsLeft);
        });

        _turn.SetCurrent(player);

        try
        {
            await Task.WhenAny(TimerCountdown(), _turn.TurnsCountdown(player, roundForcedLifetime));
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

        return;

        async Task TimerCountdown()
        {
            var timer = ModeOptions.RoundTime;
            var timeSpan = TimeSpan.FromSeconds(1);

            while (timer > 0 && roundForcedLifetime.IsTerminated == false)
            {
                timer--;

                _secondsLeft = timer;
                EmitRoundSnapshot();

                await Task.Delay(timeSpan, roundForcedLifetime.Token);
            }
        }
    }

    private void EmitRoundSnapshot()
    {
        _snapshots.Send(snapshot => snapshot.RecordLastManStandingRound(_currentPlayerId, _currentRound, _secondsLeft));
    }
}
