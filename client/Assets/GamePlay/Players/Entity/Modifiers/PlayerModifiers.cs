using Internal;

namespace GamePlay.Players
{
    public interface IPlayerModifiers
    {
        IViewableDictionary<PlayerModifier, float> Values { get; }

        void Set(PlayerModifier type, float value);
    }
    
    public class PlayerModifiers : IPlayerModifiers
    {
        public PlayerModifiers()
        {
            foreach (var modifier in PlayerModifierExtensions.All)
                _values[modifier] = 0f;
        }
        
        private readonly ViewableDictionary<PlayerModifier, float> _values = new();

        public IViewableDictionary<PlayerModifier, float> Values => _values;
        
        public void Set(PlayerModifier type, float value)
        {
            _values[type] = value;
        }
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