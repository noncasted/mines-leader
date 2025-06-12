using Common;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;
using Shared;

namespace Backend.Users;

[Reentrant]
public class UserDeck : UserGrain, IUserDeck
{
    public UserDeck([States.UserDeck] ITransactionalState<UserDeckState> state)
    {
        _state = state;
    }

    private readonly ITransactionalState<UserDeckState> _state;

    public async Task Initialize()
    {
        var state = await _state.Update(state =>
        {
            for (var i = 0; i < DeckOptions.MaxDecks; i++)
            {
                var deckIndex = i;
                var cards = new List<CardType>(DeckOptions.BaseDeck);

                state.Entries[deckIndex] = new UserDeckState.Entry
                {
                    Index = deckIndex,
                    Cards = cards
                };
            }

            state.SelectedIndex = 0;
        });

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
        });
        
        await this.CacheProjection(state);
    }
}