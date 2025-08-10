using Common.Network;
using Internal;
using Shared;

namespace GamePlay.Players
{
    public interface IPlayerModifiers
    {
        IViewableDictionary<PlayerModifier, float> Values { get; }
    }
    
    public class PlayerModifiers : IPlayerModifiers, IScopeLoaded
    {
        public PlayerModifiers(NetworkProperty<PlayerModifiersState> state)
        {
            _state = state;
            
            foreach (var modifier in PlayerModifierExtensions.All)
                _values[modifier] = 0f;
        }
        
        private readonly NetworkProperty<PlayerModifiersState> _state;
        private readonly ViewableDictionary<PlayerModifier, float> _values = new();

        public IViewableDictionary<PlayerModifier, float> Values => _values;
        
        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _state.Advise(lifetime, state =>
            {
                foreach (var (type, value) in state.Values)
                    _values[type] = value;
            });    
        }
    }
}