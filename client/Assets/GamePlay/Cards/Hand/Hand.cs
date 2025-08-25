using Common.Network;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public interface IHand
    {
        IHandPositions Positions { get; }
        IViewableList<ICard> Entries { get; }
        
        void Add(ICard card);
    }
    
    public class Hand : IHand
    {
        public Hand(IHandView view, NetworkProperty<PlayerHandState> state)
        {
            _view = view;
            _state = state;
        }

        private readonly IHandView _view;
        private readonly NetworkProperty<PlayerHandState> _state;
        private readonly ViewableList<ICard> _entries = new();

        public IHandPositions Positions => _view.Positions;
        public IViewableList<ICard> Entries => _entries;

        public void Add(ICard card)
        {
            var entryLifetime = _entries.Add(card);
            Positions.AddCard(entryLifetime, card);

            card.Lifetime.Listen(() => _entries.Remove(card));
        }
    }
}