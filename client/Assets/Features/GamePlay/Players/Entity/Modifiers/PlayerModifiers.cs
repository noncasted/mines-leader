using Internal;

namespace GamePlay.Players
{
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
}