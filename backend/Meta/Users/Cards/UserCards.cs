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

public record CardsInitialized(IReadOnlyList<CardType> BaseDeck);

public record CardAdded(CardType Card);

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
        var state = await _state.Read();
        if (state.Cards.Count > 0) return;

        state = await _state.Apply(new CardsInitialized(_userDeckConfig.Value.BaseDeck));
        await this.SendProjection(state);
    }

    public async Task AddCard(CardType card)
    {
        _logger.LogInformation("[User] [Cards] User {Id} received card {Card}",
            this.GetPrimaryKey(), card);

        var state = await _state.Apply(new CardAdded(card));
        await this.SendProjection(state);
    }

    public async Task<bool> HasCard(CardType card)
    {
        var state = await _state.Read();
        return state.Cards.Contains(card);
    }

    public async Task<IReadOnlyList<CardType>> GetAll()
    {
        var state = await _state.Read();
        return state.Cards.ToList();
    }

    public async Task<IProjectionPayload> GetProjection()
    {
        var state = await _state.Read();
        return state;
    }
}
