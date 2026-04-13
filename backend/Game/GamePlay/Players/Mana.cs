using Common.Reactive;
using Shared;

namespace Game.GamePlay;

public interface IMana
{
    int Current { get; }
    int Max { get; }

    IViewableDelegate Updated { get; }

    void SetCurrent(int value);
    void SetMax(int value);

    void Use(int amount);
    void Restore();
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

    private int Bonus => (int)_modifiers.Get(PlayerModifier.AdditionalMana);

    private readonly ViewableDelegate _updated = new();

    public IViewableDelegate Updated => _updated;
    public int Current => _current.Value;
    public int Max => _max + Bonus;

    public void SetCurrent(int value)
    {
        if (value > Max)
            value = Max;

        if (value < 0)
            value = 0;

        _current.Set(value);
        _updated.Invoke();
    }

    public void SetMax(int value)
    {
        _max = value;

        if (_current.Value > _max)
            _current.Set(_max);

        _updated.Invoke();
    }

    public void Use(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));

        var newMana = _current.Value - amount;

        if (newMana < 0)
            newMana = 0;

        _current.Set(newMana);
        _updated.Invoke();
    }

    public void Restore()
    {
        _current.Set(_max);
        _updated.Invoke();
    }
}