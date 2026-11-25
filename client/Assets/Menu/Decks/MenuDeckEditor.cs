using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Meta;
using Menu.Common;
using TMPro;
using UnityEngine;

namespace Menu.Decks
{
    // The cards of the selected deck: filled from the pool, a change is saved to the decks and sent.
    public class MenuDeckEditor
    {
        public MenuDeckEditor(IDecks decks, MenuDeckPool pool, RectTransform root, TMP_Text avgManaText)
        {
            _decks = decks;
            _pool = pool;
            _root = root;
            _avgManaText = avgManaText;
        }

        private readonly IDecks _decks;
        private readonly MenuDeckPool _pool;
        private readonly RectTransform _root;
        private readonly TMP_Text _avgManaText;
        private readonly List<MenuDeckCard> _cards = new();

        public bool IsCreated => _cards.Count != 0;

        public void Create(IReadOnlyLifetime lifetime)
        {
            var selected = _decks.Configurations[_decks.SelectedIndex.Value];

            foreach (var cardDefinition in selected.Cards)
            {
                var view = Object.Instantiate(MenuPrefabs.MenuDeckCard, _root);
                _cards.Add(view);
                var poolSpot = _pool.Spot(cardDefinition.Type);
                poolSpot.ForceMoveToDeck(view);
                view.Changed.Advise(lifetime, OnDeckChanged);
            }

            RecalculateMana();
        }

        public void Show(int index)
        {
            _decks.SetIndex(index);
            _pool.ReturnAll();

            var selected = _decks.Configurations[index];

            for (var i = 0; i < selected.Cards.Count; i++)
            {
                var deck = _cards[i];
                var cardDefinition = selected.Cards[i];
                var poolSpot = _pool.Spot(cardDefinition.Type);
                poolSpot.ForceMoveToDeck(deck);
            }
        }

        // A card of the pool dropped over a card of the deck replaces it.
        public bool TryDrop(MenuDeckPoolSpot spot)
        {
            foreach (var deckCard in _cards)
            {
                if (deckCard.PointerHandler.IsHovered.Value == false)
                    continue;

                deckCard.OnCardDropped(spot);
                return true;
            }

            return false;
        }

        private void OnDeckChanged()
        {
            var cards = new List<ICardDefinition>();

            foreach (var cardView in _cards)
                cards.Add(cardView.CurrentDefinition);

            var selected = _decks.Configurations[_decks.SelectedIndex.Value];
            selected.Update(cards);

            RecalculateMana();
            _decks.SendUpdate().Forget();
        }

        private void RecalculateMana()
        {
            var avgMana = 0f;

            foreach (var card in _cards)
                avgMana += card.CurrentCard.Config.ManaCost;

            avgMana /= _cards.Count;
            _avgManaText.text = avgMana.ToString("F1");
        }
    }
}
