using Common.Extensions;
using Game.Session;
using Shared;

namespace Game.GamePlay;

public interface IDeck
{
    int Count { get; }

    void Init(int size);
    void AddCard(CardType card);
    void RemoveCard(CardType card);
    CardType DrawCard();
    void Shuffle();
}

public class Deck : IDeck
{
    public Deck(ValueProperty<PlayerDeckState> state, IReadOnlyList<CardType> selected)
    {
        _state = state;
        _selected = selected;
    }

    private readonly ValueProperty<PlayerDeckState> _state;
    private readonly IReadOnlyList<CardType> _selected;

    public int Count => _state.Value.Queue.Count;

    public void Init(int size)
    {
        var cards = new List<CardType>(size);
        var index = 0;

        for (var i = 0; i < size; i++)
        {
            var type = _selected[index];
            cards.Add(type);

            index++;

            if (index >= _selected.Count)
                index = 0;
        }

        cards.Shuffle();

        foreach (var card in cards)
            AddCard(card);
    }

    public void AddCard(CardType card)
    {
        _state.Update(state => state.Queue.Add(card));
    }

    public void RemoveCard(CardType card)
    {
        _state.Update(state => state.Queue.Remove(card));
    }

    public CardType DrawCard()
    {
        if (_state.Value.Queue.Count == 0)
            throw new InvalidOperationException("Deck is empty");

        var card = _state.Value.Queue[0];
        _state.Update(state => state.Queue.RemoveAt(0));
        return card;
    }

    public void Shuffle()
    {
        _state.Update(state => state.Queue.Shuffle());
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