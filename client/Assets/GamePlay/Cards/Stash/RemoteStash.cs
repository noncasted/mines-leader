using System;
using Common.Network;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class RemoteStash : IStash, IScopeLoaded
    {
        public RemoteStash(
            IStashView view,
            NetworkProperty<StashState> state)
        {
            _view = view;
            _state = state;
        }

        private readonly NetworkProperty<StashState> _state;
        private readonly IStashView _view;

        public bool IsEmpty => _state.Value.Stack.Count == 0;

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _state.Advise(lifetime, () => _view.UpdateAmount(_state.Value.Stack.Count));
        }

        public void AddCard(CardType type)
        {
            throw new Exception();
        }

        public UniTask DrawCard(IReadOnlyLifetime lifetime)
        {
            throw new Exception();
        }
    }
}