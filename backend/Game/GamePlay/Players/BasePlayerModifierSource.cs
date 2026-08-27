using Shared;

namespace Game.GamePlay;

/// <summary>
/// Базовые хп, ходы и мана выдаются игроку бафами: на старте сессии ресурсы стоят в нуле,
/// а значения из конфига приходят такими источниками. В отличие от карточных бафов они
/// не складываются в бонус поверх базы, а задают саму базу через SetMax/SetCurrent,
/// поэтому живут вечно и не тикают.
/// </summary>
public abstract class BasePlayerModifierSource : IModifierSource
{
    protected BasePlayerModifierSource(PlayerModifier type, float value, string key)
    {
        Id = Guid.NewGuid();
        Type = type;
        Value = value;
        Key = key;
    }

    public Guid Id { get; }
    public PlayerModifier Type { get; }
    public float Value { get; }
    public string Key { get; }
    public int TurnsToEnd => -1;

    public bool Tick()
    {
        return false;
    }

    public DurationalModifierOverview GetOverview()
    {
        return new DurationalModifierOverview
        {
            SourceId = Id,
            Type = Type,
            Value = Value,
            Key = Key,
            TurnsToEnd = TurnsToEnd
        };
    }

    public abstract void Apply(IPlayer player, MoveSnapshot snapshot);
}
