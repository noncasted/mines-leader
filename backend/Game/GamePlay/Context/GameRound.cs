using Backend.Matches;
using Common;
using Infrastructure.Orleans;
using Microsoft.Extensions.Options;
using Shared;

namespace Game.GamePlay;

public interface IGameRound
{
    IPlayer CurrentPlayer { get; }

    Task<Guid> Process(IReadOnlyLifetime lifetime);
    void SkipTurn();
}

public class GameRound : Service, IGameRound
{
    public GameRound(
        IOrleans orleans,
        ISessionData sessionData,
        ISessionUsers users,
        IGameContext gameContext,
        IGameReadyAwaiter readyAwaiter,
        ISnapshotSender snapshotSender,
        IOptions<GameOptions> options) : base("game-round")
    {
        _orleans = orleans;
        _sessionData = sessionData;
        _users = users;
        _gameContext = gameContext;
        _readyAwaiter = readyAwaiter;
        _snapshotSender = snapshotSender;
        _options = options;

        BindProperty(_state);
    }

    private readonly ValueProperty<GameRoundState> _state = new(1);
    private readonly IOrleans _orleans;
    private readonly ISessionData _sessionData;
    private readonly ISessionUsers _users;
    private readonly IGameContext _gameContext;
    private readonly IGameReadyAwaiter _readyAwaiter;
    private readonly ISnapshotSender _snapshotSender;
    private readonly IOptions<GameOptions> _options;

    private IPlayer? _currentPlayer;
    private ILifetime? _roundLifetime;

    public IPlayer CurrentPlayer => _currentPlayer!;

    public void SkipTurn()
    {
        _roundLifetime!.Terminate();
    }

    public async Task<Guid> Process(IReadOnlyLifetime lifetime)
    {
        var match = _orleans.GetGrain<IMatch>(_sessionData.Id);
        await _orleans.InTransaction(() => match.Setup(GameMatchType.PvP, _users.Select(t => t.Id).ToList()));

        var options = _options.Value; 

        foreach (var player in _gameContext.Players)
        {
            player.User.Lifetime.Listen(SkipTurn);
            
            player.Hand.SetSize(options.HandSize);
            player.Health.SetMax(options.MaxHealth);
            player.Health.SetCurrent(options.StartHealth);
            player.Mana.SetMax(options.MaxMana);
            player.Mana.SetCurrent(options.StartMana);
            player.Moves.SetMax(options.MovesCount);
        }

        foreach (var player in _gameContext.Players)
        {
            player.Health.Current.Advise(lifetime, health =>
            {
                if (health > 0)
                    return;
                
                _roundLifetime?.Terminate();
            });
            
            player.User.Lifetime.Listen(() =>
            {
                if (lifetime.IsTerminated == true || _roundLifetime == null)
                    return;
                
                _roundLifetime.Terminate();
            });
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

        var winner = GetWinner();

        return winner;

        bool IsGameOver()
        {
            if (lifetime.IsTerminated == true)
                return true;
            
            if (_gameContext.Players.Any(p => p.Health.Current.Value == 0))
                return true;

            if (_gameContext.UserToPlayer.Any(p => p.Key.Lifetime.IsTerminated == true))
                return true;

            return false;
        }

        Guid GetWinner()
        {
            foreach (var player in _gameContext.Players)
            {
                if (player.Health.Current.Value <= 0)
                    return _gameContext.GetOpponent(player).User.Id;
            }

            foreach (var (user, player) in _gameContext.UserToPlayer)
            {
                if (user.Lifetime.IsTerminated == true)
                    return _gameContext.GetOpponent(player).User.Id;
            }

            return Guid.Empty;
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

        try
        {
            await Task.WhenAny(TimerCountdown(), TurnsCountdown());
        }
        catch (TaskCanceledException)
        {
            // Ignore
        }

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