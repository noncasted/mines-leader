using Common.Network;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class Deck : IScopeLoaded
    {
        public Deck(IDeckView view, NetworkProperty<PlayerDeckState> state)
        {
            _view = view;
            _state = state;
        }

        private int _size;

        private readonly NetworkProperty<PlayerDeckState> _state;
        private readonly IDeckView _view;

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _state.Advise(lifetime, () => _view.UpdateAmount(_state.Value.Queue.Count));
        }
    }
}