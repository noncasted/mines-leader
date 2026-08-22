using Common.Extensions;
using Shared;

namespace Game.GamePlay;

public interface IDeck
{
    int Count { get; }

    void Init(int size);
    void Replace(IReadOnlyList<CardType> cards);
    void AddCard(CardType card);
    void InsertTop(CardType card);
    void RemoveCard(CardType card);
    CardType Peek(int index);
    CardType DrawCard();
    void Shuffle();
}

public class Deck : IDeck
{
    public Deck(IReadOnlyList<CardType> selected)
    {
        _selected = selected;
    }

    private readonly IReadOnlyList<CardType> _selected;
    private readonly List<CardType> _queue = new();

    public int Count => _queue.Count;

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

    public void Replace(IReadOnlyList<CardType> cards)
    {
        _queue.Clear();

        foreach (var card in cards)
            _queue.Add(card);
    }

    public void AddCard(CardType card)
    {
        _queue.Add(card);
    }

    public void InsertTop(CardType card)
    {
        _queue.Insert(0, card);
    }

    public void RemoveCard(CardType card)
    {
        _queue.Remove(card);
    }

    public CardType Peek(int index)
    {
        return _queue[index];
    }

    public CardType DrawCard()
    {
        if (_queue.Count == 0)
            throw new InvalidOperationException("Deck is empty");

        var card = _queue[0];
        _queue.RemoveAt(0);
        return card;
    }

    public void Shuffle()
    {
        _queue.Shuffle();
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