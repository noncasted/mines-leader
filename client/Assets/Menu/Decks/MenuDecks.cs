using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using Meta;
using Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Menu.Decks
{
    public interface IMenuDecks : IUIState
    {
    }

    [DisallowMultipleComponent]
    public class MenuDecks : MonoBehaviour, IMenuDecks, IScopeSetup, ISceneService, IUIStateAsyncEnterHandler
    {
        [SerializeField] private DesignButton _backButton;

        [SerializeField] private MenuDeckCard _deckPrefab;
        [SerializeField] private MenuDeckPoolSpot _poolPrefab;
        [SerializeField] private MenuDeckIndexButton _indexPrefab;

        [SerializeField] private RectTransform _deckRoot;
        [SerializeField] private RectTransform _poolRoot;
        [SerializeField] private RectTransform _indexRoot;

        [SerializeField] private TMP_Text _avgManaText;

        private readonly List<MenuDeckCard> _deckCards = new();
        private readonly List<MenuDeckIndexButton> _indexButtons = new();
        private readonly Dictionary<CardType, MenuDeckPoolSpot> _typeToPoolSpot = new();

        private IDeckService _deckService;
        private ICardsRegistry _cardsRegistry;
        private IViewInjector _viewInjector;
        private ICardConfigs _configs;
        private IBackendProjection<SharedBackendUser.CardsProjection> _cardsProjection;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        [Inject]
        private void Construct(
            IDeckService deckService,
            ICardsRegistry cardsRegistry,
            IViewInjector viewInjector,
            ICardConfigs configs,
            IBackendProjection<SharedBackendUser.CardsProjection> cardsProjection)
        {
            _configs = configs;
            _viewInjector = viewInjector;
            _cardsRegistry = cardsRegistry;
            _deckService = deckService;
            _cardsProjection = cardsProjection;
        }

        public void Create(IScopeBuilder builder)
        {
            gameObject.SetActive(false);

            builder.RegisterComponent(this)
                   .As<IMenuDecks>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            if (_deckService.Configurations.Count == 0)
                _deckService.Updated.Advise(lifetime, () => OnInitialized(lifetime));
            else
                OnInitialized(lifetime);
        }

        private void OnInitialized(IReadOnlyLifetime lifetime)
        {
            if (_deckCards.Count != 0)
                return;

            var decksCount = _deckService.Configurations.Count;

            for (var i = 0; i < decksCount; i++)
            {
                var indexButton = Instantiate(_indexPrefab, _indexRoot);
                indexButton.Setup(i);
                _indexButtons.Add(indexButton);

                var index = i;

                indexButton.Clicked.Advise(lifetime, () => {
                    foreach (var button in _indexButtons)
                        button.Deactivate();

                    indexButton.Activate();
                    UpdateDeck(index);
                });
            }

            _indexButtons[_deckService.SelectedIndex.Value].Activate();

            foreach (var (type, definition) in _cardsRegistry.Entries)
            {
                var view = Instantiate(_poolPrefab, _poolRoot);
                _viewInjector.Inject(view.Card);
                view.Setup(definition);
                _typeToPoolSpot.Add(type, view);
            }

            var selected = _deckService.Configurations[_deckService.SelectedIndex.Value];

            foreach (var cardDefinition in selected.Cards)
            {
                var view = Instantiate(_deckPrefab, _deckRoot);
                _deckCards.Add(view);
                var poolSpot = _typeToPoolSpot[cardDefinition.Type];
                poolSpot.Card.ForceMoveToDeck(view);
                view.Changed.Advise(lifetime, OnDeckChanged);
            }

            RecalculateMana();
            UpdateDeck(_deckService.SelectedIndex.Value);
            ResizePoolRoot();

            _cardsProjection.Listen(lifetime, OnCardsUpdated);
        }

        private void OnCardsUpdated(SharedBackendUser.CardsProjection projection)
        {
            var ownedSet = new HashSet<CardType>(projection.OwnedCards);

            foreach (var (type, spot) in _typeToPoolSpot)
                spot.SetOwned(ownedSet.Contains(type));

            // Sort: owned first, then unowned
            var sorted = _typeToPoolSpot.Values
                .OrderByDescending(s => s.IsOwned)
                .ToList();

            for (var i = 0; i < sorted.Count; i++)
                sorted[i].transform.SetSiblingIndex(i);

            ResizePoolRoot();
        }

        public async UniTask OnEntered(IUIStateHandle handle)
        {
            handle.AttachGameObject(gameObject);
            ResizePoolRoot();
            await _backButton.WaitClick(handle);
            _deckService.SendUpdate().Forget();
        }

        private void UpdateDeck(int index)
        {
            _deckService.SetIndex(index);

            foreach (var spot in _typeToPoolSpot.Values)
                spot.Card.ReturnToSpot();

            var selected = _deckService.Configurations[index];

            for (var i = 0; i < selected.Cards.Count; i++)
            {
                var deck = _deckCards[i];
                var cardDefinition = selected.Cards[i];
                var poolSpot = _typeToPoolSpot[cardDefinition.Type];
                poolSpot.Card.ForceMoveToDeck(deck);
            }
        }

        private void OnDeckChanged()
        {
            var cards = new List<ICardDefinition>();

            foreach (var cardView in _deckCards)
                cards.Add(cardView.CurrentDefinition);

            var selected = _deckService.Configurations[_deckService.SelectedIndex.Value];
            selected.Update(cards);

            RecalculateMana();
        }

        private void ResizePoolRoot()
        {
            if (_poolRoot.childCount == 0)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_poolRoot);

            var lastChild = (RectTransform)_poolRoot.GetChild(_poolRoot.childCount - 1);
            var height = Mathf.Abs(lastChild.anchoredPosition.y) + lastChild.sizeDelta.y * 2;
            _poolRoot.sizeDelta = new Vector2(_poolRoot.sizeDelta.x, height);
        }

        private void RecalculateMana()
        {
            var avgMana = 0f;

            foreach (var card in _deckCards)
                avgMana += card.CurrentCard.Config.ManaCost;

            avgMana /= _deckCards.Count;
            _avgManaText.text = avgMana.ToString("F1");
        }
    }
}