using Internal;

namespace GamePlay.Players
{
    public interface IPlayerModifiers
    {
        IViewableDictionary<PlayerModifier, float> Values { get; }

        void Set(PlayerModifier type, float value);
    }

    public static class PlayerModifiersExtensions
    {
        public static float Get(this IPlayerModifiers modifiers, PlayerModifier type)
        {
            return modifiers.Values[type];
        }

        public static void Inc(this IPlayerModifiers modifiers, PlayerModifier type)
        {
            modifiers.Set(type, modifiers.Values[type] + 1);
        }

        public static void Reset(this IPlayerModifiers modifiers, PlayerModifier type)
        {
            modifiers.Set(type, 0f);
        }
    }
}