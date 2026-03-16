using Infrastructure;
using Infrastructure.State;
using Orleans.Concurrency;
using Shared;

namespace Meta.Users;

public interface IUserDeck : IUserGrain
{
    [Transaction]
    Task Initialize();

    [Transaction]
    Task Update(IReadOnlyDictionary<int, IReadOnlyList<CardType>> decks, int selectedIndex);

    [Transaction]
    Task Update(int index, IReadOnlyList<CardType> cards);

    [Transaction]
    Task<IReadOnlyList<CardType>> GetSelected();
}

[GenerateSerializer]
public class UserDeckState : IProjectionPayload
{
    [Id(0)] public Dictionary<int, Entry> Entries { get; } = new();
    [Id(1)] public int SelectedIndex { get; set; }

    public INetworkContext ToContext()
    {
        return new SharedBackendUser.DeckProjection()
        {
            SelectedIndex = SelectedIndex,
            Entries = Entries.ToDictionary(
                entry => entry.Key,
                entry => new SharedBackendUser.DeckProjection.Entry
                {
                    DeckIndex = entry.Value.Index,
                    Cards = entry.Value.Cards
                }
            )
        };
    }

    [GenerateSerializer]
    public class Entry
    {
        [Id(0)] public required int Index { get; init; }

        [Id(1)] public required IReadOnlyList<CardType> Cards { get; init; }
    }
}

[Reentrant]
public class UserDeck : UserGrain, IUserDeck
{
    public UserDeck([State] State<UserDeckState> state)
    {
        _state = state;
    }

    private readonly State<UserDeckState> _state;

    public async Task Initialize()
    {
        var state = await _state.Update(state =>
            {
                for (var i = 0; i < DeckOptions.MaxDecks; i++)
                {
                    var cards = new List<CardType>(DeckOptions.BaseDeck);

                    state.Entries[i] = new UserDeckState.Entry
                    {
                        Index = i,
                        Cards = cards
                    };
                }

                state.SelectedIndex = 0;
            }
        );

        await this.SendCachedProjection(state);
    }

    public async Task Update(IReadOnlyDictionary<int, IReadOnlyList<CardType>> decks, int selectedIndex)
    {
        var state = await _state.Update(state =>
            {
                foreach (var (index, cards) in decks)
                {
                    state.Entries[index] = new UserDeckState.Entry
                    {
                        Index = index,
                        Cards = cards
                    };
                }

                state.SelectedIndex = selectedIndex;
            }
        );

        await this.CacheProjection(state);
    }

    public async Task Update(int index, IReadOnlyList<CardType> cards)
    {
        var state = await _state.Update(state =>
            {
                state.Entries[index] = new UserDeckState.Entry
                {
                    Index = index,
                    Cards = cards
                };
            }
        );

        await this.CacheProjection(state);
    }

    public Task<IReadOnlyList<CardType>> GetSelected()
    {
        return _state.Read(state =>
            {
                var selectedDeck = state.Entries[state.SelectedIndex];
                return selectedDeck.Cards;
            }
        );
    }
}