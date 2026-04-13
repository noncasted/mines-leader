using Cluster.Configs;
using Common.Reactive;
using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.GamePlay;

public class TimeLimitedRound : Service, IGameRound
{
    public TimeLimitedRound(
        IGameContext gameContext,
        IGameReadyAwaiter readyAwaiter,
        ISnapshotSender snapshotSender,
        IRoundActionService roundActionService,
        RoundPlayers players,
        IGameModeConfig modeOptions,
        ILogger<TimeLimitedRound> logger,
        ISessionLogger sessionLogger) : base("game-round")
    {
        _gameContext = gameContext;
        _readyAwaiter = readyAwaiter;
        _snapshotSender = snapshotSender;
        _roundActionService = roundActionService;
        _players = players;
        _modeOptions = modeOptions;
        _logger = logger;
        _sessionLogger = sessionLogger;

        BindProperty(_state);
    }

    private readonly ValueProperty<TimeLimitedRoundState> _state = new(1);
    private readonly RoundPlayers _players;
    private readonly IGameModeConfig _modeOptions;
    private readonly ILogger<TimeLimitedRound> _logger;
    private readonly ISessionLogger _sessionLogger;
    private readonly IGameContext _gameContext;
    private readonly IGameReadyAwaiter _readyAwaiter;
    private readonly ISnapshotSender _snapshotSender;
    private readonly IRoundActionService _roundActionService;

    private readonly ViewableProperty<IPlayer> _currentPlayer = new(null);

    private ILifetime? _roundForcedLifetime;
    private TimeLimitedModeOptions ModeOptions => _modeOptions.Value.TimeLimited;

    public IViewableProperty<IPlayer> CurrentPlayer => _currentPlayer;

    public async Task<Guid> Process(IReadOnlyLifetime lifetime)
    {
        foreach (var player in _gameContext.Players)
        {
            player.Hand.SetSize(ModeOptions.HandSize);
            player.Deck.Init(ModeOptions.DeckSize);
        }

        ListenPlayersEvents(lifetime);

        await _readyAwaiter.Await(lifetime);

        var players = _gameContext.Players;

        _state.Update(state => {
            var playersSecondsLeft = new Dictionary<Guid, long>();

            foreach (var player in players)
                playersSecondsLeft.Add(player.User.Id, ModeOptions.RoundTime);

            state.SecondsLeft = playersSecondsLeft;
        });

        var snapshotLifetime = new Lifetime();
        var snapshot = new MoveSnapshot();
        snapshot.HandlePlayers(snapshotLifetime, _gameContext);

        foreach (var player in _gameContext.Players)
        {
            player.Health.SetMax(ModeOptions.PlayerHealth);
            player.Health.SetCurrent(ModeOptions.PlayerHealth);

            player.Mana.SetMax(ModeOptions.PlayerStartMana);
            player.Mana.Restore();

            player.Moves.SetMax(ModeOptions.PlayerMoves);
        }

        foreach (var player in players)
            _players.RestoreCards(player, snapshot);

        foreach (var player in players)
            player.Board.MinesScanner.Start(lifetime);

        snapshot.RecordGameStarted();
        _snapshotSender.Send(snapshot);
        snapshotLifetime.Terminate();

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

            if (_state.Value.SecondsLeft.Any(kvp => kvp.Value <= 0))
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

            foreach (var (id, timeLeft) in _state.Value.SecondsLeft)
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

            foreach (var (id, timeLeft) in _state.Value.SecondsLeft)
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

        _state.Update(state => state.CurrentPlayer = player.User.Id);

        {
            var startLifetime = new Lifetime();
            var startSnapshot = new MoveSnapshot();
            startSnapshot.HandlePlayers(startLifetime, _gameContext);

            player.Moves.Restore();

            _snapshotSender.Send(startSnapshot);
            startLifetime.Terminate();
        }

        _currentPlayer.Set(player);

        var roundLock = new SemaphoreSlim(1, 1);

        player.Actions.CellOpened.Advise(roundForcedLifetime, AddTimeForAction);
        player.Actions.CardUsed.Advise(roundForcedLifetime, AddTimeForAction);

        try
        {
            await Task.WhenAny(TimerCountdown());
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
            var endLifetime = new Lifetime();
            var endSnapshot = new MoveSnapshot();
            endSnapshot.HandlePlayers(endLifetime, _gameContext);

            player.Mana.SetMax(player.Mana.Max + 1);
            player.Mana.Restore();

            _players.RestoreCards(player, endSnapshot);

            _roundActionService.Tick();
            player.Moves.Lock();

            _snapshotSender.Send(endSnapshot);
            endLifetime.Terminate();
        }

        roundForcedLifetime.Terminate();

        return;

        async Task TimerCountdown()
        {
            var timeSpan = TimeSpan.FromSeconds(1);

            while (_state.Value.SecondsLeft[player.User.Id] > 0 && roundForcedLifetime.IsTerminated == false)
            {
                try
                {
                    await roundLock.WaitAsync();
                    _state.Update(state => state.SecondsLeft[player.User.Id]--);
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
                _state.Update(state => state.SecondsLeft[player.User.Id] += ModeOptions.TimeGainPerAction);
            }
            finally
            {
                roundLock.Release();
            }
        }
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