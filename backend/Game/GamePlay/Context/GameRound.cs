using Common;
using Microsoft.Extensions.Options;
using Shared;

namespace Game.GamePlay;

public interface IGameRound
{
    IPlayer CurrentPlayer { get; }

    void SkipTurn();
}

public class GameRound : Service, IUsersConnected, IGameRound
{
    public GameRound(
        ISessionUsers users,
        IPlayerFactory playerFactory,
        IGameContext gameContext,
        IGameReadyAwaiter readyAwaiter,
        ISnapshotSender snapshotSender,
        IOptions<GameOptions> options) : base("game-round")
    {
        _users = users;
        _playerFactory = playerFactory;
        _gameContext = gameContext;
        _readyAwaiter = readyAwaiter;
        _snapshotSender = snapshotSender;
        _options = options;

        BindProperty(_state);
    }

    private readonly ValueProperty<GameRoundState> _state = new(1);
    private readonly ISessionUsers _users;
    private readonly IPlayerFactory _playerFactory;
    private readonly IGameContext _gameContext;
    private readonly IGameReadyAwaiter _readyAwaiter;
    private readonly ISnapshotSender _snapshotSender;
    private readonly IOptions<GameOptions> _options;

    private IPlayer? _currentPlayer;
    private ILifetime? _roundLifetime;

    public IPlayer CurrentPlayer => _currentPlayer!;

    public Task OnUsersConnected(IReadOnlyLifetime lifetime)
    {
        foreach (var user in _users)
        {
            var player = _playerFactory.Create(user);
            _gameContext.AddPlayer(player);
        }

        Loop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public void SkipTurn()
    {
        _roundLifetime!.Terminate();
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        var options = _options.Value;

        foreach (var player in _gameContext.Players)
        {
            player.Hand.SetSize(options.HandSize);
            player.Health.SetMax(options.MaxHealth);
            player.Health.SetCurrent(options.StartHealth);
            player.Mana.SetMax(options.MaxMana);
            player.Mana.SetCurrent(options.StartMana);
            player.Moves.SetMax(options.MovesCount);
        }

        var roundLifetime = lifetime.Child();

        await _readyAwaiter.Await(lifetime);

        ManaLoop(roundLifetime).NoAwait();

        var snapshot = new MoveSnapshot(_gameContext, lifetime);
        snapshot.Start();

        foreach (var player in _gameContext.Players)
            player.Deck.AddRandom(options.DeckSize);

        foreach (var player in _gameContext.Players)
            RestoreCard(player, snapshot);

        foreach (var player in _gameContext.Players)
            player.Board.MinesScanner.Start(lifetime);

        _snapshotSender.Send(snapshot);
        _currentPlayer = _gameContext.Players.First();

        while (IsGameOver() == false)
        {
            await ProcessRound(lifetime, _currentPlayer);
            _currentPlayer = _gameContext.Players.First(t => t != _currentPlayer);
        }

        return;

        bool IsGameOver()
        {
            if (_gameContext.Players.Any(p => p.Health.Current.Value == 0))
                return true;

            if (_gameContext.UserToPlayer.Any(p => p.Key.Lifetime.IsTerminated == true))
                return true;

            return false;
        }
    }

    private async Task ProcessRound(IReadOnlyLifetime lifetime, IPlayer player)
    {
        _roundLifetime = lifetime.Child();
        var timer = _options.Value.RoundTime;

        _state.Update(state =>
            {
                state.SecondsLeft = timer;
                state.CurrentPlayer = player.User.Id;
            }
        );

        player.Moves.Restore();

        var snapshot = new MoveSnapshot(_gameContext, lifetime);
        snapshot.Start();

        await Task.WhenAny(TimerCountdown(), TurnsCountdown());

        _roundLifetime.Terminate();

        RestoreCard(player, snapshot);

        _snapshotSender.Send(snapshot);

        player.Moves.Lock();

        return;

        async Task TimerCountdown()
        {
            var timeSpan = TimeSpan.FromSeconds(1);

            while (timer > 0 && _roundLifetime.IsTerminated == false)
            {
                timer--;

                var timerValue = timer;
                _state.Update(state => state.SecondsLeft = timerValue);

                await Task.Delay(timeSpan, _roundLifetime.Token);
            }
        }

        async Task TurnsCountdown()
        {
            var timeSpan = TimeSpan.FromSeconds(0.2);

            while (player.Moves.Left > 0 && _roundLifetime.IsTerminated == false)
                await Task.Delay(timeSpan, _roundLifetime.Token);
        }
    }

    private async Task ManaLoop(IReadOnlyLifetime lifetime)
    {
        var timeSpan = TimeSpan.FromSeconds(3);
        
        while (lifetime.IsTerminated == false)
        {
            await Task.Delay(timeSpan, lifetime.Token);

            foreach (var player in _gameContext.Players)
                player.Mana.SetCurrent(player.Mana.Current + 1);
        }
    }

    private void RestoreCard(IPlayer player, MoveSnapshot snapshot)
    {
        var cardsNeeded = _options.Value.HandSize - player.Hand.Entries.Count;

        for (var i = 0; i < cardsNeeded; i++)
        {
            if (player.Deck.Count == 0)
            {
                var stashCards = player.Stash.Collect();

                foreach (var cardType in stashCards)
                    player.Deck.AddCard(cardType);
            }

            var card = player.Deck.DrawCard();
            player.Hand.Add(card);
            snapshot.RecordCardDraw(player.User.Id, card);
        }
    }
}