using Internal;

namespace GamePlay.Cards
{
    public class Hand : IHand
    {
        public Hand(IHandView view)
        {
            _view = view;
        }

        private readonly IHandView _view;
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