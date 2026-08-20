using Common.Reactive;
using Shared;

namespace Game.GamePlay;

public interface IHealth
{
    int Current { get; }
    int BaseMax { get; }
    int ResultMax { get; }

    IViewableDelegate Updated { get; }

    void SetCurrent(MoveSnapshot snapshot, int value);
    void SetMax(MoveSnapshot snapshot, int value);
    void TakeDamage(MoveSnapshot snapshot, int damage);
    void Heal(MoveSnapshot snapshot, int amount);
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
    private IPlayer? _owner;

    private int Bonus => (int)_modifiers.Get(PlayerModifier.AdditionalHealth);

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

    public void TakeDamage(MoveSnapshot snapshot, int damage)
    {
        if (damage < 0)
            throw new ArgumentException("Damage cannot be negative", nameof(damage));

        var newHealth = _current.Value - damage;

        if (newHealth < 0)
            newHealth = 0;

        _current.Set(newHealth);
        _updated.Invoke();
        Record(snapshot);
    }

    public void Heal(MoveSnapshot snapshot, int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Healing amount cannot be negative", nameof(amount));

        var newHealth = _current.Value + amount;

        if (newHealth > ResultMax)
            newHealth = ResultMax;

        _current.Set(newHealth);
        _updated.Invoke();
        Record(snapshot);
    }

    private void Record(MoveSnapshot snapshot)
    {
        if (_owner != null)
            snapshot.RecordHealthUpdate(_owner);
    }
}