using Common.Network;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class Stash : IScopeLoaded
    {
        public Stash(IStashView view, NetworkProperty<PlayerStashState> state)
        {
            _view = view;
            _state = state;
        }

        private readonly NetworkProperty<PlayerStashState> _state;
        private readonly IStashView _view;

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _state.Advise(lifetime, () => _view.UpdateAmount(_state.Value.Count));
        }
    }
}