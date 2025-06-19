using System;
using Common.Network.Common;
using Cysharp.Threading.Tasks;
using Internal;

namespace GamePlay.Cards
{
    public class RemoteDeck : IDeck, IScopeLoaded
    {
        public RemoteDeck(
            IDeckView view,
            NetworkProperty<DeckState> state)
        {
            _view = view;
            _state = state;
        }

        private int _size;

        private readonly NetworkProperty<DeckState> _state;
        private readonly IDeckView _view;

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _state.Advise(lifetime, () => _view.UpdateAmount(_state.Value.Queue.Count));
        }
        
        public UniTask DrawCard(IReadOnlyLifetime lifetime)
        {
            throw new Exception();
        }
    }
}