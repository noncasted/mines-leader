using Shared;

namespace Game.GamePlay;

public interface IHand
{
    IReadOnlyList<CardType> Entries { get; }
    int Size { get; }

    void Add(CardType card);
}

public class Hand : IHand
{
    public Hand(ValueProperty<PlayerHandState> state, int size)
    {
        _state = state;
        Size = size;
    }

    private readonly ValueProperty<PlayerHandState> _state;

    public IReadOnlyList<CardType> Entries => _state.Value.Entries;
    public int Size { get; }
    
    public void Add(CardType card)
    {
        _state.Value.Entries.Add(card);
    }
}