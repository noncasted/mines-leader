using Shared;

namespace Game.GamePlay;

/// <summary>
/// Base HP, moves and mana are granted to the player through buffs: at session start resources are zero,
/// and config values come through such sources. Unlike card buffs they
/// do not add a bonus on top of the base but set the base itself through SetMax/SetCurrent,
/// so they live forever and do not tick.
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
    public float Value { get; protected set; }
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
