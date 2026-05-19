using Common.Reactive;
using Shared;

namespace Game.GamePlay;

public interface IMana
{
    int Current { get; }
    int BaseMax { get; }
    int ResultMax { get; }

    IViewableDelegate Updated { get; }

    void SetCurrent(MoveSnapshot snapshot, int value);
    void SetMax(MoveSnapshot snapshot, int value);

    void Use(MoveSnapshot snapshot, int amount);
    void Restore(MoveSnapshot snapshot);
}

public class Mana : IMana
{
    public Mana(IModifiers modifiers)
    {
        _modifiers = modifiers;
    }

    private readonly IModifiers _modifiers;
    private readonly ViewableProperty<int> _current = new(0);

    private int _max;
    private IPlayer? _owner;

    private int Bonus => (int)_modifiers.Get(PlayerModifier.AdditionalMana);

    private readonly ViewableDelegate _updated = new();

    public IViewableDelegate Updated => _updated;
    public int Current => _current.Value;
    public int BaseMax => _max;
    public int ResultMax => _max + Bonus;

    public void BindOwner(IPlayer owner)
    {
        _owner = owner;
    }

    public void SetCurrent(MoveSnapshot snapshot, int value)
    {
        if (value > ResultMax)
            value = ResultMax;

        if (value < 0)
            value = 0;

        _current.Set(value);
        _updated.Invoke();
        Record(snapshot);
    }

    public void SetMax(MoveSnapshot snapshot, int value)
    {
        _max = value;

        if (_current.Value > ResultMax)
            _current.Set(ResultMax);

        _updated.Invoke();
        Record(snapshot);
    }

    public void Use(MoveSnapshot snapshot, int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));

        var newMana = _current.Value - amount;

        if (newMana < 0)
            newMana = 0;

        _current.Set(newMana);
        _updated.Invoke();
        Record(snapshot);
    }

    public void Restore(MoveSnapshot snapshot)
    {
        _current.Set(_max);
        _updated.Invoke();
        Record(snapshot);
    }

    private void Record(MoveSnapshot snapshot)
    {
        if (_owner != null)
            snapshot.RecordManaUpdate(_owner);
    }
}