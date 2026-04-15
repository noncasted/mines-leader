using Common;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;

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
[GrainState(Table = "state_user_cards", State = "user_cards", Lookup = "UserCards", Key = GrainKeyType.Guid)]
public class UserCardsState : IProjectionPayload, IStateValue
{
    [Id(0)] public HashSet<CardType> Cards { get; set; } = new();

    public int Version => 0;

    public INetworkContext ToContext() => new SharedBackendUser.CardsProjection
    {
        OwnedCards = Cards.ToList()
    };
}

public class UserCards : UserGrain, IUserCards
{
    public UserCards(
        [State] State<UserCardsState> state,
        ILogger<UserCards> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly State<UserCardsState> _state;
    private readonly ILogger<UserCards> _logger;

    public async Task Initialize()
    {
        var state = await _state.Update(state => {
            foreach (var card in DeckOptions.BaseDeck)
                state.Cards.Add(card);
        });

        await this.SendProjection(state);
    }

    public async Task AddCard(CardType card)
    {
        _logger.LogInformation("[User] [Cards] User {Id} received card {Card}",
            this.GetPrimaryKey(), card);

        var state = await _state.Update(state => state.Cards.Add(card));
        await this.SendProjection(state);
    }

    public Task<bool> HasCard(CardType card)
    {
        return _state.Read(state => state.Cards.Contains(card));
    }

    public Task<IReadOnlyList<CardType>> GetAll()
    {
        return _state.Read(state => (IReadOnlyList<CardType>)state.Cards.ToList());
    }

    public Task<IProjectionPayload> GetProjection()
    {
        return _state.Read(s => (IProjectionPayload)s);
    }
}