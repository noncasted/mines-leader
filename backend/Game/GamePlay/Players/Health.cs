using Common.Reactive;
using Shared;

namespace Game.GamePlay;

public interface IHealth
{
    IViewableProperty<int> Current { get; }
    int Max { get; }

    IViewableDelegate Updated { get; }

    void SetCurrent(int value);
    void SetMax(int value);
    void TakeDamage(int damage);
    void Heal(int amount);
}

public class Health : IHealth
{
    public Health(IModifiers modifiers)
    {
        _modifiers = modifiers;
    }

    private readonly IModifiers _modifiers;
    private readonly ViewableProperty<int> _current = new(0);

    private int _max;

    private int Bonus => (int)_modifiers.Get(PlayerModifier.AdditionalHealth);

    private readonly ViewableDelegate _updated = new();

    public IViewableDelegate Updated => _updated;
    public IViewableProperty<int> Current => _current;
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
        _updated.Invoke();
    }

    public void TakeDamage(int damage)
    {
        if (damage < 0)
            throw new ArgumentException("Damage cannot be negative", nameof(damage));

        var newHealth = _current.Value - damage;

        if (newHealth < 0)
            newHealth = 0;

        _current.Set(newHealth);
        _updated.Invoke();
    }

    public void Heal(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Healing amount cannot be negative", nameof(amount));

        var newHealth = _current.Value + amount;

        if (newHealth > Max)
            newHealth = Max;

        _current.Set(newHealth);
        _updated.Invoke();
    }
}