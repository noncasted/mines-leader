using Common.Network.Common;
using Internal;

namespace GamePlay.Players
{
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
        
        public void SetCurrent(int amount)
        {
            _state.Value.Current = amount;
            _state.MarkDirty();
        }

        public void SetMax(int amount)
        {
            _state.Value.Max = amount;
            _state.MarkDirty();
        }
    }
}