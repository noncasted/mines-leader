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
    private readonly List<ActiveCard> _entries = new();
    private int _size;

    public IReadOnlyList<ActiveCard> Entries => _entries;
    public int Size => _size;

    public void SetSize(int value)
    {
        _size = value;
    }

    public ActiveCard Add(CardType cardType)
    {
        var activeCard = new ActiveCard { Id = Guid.NewGuid(), Type = cardType };
        _entries.Add(activeCard);
        return activeCard;
    }

    public void Remove(Guid cardId)
    {
        _entries.RemoveAll(c => c.Id == cardId);
    }
}