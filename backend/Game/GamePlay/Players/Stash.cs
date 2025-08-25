using Shared;

namespace Game.GamePlay;

public interface IStash
{
    int Count { get; }

    CardType Pick();
    void Add(CardType card);
    IReadOnlyList<CardType> Collect();
}

public class Stash : IStash
{
    public Stash(ValueProperty<PlayerStashState> state)
    {
        _state = state;
    }
    
    private readonly List<CardType> _cards = new();
    private readonly ValueProperty<PlayerStashState> _state;

    public int Count => _cards.Count;

    public CardType Pick()
    {
        var card = _cards.Last();
        _cards.RemoveAt(_cards.Count - 1);
        _state.Update(state => state.Count--);
        return card;
    }

    public void Add(CardType card)
    {
        _cards.Add(card);
        _state.Update(state => state.Count++);
    }

    public IReadOnlyList<CardType> Collect()
    {
        var cards = _cards.ToList();
        _cards.Clear();
        _state.Update(state => state.Count = 0);
        return cards.AsReadOnly();
    }
}