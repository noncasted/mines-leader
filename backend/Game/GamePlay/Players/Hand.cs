using Shared;

namespace Game.GamePlay;

public interface IHand
{
    IReadOnlyList<CardType> Entries { get; }
    int Size { get; }

    void SetSize(int value);
    void Add(CardType card);
}

public class Hand : IHand
{
    public Hand(ValueProperty<PlayerHandState> state)
    {
        _state = state;
    }

    private readonly ValueProperty<PlayerHandState> _state;

    private int _size;

    public IReadOnlyList<CardType> Entries => _state.Value.Entries;
    public int Size => _size;

    public void SetSize(int value)
    {
        _size = value;
    }

    public void Add(CardType card)
    {
        _state.Value.Entries.Add(card);
    }
}