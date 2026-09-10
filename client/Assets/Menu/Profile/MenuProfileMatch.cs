using System;
using System.Collections.Generic;
using Internal;
using Meta;
using Shared;
using TMPro;
using UnityEngine;

namespace Menu.Profile
{
    /// <summary>
    /// Раскладка выбранного матча: колода соперника сверху, своя снизу,
    /// плашка результата, время матча и изменение рейтинга.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuProfileMatch : MonoBehaviour
    {
        [SerializeField] private GameObject _root;

        [SerializeField] private GameObject _win;
        [SerializeField] private GameObject _lose;

        [SerializeField] private TMP_Text _opponentName;
        [SerializeField] private TMP_Text _timer;
        [SerializeField] private TMP_Text _ratingChange;

        [SerializeField] private RectTransform _opponentCardsRoot;
        [SerializeField] private RectTransform _ownCardsRoot;

        [SerializeField] private Color _ratingGainColor = new(0.458823532f, 0.654902f, 0.2627451f, 1f);
        [SerializeField] private Color _ratingLossColor = new(0.647058845f, 0.1882353f, 0.1882353f, 1f);

        private readonly List<MenuProfileCard> _cards = new();

        private ICardsRegistry _cardsRegistry;
        private ICardDescriptionProvider _descriptions;
        private ICardConfigs _configs;

        [Inject]
        public void Construct(
            ICardsRegistry cardsRegistry,
            ICardDescriptionProvider descriptions,
            ICardConfigs configs)
        {
            _cardsRegistry = cardsRegistry;
            _descriptions = descriptions;
            _configs = configs;
        }

        public void Hide()
        {
            _root.SetActive(false);
        }

        public void Show(ProfileMatchDetails details)
        {
            _root.SetActive(true);

            var opponent = string.IsNullOrEmpty(details.OpponentName) ? "Unknown" : details.OpponentName;

            _opponentName.text = opponent;

            ApplyResult(details.Won, opponent);
            ApplyTimer(details.Time);
            ApplyRating(details.RatingChange);

            ClearCards();
            BuildCards(details.OpponentCards, _opponentCardsRoot);
            BuildCards(details.OwnCards, _ownCardsRoot);
        }

        private void ApplyResult(bool won, string opponent)
        {
            _win.SetActive(won == true);
            _lose.SetActive(won == false);
        }

        private void ApplyTimer(TimeSpan time)
        {
            _timer.text = time.Hours > 0
                ? $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}"
                : $"{time.Minutes:D2}:{time.Seconds:D2}";
        }

        private void ApplyRating(int change)
        {
            _ratingChange.text = change > 0 ? $"+{change}" : change.ToString();
            _ratingChange.color = change < 0 ? _ratingLossColor : _ratingGainColor;
        }

        private void BuildCards(IReadOnlyList<CardType> cards, RectTransform root)
        {
            if (root == null || cards == null)
                return;

            // All каждый раз собирает новый словарь, поэтому берём его один раз на раскладку.
            var configs = _configs.Value?.All;

            foreach (var type in cards)
            {
                if (_cardsRegistry.Entries.TryGetValue(type, out var definition) == false)
                    continue;

                var manaCost = configs != null && configs.TryGetValue(type, out var config) ? config.ManaCost : 0;

                var view = Instantiate(MenuPrefabs.MenuProfileCard, root);
                view.Setup(definition, _descriptions.GetDescription(type), manaCost);
                _cards.Add(view);
            }
        }

        private void ClearCards()
        {
            foreach (var card in _cards)
            {
                if (card != null)
                    Destroy(card.gameObject);
            }

            _cards.Clear();
        }
    }
}