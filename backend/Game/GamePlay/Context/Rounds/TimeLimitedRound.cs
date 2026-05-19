using Cluster.Configs;
using Common.Reactive;
using Game.GamePlay.Snapshots;
using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.GamePlay;

public class TimeLimitedRound : Service, IGameRound
{
    public TimeLimitedRound(
        IGameContext gameContext,
        IGameReadyAwaiter readyAwaiter,
        IPlayersReadyAwaiter playersReadyAwaiter,
        ISnapshotSender snapshotSender,
        ISnapshotDiffGuard diffGuard,
        IRoundActionService roundActionService,
        RoundPlayers players,
        IGameModeConfig modeOptions,
        IPlayerConfig playerConfig,
        ILogger<TimeLimitedRound> logger,
        ISessionLogger sessionLogger) : base("game-round")
    {
        _gameContext = gameContext;
        _readyAwaiter = readyAwaiter;
        _playersReadyAwaiter = playersReadyAwaiter;
        _snapshotSender = snapshotSender;
        _diffGuard = diffGuard;
        _roundActionService = roundActionService;
        _players = players;
        _modeOptions = modeOptions;
        _playerConfig = playerConfig;
        _logger = logger;
        _sessionLogger = sessionLogger;
    }

    private readonly Dictionary<Guid, long> _secondsLeft = new();
    private Guid _currentPlayerId;

    private readonly RoundPlayers _players;
    private readonly IGameModeConfig _modeOptions;
    private readonly IPlayerConfig _playerConfig;
    private readonly ILogger<TimeLimitedRound> _logger;
    private readonly ISessionLogger _sessionLogger;
    private readonly IGameContext _gameContext;
    private readonly IGameReadyAwaiter _readyAwaiter;
    private readonly IPlayersReadyAwaiter _playersReadyAwaiter;
    private readonly ISnapshotSender _snapshotSender;
    private readonly ISnapshotDiffGuard _diffGuard;
    private readonly IRoundActionService _roundActionService;

    private readonly ViewableProperty<IPlayer> _currentPlayer = new(null!);

    private ILifetime? _roundForcedLifetime;
    private TimeLimitedModeOptions ModeOptions => _modeOptions.Value.TimeLimited;

    public IViewableProperty<IPlayer> CurrentPlayer => _currentPlayer;

    public async Task<Guid> Process(IReadOnlyLifetime lifetime)
    {
        foreach (var player in _gameContext.Players)
        {
            player.Hand.SetSize(_playerConfig.Value.HandSize);
            player.Deck.Init(_playerConfig.Value.DeckSize);
        }

        ListenPlayersEvents(lifetime);

        await _readyAwaiter.Await(lifetime);

        var players = _gameContext.Players;

        foreach (var player in players)
            _secondsLeft[player.User.Id] = ModeOptions.RoundTime;

        var snapshot = new MoveSnapshot();

        var initPreState = _diffGuard.IsEnabled == true
            ? GameStateCapture.Capture(_gameContext)
            : null;

        foreach (var player in _gameContext.Players)
        {
            player.Health.SetMax(snapshot, ModeOptions.PlayerHealth);
            player.Health.SetCurrent(snapshot, ModeOptions.PlayerHealth);

            player.Mana.SetMax(snapshot, ModeOptions.PlayerStartMana);
            player.Mana.Restore(snapshot);

            player.Moves.SetMax(snapshot, ModeOptions.PlayerMoves);
        }

        foreach (var player in players)
            _players.RestoreCards(player, snapshot);

        snapshot.RecordGameStarted();
        snapshot.RecordTimeLimitedRound(_currentPlayerId, _secondsLeft);

        if (initPreState != null)
        {
            var postState = GameStateCapture.Capture(_gameContext);
            _diffGuard.Validate(initPreState, snapshot.Collect(), postState, "round:init");
        }

        _snapshotSender.Send(snapshot);

        var playersReadyLifetime = lifetime.Child();

        foreach (var player in players)
            player.User.Lifetime.Listen(() => playersReadyLifetime.Terminate());

        await _playersReadyAwaiter.Await(playersReadyLifetime);

        var botPlayer = players.FirstOrDefault(p => p.User.IsBot);

        if (botPlayer != null)
            _currentPlayer.Set(botPlayer);

        var roundsCount = 0;

        while (IsGameOver() == false)
        {
            var nextPlayer = players.First(t => t != _currentPlayer.Value);
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
            if (lifetime.IsTerminated == true)
                return true;

            if (players.Any(p => p.Health.Current.Value == 0))
                return true;

            if (players.Any(p => p.User.Lifetime.IsTerminated == true))
                return true;

            if (_secondsLeft.Any(kvp => kvp.Value <= 0))
                return true;

            if (roundsCount >= 2)
            {
                var flagWinner = _players.GetFlagWinner();

                if (flagWinner != Guid.Empty)
                    return true;
            }

            return false;
        }

        Guid GetWinner()
        {
            foreach (var player in players)
            {
                if (player.Health.Current.Value <= 0)
                    return _gameContext.GetOpponent(player).User.Id;
            }

            foreach (var (user, player) in _gameContext.UserToPlayer)
            {
                if (user.Lifetime.IsTerminated == true)
                    return _gameContext.GetOpponent(player).User.Id;
            }

            foreach (var (id, timeLeft) in _secondsLeft)
            {
                if (timeLeft <= 0)
                    return players.First(p => p.User.Id != id).User.Id;
            }

            var flagWinner = _players.GetFlagWinner();

            if (flagWinner != Guid.Empty)
                return flagWinner;

            return Guid.Empty;
        }

        string GetWinReason()
        {
            foreach (var player in players)
            {
                if (player.Health.Current.Value <= 0)
                    return $"Player {player.User.Id} health reached 0";
            }

            foreach (var (user, _) in _gameContext.UserToPlayer)
            {
                if (user.Lifetime.IsTerminated == true)
                    return $"Player {user.Id} disconnected";
            }

            foreach (var (id, timeLeft) in _secondsLeft)
            {
                if (timeLeft <= 0)
                    return $"Player {id} ran out of time";
            }

            if (_players.GetFlagWinner() != Guid.Empty)
                return "All opponent mines flagged";

            return "Unknown";
        }
    }

