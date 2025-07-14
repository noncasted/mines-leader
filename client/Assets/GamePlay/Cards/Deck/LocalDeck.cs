using System.Collections.Generic;
using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    public class LocalDeck : IDeck, IScopeSetup
    {
        public LocalDeck(
            ICardFactory cardFactory,
            IGameContext gameContext,
            IDeckView view,
            IStash stash,
            NetworkProperty<DeckState> state)
        {
            _cardFactory = cardFactory;
            _gameContext = gameContext;
            _view = view;
            _stash = stash;
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
        private readonly IStash _stash;
        private readonly ICardFactory _cardFactory;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _size = _gameContext.Options.DeckSize;

            for (var i = 0; i < _size; i++)
                _state.Value.Add(CardTypeExtensions.GetRandom());

            _state.MarkDirty();
        }

        public UniTask DrawCard(IReadOnlyLifetime lifetime)
        {
            if (_state.Value.Queue.Count == 0)
            {
                var stash = new List<CardType>(_stash.Reset());
                stash.Shuffle();
                _state.Value.Add(stash);
            }

            var cardType = _state.Value.Pick();
            _state.MarkDirty();

            _view.UpdateAmount(_state.Value.Queue.Count);
            return _cardFactory.Create(lifetime, cardType, _view.PickPoint);
        }
    }
}