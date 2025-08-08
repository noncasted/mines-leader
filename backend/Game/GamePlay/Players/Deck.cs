using Shared;

namespace Game.GamePlay;

public interface IDeck
{
    int Count { get; }
    void AddCard(CardType card);
    CardType DrawCard();
}

public class Deck : IDeck
{
    public Deck(ValueProperty<PlayerDeckState> state)
    {
        _state = state;
    }

    private readonly ValueProperty<PlayerDeckState> _state;

    public int Count => _state.Value.Queue.Count;

    public void AddCard(CardType card)
    {
        _state.Update(state =>state.Queue.Add(card));
    }

    public CardType DrawCard()
    {
        if (_state.Value.Queue.Count == 0)
            throw new InvalidOperationException("Deck is empty");

        var card = _state.Value.Queue[0];
        _state.Update(state => state.Queue.RemoveAt(0));
        return card;
    }
}

public static class DeckExtensions
{
    public static void AddRandom(this IDeck deck, int count)
    {
        var random = Random.Shared;
        
        for (var i = 0; i < count; i++)
        {
            var index = random.Next(0, CardTypeExtensions.All.Count);
            deck.AddCard(CardTypeExtensions.All[index]);
        }
    }
}