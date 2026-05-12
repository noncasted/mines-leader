using Common;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;
using Cluster.Configs;

namespace Meta.Users;

public interface IUserCards : IUserGrain, IUserProjectionSource
{
    [Transaction]
    Task Initialize();

    [Transaction]
    Task AddCard(CardType card);

    [Transaction]
    Task<bool> HasCard(CardType card);

    [Transaction]
    Task<IReadOnlyList<CardType>> GetAll();
}

[GenerateSerializer]
[GrainEventState(State = "user_cards", Lookup = "UserCards", Key = GrainKeyType.Guid)]
public class UserCardsState : IEventStateValue, IProjectionPayload
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public HashSet<CardType> Cards { get; set; } = new();

    public int Version => 0;

    public void Apply(CardsInitialized e)
    {
        foreach (var card in e.BaseDeck)
            Cards.Add(card);
    }

    public void Apply(CardAdded e) => Cards.Add(e.Card);

    public INetworkContext ToContext() => new SharedBackendUser.CardsProjection
    {
        OwnedCards = Cards.ToList()
    };
}

public class CardsInitialized
{
    public IReadOnlyList<CardType> BaseDeck { get; set; } = new List<CardType>();
}

public class CardAdded
{
    public CardType Card { get; set; }
}

public class UserCards : UserGrain, IUserCards
{
    public UserCards(
        [EventState] EventState<UserCardsState> state,
        IUserDeckConfig userDeckConfig,
        ILogger<UserCards> logger)
    {
        _state = state;
        _userDeckConfig = userDeckConfig;
        _logger = logger;
    }

    private readonly EventState<UserCardsState> _state;
    private readonly IUserDeckConfig _userDeckConfig;
    private readonly ILogger<UserCards> _logger;

    public async Task Initialize()
    {
        await _state.Read();
        if (_state.Value.Cards.Count > 0) return;

        await _state.Append(new CardsInitialized { BaseDeck = _userDeckConfig.Value.BaseDeck });
        await _state.WriteSession();
        await this.SendProjection(_state.Value);
    }

    public async Task AddCard(CardType card)
    {
        _logger.LogInformation("[User] [Cards] User {Id} received card {Card}",
            this.GetPrimaryKey(), card);

        await _state.Read();
        await _state.Append(new CardAdded { Card = card });
        await _state.WriteSession();
        await this.SendProjection(_state.Value);
    }

    public Task<bool> HasCard(CardType card)
    {
        return _state.ReadAndHasCard(card);
    }

    public Task<IReadOnlyList<CardType>> GetAll()
    {
        return _state.ReadAndGetAll();
    }

    public Task<IProjectionPayload> GetProjection()
    {
        return Task.FromResult((IProjectionPayload)_state.Value);
    }
}

public static class UserCardsEventStateExtensions
{
    public static async Task<bool> ReadAndHasCard(this EventState<UserCardsState> state, CardType card)
    {
        await state.Read();
        return state.Value.Cards.Contains(card);
    }

    public static async Task<IReadOnlyList<CardType>> ReadAndGetAll(this EventState<UserCardsState> state)
    {
        await state.Read();
        return state.Value.Cards.ToList();
    }
}
