using System.Collections.Generic;

namespace Meta
{
    public interface IDeckConfiguration
    {
        IReadOnlyList<ICardDefinition> Cards { get; }
        int Index { get; }

        void Update(IReadOnlyList<ICardDefinition> cards);
    }

    public class DeckConfiguration : IDeckConfiguration
    {
        public DeckConfiguration(int index, IReadOnlyList<ICardDefinition> cards)
        {
            Index = index;
            _cards = new List<ICardDefinition>(cards);
        }

        private readonly List<ICardDefinition> _cards;

        public int Index { get; }
        public IReadOnlyList<ICardDefinition> Cards => _cards;

        public void Update(IReadOnlyList<ICardDefinition> cards)
        {
            _cards.Clear();
            _cards.AddRange(cards);
        }
    }
}