using Common.Network;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface IDeck
    {
        IDeckView View { get; }
    }
    
    public class Deck : IScopeLoaded, IDeck
    {
        public Deck(IDeckView view, NetworkProperty<PlayerDeckState> state)
        {
            _view = view;
            _state = state;
        }

        private int _size;

        private readonly NetworkProperty<PlayerDeckState> _state;
        private readonly IDeckView _view;

        public IDeckView View => _view;
        
        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _state.Advise(lifetime, () =>
            {
                _view.UpdateAmount(_state.Value.Queue.Count);
            });
        }
    }
}