using Common;
using Microsoft.Extensions.Options;
using Shared;

namespace Game.GamePlay;

public interface IGameRound
{
    IPlayer CurrentMove { get; }
}

public class GameRound : Service, IUsersConnected
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

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        var roundLifetime = lifetime.Child();
        
        await _readyAwaiter.Await(lifetime);
        
        ManaLoop(roundLifetime).NoAwait();

        var snapshot = new MoveSnapshot(_gameContext);
        
        foreach (var player in _gameContext.Players)
        {
            player.Deck.AddRandom(_options.Value.StartCards);
            snapshot.RecordDeckFill(player.User.Id, player.Deck.Count);
        }

        _snapshotSender.Send(snapshot);
        
        var currentPlayer = _gameContext.Players.First();

        while (IsGameOver() == false)
        {
            await ProcessRound(lifetime, currentPlayer);
            currentPlayer = _gameContext.Players.First(t => t != currentPlayer);
        }

        return;

        bool IsGameOver()
        {
            return _gameContext.Players.Any(p => p.Health.Current.Value == 0);
        }
    }

    private async Task ProcessRound(IReadOnlyLifetime lifetime, IPlayer player)
    {
        var roundLifetime = lifetime.Child();
        var timer = _options.Value.RoundTime;
        
        player.Moves.Restore();

        await Task.WhenAny(TimerCountdown(), TurnsCountdown());
        
        roundLifetime.Terminate();
        
        var cardsNeeded = _options.Value.HandSize - player.Hand.Entries.Count;

        var snapshot = new MoveSnapshot(_gameContext);

        for (var i = 0; i < cardsNeeded; i++)
        {
            if (player.Deck.Count == 0)
            {
                var stashCards = player.Stash.Collect();

                foreach (var cardType in stashCards)
                    player.Deck.AddCard(cardType);
                
                snapshot.RecordReshuffleFromStash(player.User.Id, stashCards.Count);
            }
            
            var card = player.Deck.DrawCard();
            player.Hand.Add(card);
            snapshot.RecordCardDraw(player.User.Id, card);
        }        
        async Task TimerCountdown()
        {
            var timeSpan = TimeSpan.FromSeconds(1);
            
            while (timer > 0 && roundLifetime.IsTerminated == false)
            {
                timer--;
                await Task.Delay(timeSpan, roundLifetime.Token);
            }
        }

        async Task TurnsCountdown()
        {
            var timeSpan = TimeSpan.FromSeconds(0.2);
            
            while (player.Moves.Left > 0 && roundLifetime.IsTerminated == false)
                await Task.Delay(timeSpan, roundLifetime.Token);
        }
    }

    private async Task ManaLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), lifetime.Token);

            foreach (var player in _gameContext.Players)
                player.Mana.SetCurrent(player.Mana.Current + 1);
        }
    }
}