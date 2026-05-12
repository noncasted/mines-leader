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

public class DeckInitialized
{
    public Dictionary<int, UserDeckState.Entry> Entries { get; set; } = new();
    public int SelectedIndex { get; set; }
}

public class DeckEntryUpdated
{
    public int Index { get; set; }
    public IReadOnlyList<CardType> Cards { get; set; } = new List<CardType>();
}

public class DeckSelectedIndexUpdated
{
    public int SelectedIndex { get; set; }
}

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
        await _state.Read();
        if (_state.Value.Entries.Count > 0) return;

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

        await _state.Append(new DeckInitialized
        {
            Entries = entries,
            SelectedIndex = 0
        });
        await _state.WriteSession();

        await this.SendProjection(_state.Value);
    }

    public async Task Update(IReadOnlyDictionary<int, IReadOnlyList<CardType>> decks, int selectedIndex)
    {
        foreach (var (_, cards) in decks)
            await ValidateCards(cards);

        await _state.Read();
        
        foreach (var (index, cards) in decks)
        {
            await _state.Append(new DeckEntryUpdated
            {
                Index = index,
                Cards = cards
            });
        }

        if (selectedIndex != _state.Value.SelectedIndex)
        {
            await _state.Append(new DeckSelectedIndexUpdated
            {
                SelectedIndex = selectedIndex
            });
        }

        await _state.WriteSession();

        await this.SendProjection(_state.Value);
    }

    public async Task Update(int index, IReadOnlyList<CardType> cards)
    {
        await ValidateCards(cards);

        await _state.Read();
        await _state.Append(new DeckEntryUpdated
        {
            Index = index,
            Cards = cards
        });
        await _state.WriteSession();

        await this.SendProjection(_state.Value);
    }

    public Task<IReadOnlyList<CardType>> GetSelected()
    {
        return _state.ReadAndGetSelected();
    }

    public Task<UserDeckState> GetState()
    {
        return _state.ReadAndReturn();
    }

    public Task<IProjectionPayload> GetProjection()
    {
        return Task.FromResult((IProjectionPayload)_state.Value);
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

public static class UserDeckEventStateExtensions
{
    public static async Task<IReadOnlyList<CardType>> ReadAndGetSelected(this EventState<UserDeckState> state)
    {
        await state.Read();
        var selectedDeck = state.Value.Entries[state.Value.SelectedIndex];
        return selectedDeck.Cards;
    }

    public static async Task<UserDeckState> ReadAndReturn(this EventState<UserDeckState> state)
    {
        await state.Read();
        return state.Value;
    }
}
