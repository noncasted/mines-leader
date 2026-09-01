using System.Collections.Generic;
using Shared;

namespace Meta
{
    public interface ICardsCollection
    {
        IReadOnlyDictionary<CardType, ICardDefinition> Entries { get; }
    }

    public class CardsCollection : ICardsCollection
    {
        private readonly Dictionary<CardType, ICardDefinition> _entries = new();

        public IReadOnlyDictionary<CardType, ICardDefinition> Entries => _entries;
    }
}