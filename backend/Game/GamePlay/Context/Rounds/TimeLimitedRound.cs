using Common.Reactive;
using Game.Session;
using Microsoft.Extensions.Options;
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

    private readonly ValueProperty<TimeLimitedRoundState> _state = new(1);
    private readonly RoundPlayers _players;
    private readonly IGameContext _gameContext;
    private readonly IGameReadyAwaiter _readyAwaiter;
    private readonly ISnapshotSender _snapshotSender;
    private readonly IRoundActionService _roundActionService;
    private readonly IOptions<GameOptions> _gameOptions;
    private readonly IOptions<RoundsOptions> _roundOptions;

    private readonly ViewableProperty<IPlayer> _currentPlayer = new(null);

    private ILifetime? _roundForcedLifetime;

    public IViewableProperty<IPlayer> CurrentPlayer => _currentPlayer;

    public async Task<Guid> Process(IReadOnlyLifetime lifetime)
    {
        _players.Setup();
        ListenPlayersEvents(lifetime);

        await _readyAwaiter.Await(lifetime);

        var players = _gameContext.Players;

        _state.Update(state =>
            {
                var playersSecondsLeft = new Dictionary<Guid, long>();

                foreach (var player in players)
                    playersSecondsLeft.Add(player.User.Id, _roundOptions.Value.TimeLimitedSeconds);

                state.SecondsLeft = playersSecondsLeft;
            }
        );

        var snapshot = new MoveSnapshot();

        foreach (var player in players)
            player.Deck.Init();

        foreach (var player in players)
            _players.RestoreCard(player, snapshot);

        foreach (var player in players)
            player.Board.MinesScanner.Start(lifetime);

        _snapshotSender.Send(snapshot);

        var roundsCount = 0;

        while (IsGameOver() == false)
        {
            await ProcessRound(lifetime, players.First(t => t != _currentPlayer.Value));
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
            await Task.WhenAny(TimerCountdown());
        }
        catch (TaskCanceledException)
        {
            // Ignore
        }

        player.Mana.SetMax(player.Mana.Max + 1);
        player.Mana.Restore();

        var snapshot = new MoveSnapshot();
        _players.RestoreCard(player, snapshot);

        _roundActionService.Tick();
        _snapshotSender.Send(snapshot);
        player.Moves.Lock();

        roundForcedLifetime.Terminate();

        return;

        async Task TimerCountdown()
        {
            var timeSpan = TimeSpan.FromSeconds(1);

            while (_state.Value.SecondsLeft[player.User.Id] > 0 && roundForcedLifetime.IsTerminated == false)
            {
                _state.Update(state => state.SecondsLeft[player.User.Id]--);
                await Task.Delay(timeSpan, roundForcedLifetime.Token);
            }
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