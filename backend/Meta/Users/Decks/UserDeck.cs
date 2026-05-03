using Common;
using Infrastructure;
using Infrastructure.State;
using Shared;
using Cluster.Configs;

namespace Meta.Users;

public interface IUserDeck : IUserGrain, IUserProjectionSource
{
    [Transaction]
    Task Initialize();

    [Transaction]
    Task Update(IReadOnlyDictionary<int, IReadOnlyList<CardType>> decks, int selectedIndex);

    [Transaction]
    Task Update(int index, IReadOnlyList<CardType> cards);

    [Transaction]
    Task<IReadOnlyList<CardType>> GetSelected();

    [Transaction]
    Task<UserDeckState> GetState();
}

[GenerateSerializer]
[GrainState(Table = "state_user_projection", State = "user_deck", Lookup = "UserDeck", Key = GrainKeyType.Guid)]
public class UserDeckState : IProjectionPayload, IStateValue
{
    [Id(0)] public Dictionary<int, Entry> Entries { get; } = new();
    [Id(1)] public int SelectedIndex { get; set; }

    public int Version => 0;

    public INetworkContext ToContext()
    {
        return new SharedBackendUser.DeckProjection()
        {
            SelectedIndex = SelectedIndex,
            Entries = Entries.ToDictionary(entry => entry.Key,
                entry => new SharedBackendUser.DeckProjection.Entry
                {
                    DeckIndex = entry.Value.Index,
                    Cards = entry.Value.Cards
                })
        };
    }

    [GenerateSerializer]
    public class Entry
    {
        [Id(0)] public required int Index { get; init; }

        [Id(1)] public required IReadOnlyList<CardType> Cards { get; init; }
    }
}

public class UserDeck : UserGrain, IUserDeck
{
    public UserDeck(
        [State] State<UserDeckState> state,
        IUserDeckConfig userDeckConfig)
    {
        _state = state;
        _userDeckConfig = userDeckConfig;
    }

    private readonly State<UserDeckState> _state;
    private readonly IUserDeckConfig _userDeckConfig;

    public async Task Initialize()
    {
        var state = await _state.Update(state => {
            for (var i = 0; i < DeckOptions.MaxDecks; i++)
            {
                var cards = new List<CardType>(_userDeckConfig.Value.BaseDeck);

                state.Entries[i] = new UserDeckState.Entry
                {
                    Index = i,
                    Cards = cards
                };
            }

            state.SelectedIndex = 0;
        });

        await this.SendProjection(state);
    }

    public async Task Update(IReadOnlyDictionary<int, IReadOnlyList<CardType>> decks, int selectedIndex)
    {
        foreach (var (_, cards) in decks)
            await ValidateCards(cards);

        var state = await _state.Update(state => {
            foreach (var (index, cards) in decks)
            {
                state.Entries[index] = new UserDeckState.Entry
                {
                    Index = index,
                    Cards = cards
                };
            }

            state.SelectedIndex = selectedIndex;
        });
    }

    public async Task Update(int index, IReadOnlyList<CardType> cards)
    {
        await ValidateCards(cards);

        var state = await _state.Update(state => {
            state.Entries[index] = new UserDeckState.Entry
            {
                Index = index,
                Cards = cards
            };
        });
    }

    public Task<IReadOnlyList<CardType>> GetSelected()
    {
        return _state.Read(state => {
            var selectedDeck = state.Entries[state.SelectedIndex];
            return selectedDeck.Cards;
        });
    }

    public Task<UserDeckState> GetState()
    {
        return _state.ReadValue();
    }

    public Task<IProjectionPayload> GetProjection()
    {
        return _state.Read(s => (IProjectionPayload)s);
    }

    private async Task ValidateCards(IEnumerable<CardType> cards)
    {
        var userCards = this.Grains.GetGrain<IUserCards>(this.UserId);

        foreach (var card in cards)
        {
            var hasCard = await userCards.HasCard(card);

            if (!hasCard)
                throw new InvalidOperationException($"Card {card} is not owned");
        }
    }
}