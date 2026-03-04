using Common.Reactive;
using Game.Session;
using Microsoft.Extensions.Options;
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
        IOptions<GameOptions> gameOptions,
        IOptions<RoundsOptions> roundOptions) : base("game-round")
    {
        _gameContext = gameContext;
        _readyAwaiter = readyAwaiter;
        _snapshotSender = snapshotSender;
        _roundActionService = roundActionService;
        _players = players;
        _gameOptions = gameOptions;
        _roundOptions = roundOptions;

        BindProperty(_state);
    }

    private readonly ValueProperty<LastManStandingRoundState> _state = new(1);
    private readonly RoundPlayers _players;
    private readonly IGameContext _gameContext;
    private readonly IGameReadyAwaiter _readyAwaiter;
    private readonly ISnapshotSender _snapshotSender;
    private readonly IRoundActionService _roundActionService;
    private readonly IOptions<GameOptions> _gameOptions;
    private readonly IOptions<RoundsOptions> _roundOptions;

    private readonly ViewableProperty<IPlayer> _currentPlayer = new(null);

    private ILifetime? _roundForcedLifetime;

    public IViewableProperty<IPlayer> CurrentPlayer => _currentPlayer!;

    public async Task<Guid> Process(IReadOnlyLifetime lifetime)
    {
        _players.Setup();
        ListenPlayersEvents(lifetime);

        await _readyAwaiter.Await(lifetime);

        var players = _gameContext.Players;
        var snapshot = new MoveSnapshot();
        snapshot.HandleBoards(lifetime, _gameContext);

        foreach (var player in players)
            player.Deck.Init();

        foreach (var player in players)
            _players.RestoreCards(player, snapshot);

        foreach (var player in players)
            player.Board.MinesScanner.Start(lifetime);

        _snapshotSender.Send(snapshot);
        _currentPlayer.Set(players.First());

        while (IsGameOver() == false)
        {
            await ProcessRound(lifetime, _currentPlayer.Value);
            _currentPlayer.Set(players.First(t => t != _currentPlayer.Value));

            _state.Update(state => state.CurrentRound++);
        }

        var winner = GetWinner();

        return winner;

        bool IsGameOver()
        {
            if (lifetime.IsTerminated == true)
                return true;

            var alivePlayers = players.Count(p => p.Health.Current.Value > 0);

            if (alivePlayers <= 1)
                return true;

            if (players.Any(p => p.User.Lifetime.IsTerminated == true))
                return true;

            var flagWinner = _players.GetFlagWinner();

            if (flagWinner != Guid.Empty)
                return true;

            return false;
        }

        Guid GetWinner()
        {
            foreach (var player in players)
            {
                if (player.Health.Current.Value <= 0)
                    continue;

                var opponent = players.FirstOrDefault(p => p != player && p.Health.Current.Value > 0);
                if (opponent == null)
                    return player.User.Id;
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
        var roundLifetime = lifetime.Child();

        _state.Update(state => state.CurrentPlayer = player.User.Id);

        player.Moves.Restore();

        try
        {
            await Task.WhenAny(TimerCountdown(), TurnsCountdown());
        }
        catch (TaskCanceledException)
        {
            // Ignore
        }

        player.Mana.SetMax(player.Mana.Max + 1);
        player.Mana.Restore();

        var snapshot = new MoveSnapshot();
        _players.RestoreCards(player, snapshot);
        _snapshotSender.Send(snapshot);

        _roundActionService.Tick();
        player.Moves.Lock();

        _roundForcedLifetime.Terminate();
        roundLifetime.Terminate();

        return;

        async Task TimerCountdown()
        {
            var timer = _roundOptions.Value.LastManStandingRoundSeconds;
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