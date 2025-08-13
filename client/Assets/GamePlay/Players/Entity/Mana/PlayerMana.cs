using Common.Network;
using Internal;
using Shared;

namespace GamePlay.Players
{
    public interface IPlayerMana
    {
        IViewableProperty<int> Current { get; }
        IViewableProperty<int> Max { get; }
    }

    public class PlayerMana : IPlayerMana, IScopeLoaded
    {
        public PlayerMana(NetworkProperty<PlayerManaState> state)
        {
            _state = state;
        }

        private readonly NetworkProperty<PlayerManaState> _state;

        private readonly ViewableProperty<int> _current = new();
        private readonly ViewableProperty<int> _max = new();

        private float _regenerationTimer;

        public IViewableProperty<int> Current => _current;
        public IViewableProperty<int> Max => _max;

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _state.View(lifetime, state =>
                {
                    _current.Set(state.Current);
                    _max.Set(state.Max);
                }
            );
        }
    }
}