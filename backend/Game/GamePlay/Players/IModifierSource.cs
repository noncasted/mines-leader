using Shared;

namespace Game.GamePlay;

public interface IModifierSource
{
    Guid Id { get; }
    PlayerModifier Type { get; }
    float Value { get; }
    string Key { get; }
    int TurnsToEnd { get; }

    bool Tick();
    DurationalModifierOverview GetOverview();
}
