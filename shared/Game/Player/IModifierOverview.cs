using MemoryPack;

namespace Shared
{

    public interface IModifierOverview
    {
        PlayerModifier Type { get; }
        float Value { get; }
        string Key { get; }
    }
}