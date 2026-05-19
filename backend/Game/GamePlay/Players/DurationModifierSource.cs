using Shared;

namespace Game.GamePlay;

public class DurationModifierSource : IModifierSource
{
    public DurationModifierSource(PlayerModifier type, float value, string key, int turnsToEnd)
    {
        Id = Guid.NewGuid();
        Type = type;
        Value = value;
        Key = key;
        TurnsToEnd = turnsToEnd;
    }

    public Guid Id { get; }
    public PlayerModifier Type { get; }
    public float Value { get; }
    public string Key { get; }
    public int TurnsToEnd { get; private set; }

    public bool Tick()
    {
        if (TurnsToEnd > 0)
            TurnsToEnd--;

        return TurnsToEnd == 0;
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
}
