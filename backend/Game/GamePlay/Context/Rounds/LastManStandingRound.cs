using Cluster.Configs;
using Common.Reactive;
using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.GamePlay;

public class LastManStandingRound : Service, IGameRound
{
    public LastManStandingRound(
        IGameContext gameContext,
        IGameReadyAwaiter readyAwaiter,
        ISnapshotSender snapshotSender,
        IRoundActionService roundActionService,
        RoundPlayers players,
        IGameModeConfig modeOptions,
        ILogger<TimeLimitedRound> logger) : base("game-round")
    {
        _gameContext = gameContext;
        _readyAwaiter = readyAwaiter;
        _snapshotSender = snapshotSender;
        _roundActionService = roundActionService;
        _players = players;
        _modeOptions = modeOptions;
        _logger = logger;

        BindProperty(_state);
    }

    private readonly ValueProperty<LastManStandingRoundState> _state = new(1);
    private readonly RoundPlayers _players;
    private readonly IGameContext _gameContext;
    private readonly IGameReadyAwaiter _readyAwaiter;
    private readonly ISnapshotSender _snapshotSender;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameModeConfig _modeOptions;
    private readonly ILogger<TimeLimitedRound> _logger;

    private readonly ViewableProperty<IPlayer> _currentPlayer = new(null);

    private ILifetime? _roundForcedLifetime;

    private LastManStandingModeOptions ModeOptions => _modeOptions.Value.LastManStanding;

    public IViewableProperty<IPlayer> CurrentPlayer => _currentPlayer!;

    public async Task<Guid> Process(IReadOnlyLifetime lifetime)
    {
        foreach (var player in _gameContext.Players)
        {
            player.Hand.SetSize(ModeOptions.HandSize);

            player.Health.SetMax(ModeOptions.PlayerHealth);
            player.Health.SetCurrent(ModeOptions.PlayerHealth);

            player.Mana.SetMax(ModeOptions.PlayerStartMana);
            player.Mana.Restore();

            player.Moves.SetMax(ModeOptions.PlayerMoves);

            player.Deck.Init(ModeOptions.DeckSize);
        }

        ListenPlayersEvents(lifetime);

        await _readyAwaiter.Await(lifetime);

        var players = _gameContext.Players;

        var snapshot = new MoveSnapshot();

        foreach (var player in players)
            _players.RestoreCards(player, snapshot);

        foreach (var player in players)
            player.Board.MinesScanner.Start(lifetime);

        _snapshotSender.Send(snapshot);
        var roundsCount = 0;

        while (IsGameOver() == false)
        {
            await ProcessRound(lifetime, players.First(t => t != _currentPlayer.Value));
            _state.Update(state => state.CurrentRound++);
            roundsCount++;
        }

        var winner = GetWinner();

        return winner;

        bool IsGameOver()
        {
            if (lifetime.IsTerminated == true)
                return true;

            if (players.Any(p => p.Health.Current.Value == 0))
                return true;

            if (players.Any(p => p.User.Lifetime.IsTerminated == true))
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

            var flagWinner = _players.GetFlagWinner();

            if (flagWinner != Guid.Empty)
                return flagWinner;

            return Guid.Empty;
        }
    }

    public void SkipTurn()
    {
        _roundForcedLifetime!.Terminate();
    }

    private async Task ProcessRound(IReadOnlyLifetime lifetime, IPlayer player)
    {
        _roundForcedLifetime = lifetime.Child();
        var roundForcedLifetime = _roundForcedLifetime;

        _state.Update(state => state.CurrentPlayer = player.User.Id);

        player.Moves.Restore();
        _currentPlayer.Set(player);

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


        player.Mana.SetMax(player.Mana.Max + 1);
        player.Mana.Restore();

        var snapshot = new MoveSnapshot();
        _players.RestoreCards(player, snapshot);
        _snapshotSender.Send(snapshot);

        _roundActionService.Tick();
        player.Moves.Lock();

        roundForcedLifetime.Terminate();

        return;

        async Task TimerCountdown()
        {
            var timer = ModeOptions.RoundTime;
            var timeSpan = TimeSpan.FromSeconds(1);

            while (timer > 0 && _roundForcedLifetime.IsTerminated == false)
            {
                timer--;

                var timerValue = timer;
                _state.Update(state => state.SecondsLeft = timerValue);

                await Task.Delay(timeSpan, _roundForcedLifetime.Token);
            }
        }

        async Task TurnsCountdown()
        {
            var timeSpan = TimeSpan.FromSeconds(0.2);

            while (player.Moves.Left > 0 && _roundForcedLifetime.IsTerminated == false)
                await Task.Delay(timeSpan, _roundForcedLifetime.Token);
        }
    }

    private void ListenPlayersEvents(IReadOnlyLifetime lifetime)
    {
        foreach (var player in _gameContext.Players)
        {
            player.Health.Current.Advise(lifetime, health =>
                {
                    if (health > 0)
                        return;

                    _roundForcedLifetime?.Terminate();
                }
            );

            player.User.Lifetime.Listen(() =>
                {
                    SkipTurn();

                    if (lifetime.IsTerminated == true || _roundForcedLifetime == null)
                        return;

                    _roundForcedLifetime.Terminate();
                }
            );
        }
    }
}