    public void SkipTurn()
    {
        _sessionLogger.LogTurnSkipped(_currentPlayer.Value?.User.Id ?? Guid.Empty);
        _roundForcedLifetime!.Terminate();
    }

    private async Task ProcessRound(IReadOnlyLifetime lifetime, IPlayer player)
    {
        _roundForcedLifetime = lifetime.Child();
        var roundForcedLifetime = _roundForcedLifetime;

        _currentPlayerId = player.User.Id;

        {
            var startSnapshot = new MoveSnapshot();

            var startPreState = _diffGuard.IsEnabled == true
                ? GameStateCapture.Capture(_gameContext)
                : null;

            player.Moves.Restore(startSnapshot);
            startSnapshot.RecordTimeLimitedRound(_currentPlayerId, _secondsLeft);

            if (startPreState != null)
            {
                var postState = GameStateCapture.Capture(_gameContext);
                _diffGuard.Validate(startPreState, startSnapshot.Collect(), postState, "round:start");
            }

            _snapshotSender.Send(startSnapshot);
        }

        _currentPlayer.Set(player);

        var roundLock = new SemaphoreSlim(1, 1);

        player.Actions.CellOpened.Advise(roundForcedLifetime, AddTimeForAction);
        player.Actions.CardUsed.Advise(roundForcedLifetime, AddTimeForAction);

        try
        {
            await Task.WhenAny(TimerCountdown(), TurnsCountdown());
        }
        catch (TaskCanceledException)
        {
            // Ignore
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in round timer");
        }

        {
            var endSnapshot = new MoveSnapshot();

            var endPreState = _diffGuard.IsEnabled == true
                ? GameStateCapture.Capture(_gameContext)
                : null;

            if (player.Mana.ResultMax < ModeOptions.MaxManaCap)
            {
                player.Mana.SetMax(endSnapshot, player.Mana.ResultMax + 1);
            }

            player.Mana.Restore(endSnapshot);
            _sessionLogger.LogManaChanged(player.User.Id, player.Mana.Current, player.Mana.ResultMax);

            _players.RestoreCards(player, endSnapshot);

            _roundActionService.Tick(endSnapshot);
            player.Moves.Lock(endSnapshot);

            if (endPreState != null)
            {
                var postState = GameStateCapture.Capture(_gameContext);
                _diffGuard.Validate(endPreState, endSnapshot.Collect(), postState, "round:end");
            }

            _snapshotSender.Send(endSnapshot);
        }

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

        async Task TurnsCountdown()
        {
            var timeSpan = TimeSpan.FromSeconds(0.2);

            while (player.Moves.Left > 0 && roundForcedLifetime.IsTerminated == false)
                await Task.Delay(timeSpan, roundForcedLifetime.Token);
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
        var snapshot = new MoveSnapshot();
        snapshot.RecordTimeLimitedRound(_currentPlayerId, _secondsLeft);
        _snapshotSender.Send(snapshot);
    }

    private void ListenPlayersEvents(IReadOnlyLifetime lifetime)
    {
        foreach (var player in _gameContext.Players)
        {
            player.Health.Current.Advise(lifetime, health => {
                if (health > 0)
                    return;

                _roundForcedLifetime?.Terminate();
            });

            player.User.Lifetime.Listen(() => {
                SkipTurn();

                if (lifetime.IsTerminated == true || _roundForcedLifetime == null)
                    return;

                _roundForcedLifetime.Terminate();
            });
        }
    }
}