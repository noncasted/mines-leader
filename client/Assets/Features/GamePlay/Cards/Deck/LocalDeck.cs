using System;
using System.Collections.Generic;
using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.GameServices;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class LocalDeck : IDeck, IScopeSetup
    {
        public LocalDeck(
            ICardFactory cardFactory,
            IGameContext gameContext,
            IDeckView view,
            NetworkProperty<DeckState> state)
        {
            _cardFactory = cardFactory;
            _gameContext = gameContext;
            _view = view;
            _state = state;

            state.Set(new DeckState()
            {
                Queue = new List<CardType>(),
            });
        }

        private int _size;

        private readonly NetworkProperty<DeckState> _state;
        private readonly IGameContext _gameContext;
        private readonly IDeckView _view;
        private readonly ICardFactory _cardFactory;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _size = _gameContext.Options.DeckSize;

            for (var i = 0; i < _size; i++)
                _state.Value.Queue.Add(CardTypeExtensions.GetRandom());

            _state.MarkDirty();
        }

        public UniTask DrawCard(IReadOnlyLifetime lifetime)
        {
            if (_state.Value.Queue.Count == 0)
                throw new Exception("Deck is empty");

            var cardType = _state.Value.Pick();
            _state.MarkDirty();

            _view.UpdateAmount(_state.Value.Queue.Count);
            return _cardFactory.Create(lifetime, cardType, _view.PickPoint);
        }
    }
}