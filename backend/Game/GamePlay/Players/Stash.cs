using Shared;

namespace Game.GamePlay;

public interface IStash
{
    int Count { get; }
    IReadOnlyList<CardType> Entries { get; }

    CardType Pick();
    void Add(CardType card);
    IReadOnlyList<CardType> Collect();
}

public class Stash : IStash
{
    private readonly List<CardType> _cards = new();

    public int Count => _cards.Count;
    public IReadOnlyList<CardType> Entries => _cards;

    public CardType Pick()
    {
        var card = _cards.Last();
        _cards.RemoveAt(_cards.Count - 1);
        return card;
    }

    public void Add(CardType card)
    {
        _cards.Add(card);
    }

    public IReadOnlyList<CardType> Collect()
    {
        var cards = _cards.ToList();
        _cards.Clear();
        return cards.AsReadOnly();
    }
}
