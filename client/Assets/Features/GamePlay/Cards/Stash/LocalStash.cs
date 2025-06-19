using System;
using System.Collections.Generic;
using Common.Network.Common;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class LocalStash : IStash
    {
        public LocalStash(
            ICardFactory cardFactory,
            IStashView view,
            NetworkProperty<StashState> state)
        {
            _cardFactory = cardFactory;
            _view = view;
            _state = state;
            
            state.Set(new StashState()
            {
                Stack = new List<CardType>(),
            });
        }

        private readonly NetworkProperty<StashState> _state;
        private readonly IStashView _view;
        private readonly ICardFactory _cardFactory;

        public bool IsEmpty => _state.Value.Stack.Count == 0;

        public void AddCard(CardType type)
        {
            _state.Value.Stack.Add(type); 
            _state.MarkDirty();
            _view.UpdateAmount(_state.Value.Stack.Count);
        }

        public UniTask DrawCard(IReadOnlyLifetime lifetime)
        {
            if (_state.Value.Stack.Count == 0)
                throw new Exception("Stash is empty");

            var cardType = _state.Value.Pick();
            _state.MarkDirty();
            
            _view.UpdateAmount(_state.Value.Stack.Count);
            return _cardFactory.Create(lifetime, cardType, _view.PickPoint);
        }
    }
}