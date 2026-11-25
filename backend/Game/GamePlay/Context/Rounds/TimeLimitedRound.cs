using Cluster.Configs;
using Internal;
using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.GamePlay;

public class TimeLimitedRound : Service, IGameRound
{
    public TimeLimitedRound(
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

    private readonly Dictionary<Guid, long> _secondsLeft = new();
    private Guid _currentPlayerId;

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

    private TimeLimitedModeOptions ModeOptions => _modeOptions.Value.TimeLimited;

    public IViewableProperty<IPlayer> CurrentPlayer => _turn.CurrentPlayer;

    public async Task<Guid> Process(IReadOnlyLifetime lifetime)
    {
        _readiness.PrepareHands();
        _turn.ListenPlayers(lifetime);

        await _readiness.AwaitGame(lifetime);

        var players = _gameContext.Players;

        foreach (var player in players)
            _secondsLeft[player.User.Id] = ModeOptions.RoundTime;

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
            snapshot.RecordTimeLimitedRound(_currentPlayerId, _secondsLeft);
        });

        await _readiness.AwaitPlayers(lifetime);

        var botPlayer = players.FirstOrDefault(p => p.User.IsBot);

        if (botPlayer != null)
            _turn.SetCurrent(botPlayer);

        var roundsCount = 0;

        while (IsGameOver() == false)
        {
            var nextPlayer = players.First(t => t != _turn.CurrentPlayer.Value);
            roundsCount++;
            _sessionLogger.LogRoundStart(nextPlayer.User.Id, roundsCount);
            await ProcessRound(lifetime, nextPlayer);
            _sessionLogger.LogRoundEnd(nextPlayer.User.Id, roundsCount);
        }

        var winner = GetWinner();
        _sessionLogger.LogGameOver(winner, GetWinReason());

        return winner;

        bool IsGameOver()
        {
            if (_secondsLeft.Any(kvp => kvp.Value <= 0))
                return true;

            return _outcome.IsOver(lifetime, players, roundsCount);
        }

        Guid GetWinner()
        {
            var defeatWinner = _outcome.FindDefeatWinner(players);

            if (defeatWinner != Guid.Empty)
                return defeatWinner;

            foreach (var (id, timeLeft) in _secondsLeft)
            {
                if (timeLeft <= 0)
                    return players.First(p => p.User.Id != id).User.Id;
            }

            return _players.GetFlagWinner();
        }

        string GetWinReason()
        {
            var defeatReason = _outcome.FindDefeatReason(players);

            if (defeatReason != null)
                return defeatReason;

            foreach (var (id, timeLeft) in _secondsLeft)
            {
                if (timeLeft <= 0)
                    return $"Player {id} ran out of time";
            }

            return _outcome.FindFlagReason() ?? "Unknown";
        }
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
            startSnapshot.RecordTimeLimitedRound(_currentPlayerId, _secondsLeft);
        });

        _turn.SetCurrent(player);

        var roundLock = new SemaphoreSlim(1, 1);

        player.Actions.CellOpened.Advise(roundForcedLifetime, AddTimeForAction);
        player.Actions.CardUsed.Advise(roundForcedLifetime, AddTimeForAction);

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
            var timeSpan = TimeSpan.FromSeconds(1);

            while (_secondsLeft[player.User.Id] > 0 && roundForcedLifetime.IsTerminated == false)
            {
                try
                {
                    await roundLock.WaitAsync();
                    _secondsLeft[player.User.Id]--;
                    EmitRoundSnapshot();
                }
                finally
                {
                    roundLock.Release();
                }

                await Task.Delay(timeSpan, roundForcedLifetime.Token);
            }
        }

        void AddTimeForAction()
        {
            try
            {
                roundLock.Wait();
                _secondsLeft[player.User.Id] += ModeOptions.TimeGainPerAction;
                EmitRoundSnapshot();
            }
            finally
            {
                roundLock.Release();
            }
        }
    }

    private void EmitRoundSnapshot()
    {
        _snapshots.Send(snapshot => snapshot.RecordTimeLimitedRound(_currentPlayerId, _secondsLeft));
    }
}
