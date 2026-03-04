using Game.Session;
using Shared;

namespace Game.GamePlay;

public interface IHand
{
    IReadOnlyList<ActiveCard> Entries { get; }
    int Size { get; }

    void SetSize(int value);
    ActiveCard Add(CardType cardType);
    void Remove(Guid cardId);
}

public class Hand : IHand
{
    public Hand(ValueProperty<PlayerHandState> state)
    {
        _state = state;
    }

    private readonly ValueProperty<PlayerHandState> _state;

    private int _size;

    public IReadOnlyList<ActiveCard> Entries => _state.Value.Entries;
    public int Size => _size;

    public void SetSize(int value)
    {
        _size = value;
    }

    public ActiveCard Add(CardType cardType)
    {
        var activeCard = new ActiveCard { Id = Guid.NewGuid(), Type = cardType };
        _state.Value.Entries.Add(activeCard);
        return activeCard;
    }

    public void Remove(Guid cardId)
    {
        _state.Value.Entries.RemoveAll(c => c.Id == cardId);
    }
}