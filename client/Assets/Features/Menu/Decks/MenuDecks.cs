using System.Collections.Generic;
using Assets.Meta;
using Cysharp.Threading.Tasks;
using Global.GameServices;
using Global.UI;
using Internal;
using Shared;
using TMPro;
using UnityEngine;
using VContainer;

namespace Menu
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

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        [Inject]
        private void Construct(
            IDeckService deckService,
            ICardsRegistry cardsRegistry,
            IViewInjector viewInjector)
        {
            _viewInjector = viewInjector;
            _cardsRegistry = cardsRegistry;
            _deckService = deckService;
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

                indexButton.Clicked.Advise(lifetime, () =>
                {
                    foreach (var button in _indexButtons)
                        button.Deactivate();

                    indexButton.Activate();
                    UpdateDeck(index);
                });
            }

            _indexButtons[_deckService.SelectedIndex.Value].Activate();

            foreach (var (type, definition) in _cardsRegistry.Cards)
            {
                var view = Instantiate(_poolPrefab, _poolRoot);
                view.Setup(definition);
                _typeToPoolSpot.Add(type, view);
                _viewInjector.Inject(view.Card);
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
        }

        public async UniTask OnEntered(IUIStateHandle handle)
        {
            handle.AttachGameObject(gameObject);
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

        private void RecalculateMana()
        {
            var avgMana = 0f;

            foreach (var card in _deckCards)
                avgMana += card.CurrentDefinition.ManaCost;

            avgMana /= _deckCards.Count;
            _avgManaText.text = avgMana.ToString("F1");
        }
    }
}