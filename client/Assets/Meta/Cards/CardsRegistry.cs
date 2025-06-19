using System.Collections.Generic;
using Internal;
using Shared;

namespace Meta
{
    public interface ICardsRegistry : IScriptableRegistry<CardDefinition>
    {
        IReadOnlyDictionary<CardType, ICardDefinition> Cards { get; }
    }
    
    public class CardsRegistry : ScriptableRegistry<CardDefinition>, ICardsRegistry
    {
        private readonly Dictionary<CardType, ICardDefinition> _cards = new();
        
        public IReadOnlyDictionary<CardType, ICardDefinition> Cards => _cards;
        
        protected override void OnInitialize()
        {
            foreach (var definition in Objects)
                _cards[definition.Type] = definition;
        }
    }
}