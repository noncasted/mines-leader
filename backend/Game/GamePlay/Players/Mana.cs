using Common.Reactive;
using Game.Session;
using Shared;

namespace Game.GamePlay;

public interface IMana
{
    int Current { get; }
    int Max { get; }

    void SetCurrent(int value);
    void SetMax(int value);

    void Use(int amount);
    void Restore();
}

public class Mana : IMana
{
    public Mana(ValueProperty<PlayerManaState> state)
    {
        _state = state;
    }

    private readonly ValueProperty<PlayerManaState> _state;
    private readonly ViewableProperty<int> _current = new(0);

    private int _max;

    public int Current => _current.Value;
    public int Max => _max;

    public void SetCurrent(int value)
    {
        if (value > _max)
            value = _max;

        if (value < 0)
            value = 0;
        
        _current.Set(value);
        SyncState();
    }

    public void SetMax(int value)
    {
        _max = value;
        SyncState();
    }

    public void Use(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));

        var newMana = _current.Value - amount;

        if (newMana < 0)
            newMana = 0;

        _current.Set(newMana);
        SyncState();
    }

    public void Restore()
    {
        _current.Set(_max);
        SyncState();
    }

    private void SyncState()
    {
        _state.Set(new PlayerManaState()
            {
                Current = _current.Value,
                Max = _max
            }
        );
    }
}