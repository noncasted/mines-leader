using Shared;

namespace Game.GamePlay;

public interface IModifiers
{
    IReadOnlyDictionary<PlayerModifier, float> Values { get; }

    void Set(PlayerModifier type, float value);
}

public class Modifiers : IModifiers
{
    private readonly Dictionary<PlayerModifier, float> _values = new();

    public IReadOnlyDictionary<PlayerModifier, float> Values => _values;

    public void Set(PlayerModifier type, float value)
    {
        _values[type] = value;
    }
}

public static class PlayerModifiersExtensions
{
    public static float Get(this IModifiers modifiers, PlayerModifier type)
    {
        return modifiers.Values[type];
    }

    public static void Inc(this IModifiers modifiers, PlayerModifier type)
    {
        modifiers.Set(type, modifiers.Values[type] + 1);
    }

    public static void Reset(this IModifiers modifiers, PlayerModifier type)
    {
        modifiers.Set(type, 0f);
    }
}