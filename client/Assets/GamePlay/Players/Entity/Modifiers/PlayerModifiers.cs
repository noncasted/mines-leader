using Internal;
using Shared;

namespace GamePlay.Players
{
    public interface IPlayerModifiers
    {
        IViewableDictionary<PlayerModifier, float> Values { get; }

        void Set(PlayerModifier modifier, float value);
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

        public void Set(PlayerModifier modifier, float value)
        {
            _values[modifier] = value;
        }
    }
}
