using Cluster.Configs;
using Common;
using Infrastructure;
using Infrastructure.State;
using Shared;

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
[GrainEventState(State = "user_deck", Lookup = "UserDeck", Key = GrainKeyType.Guid)]
public class UserDeckState : IEventStateValue, IProjectionPayload
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public Dictionary<int, Entry> Entries { get; set; } = new();
    [Id(2)] public int SelectedIndex { get; set; }

    public int Version => 0;

    public void Apply(DeckInitialized e)
    {
        foreach (var entry in e.Entries)
            Entries[entry.Key] = entry.Value;

        SelectedIndex = e.SelectedIndex;
    }

    public void Apply(DeckEntryUpdated e)
    {
        Entries[e.Index] = new Entry
        {
            Index = e.Index,
            Cards = e.Cards
        };
    }

    public void Apply(DeckSelectedIndexUpdated e)
    {
        SelectedIndex = e.SelectedIndex;
    }

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

public record DeckInitialized(Dictionary<int, UserDeckState.Entry> Entries, int SelectedIndex);

public record DeckEntryUpdated(int Index, IReadOnlyList<CardType> Cards);

public record DeckSelectedIndexUpdated(int SelectedIndex);

public class UserDeck : UserGrain, IUserDeck
{
    public UserDeck(
        [EventState] EventState<UserDeckState> state,
        IUserDeckConfig userDeckConfig)
    {
        _state = state;
        _userDeckConfig = userDeckConfig;
    }

    private readonly EventState<UserDeckState> _state;
    private readonly IUserDeckConfig _userDeckConfig;

    public async Task Initialize()
    {
        var state = await _state.Read();
        if (state.Entries.Count > 0) return;

        var entries = new Dictionary<int, UserDeckState.Entry>();
        for (var i = 0; i < DeckOptions.MaxDecks; i++)
        {
            var cards = new List<CardType>(_userDeckConfig.Value.BaseDeck);
            entries[i] = new UserDeckState.Entry
            {
                Index = i,
                Cards = cards
            };
        }

        state = await _state.Apply(new DeckInitialized(entries, 0));
        await this.SendProjection(state);
    }

    public async Task Update(IReadOnlyDictionary<int, IReadOnlyList<CardType>> decks, int selectedIndex)
    {
        foreach (var (_, cards) in decks)
            await ValidateCards(cards);

        var state = await _state.Read();
        var events = new List<object>();

        foreach (var (index, cards) in decks)
            events.Add(new DeckEntryUpdated(index, cards));

        if (selectedIndex != state.SelectedIndex)
            events.Add(new DeckSelectedIndexUpdated(selectedIndex));

        state = await _state.Apply(events.ToArray());
        await this.SendProjection(state);
    }

    public async Task Update(int index, IReadOnlyList<CardType> cards)
    {
        await ValidateCards(cards);
        var state = await _state.Apply(new DeckEntryUpdated(index, cards));
        await this.SendProjection(state);
    }

    public async Task<IReadOnlyList<CardType>> GetSelected()
    {
        var state = await _state.Read();
        return state.Entries[state.SelectedIndex].Cards;
    }

    public Task<UserDeckState> GetState()
    {
        return _state.Read();
    }

    public async Task<IProjectionPayload> GetProjection()
    {
        var state = await _state.Read();
        return state;
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
