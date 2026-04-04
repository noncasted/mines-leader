using System;
using System.Collections.Generic;
using Shared;
using UnityEngine;

namespace Meta
{
    public interface ICardsRegistry
    {
        IReadOnlyDictionary<CardType, ICardDefinition> Entries { get; }
    }

    public class CardsRegistry : ICardsRegistry
    {
        private readonly Dictionary<CardType, ICardDefinition> _cards = new();

        public CardsRegistry()
        {
            Load();
        }

        public IReadOnlyDictionary<CardType, ICardDefinition> Entries => _cards;

        private void Load()
        {
            var textAsset = Resources.Load<TextAsset>("cards-info");
            var payload = JsonUtility.FromJson<CardsInfoPayload>(textAsset.text);

            foreach (var entry in payload.cards)
            {
                var type = (CardType)Enum.Parse(typeof(CardType), entry.type);
                var icon = string.IsNullOrEmpty(entry.icon) ? null : Resources.Load<Sprite>(entry.icon);
                var definition = new CardDefinition(type, entry.name, entry.description, icon);
                _cards[type] = definition;
            }
        }
    }
}