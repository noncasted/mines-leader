using Common.Network;
using Internal;
using Shared;

namespace GamePlay.Players
{
    public interface IPlayerHealth
    {
        IViewableProperty<int> Current { get; }
        IViewableProperty<int> Max { get; }
    }
    
    public class PlayerHealth : IPlayerHealth, IScopeLoaded
    {
        public PlayerHealth(NetworkProperty<PlayerHealthState> state)
        {
            _state = state;
            state.Set(new PlayerHealthState());
        }

        private readonly NetworkProperty<PlayerHealthState> _state;

        private readonly ViewableProperty<int> _current = new();
        private readonly ViewableProperty<int> _max = new();

        public IViewableProperty<int> Current => _current;
        public IViewableProperty<int> Max => _max;
        
        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _state.View(lifetime, state =>
            {
                _current.Set(state.Current);
                _max.Set(state.Max);
            });   
        }
    }
}