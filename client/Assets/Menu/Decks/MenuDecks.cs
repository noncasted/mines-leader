using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using Meta;
using Shared;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [SerializeField] private MenuDeckIndexButton _indexPrefab;

        [SerializeField] private RectTransform _deckRoot;
        [SerializeField] private RectTransform _poolRoot;
        [SerializeField] private RectTransform _indexRoot;

        [SerializeField] private TMP_Text _avgManaText;
        [SerializeField] private MenuCardPreviewPopup _previewPopup;

        private readonly List<MenuDeckCard> _deckCards = new();
        private readonly List<MenuDeckIndexButton> _indexButtons = new();
        private readonly Dictionary<CardType, MenuDeckPoolSpot> _typeToPoolSpot = new();

        private IDecks _decks;
        private ICardsRegistry _cardsRegistry;
        private IViewInjector _viewInjector;
        private ICardConfigs _configs;
        private IBackendProjection<SharedBackendUser.CardsProjection> _cardsProjection;
        private IMenuCardPreviewPlayer _previewPlayer;
        private IUpdater _updater;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        [Inject]
        internal void Construct(
            IDecks decks,
            ICardsRegistry cardsRegistry,
            IViewInjector viewInjector,
            ICardConfigs configs,
            IBackendProjection<SharedBackendUser.CardsProjection> cardsProjection,
            IMenuCardPreviewPlayer previewPlayer,
            IUpdater updater)
        {
            _configs = configs;
            _viewInjector = viewInjector;
            _cardsRegistry = cardsRegistry;
            _decks = decks;
            _cardsProjection = cardsProjection;
            _previewPlayer = previewPlayer;
            _updater = updater;
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
            if (_decks.Configurations.Count == 0)
                _decks.Updated.Advise(lifetime, () => OnInitialized(lifetime));
            else
                OnInitialized(lifetime);
        }

        private void OnInitialized(IReadOnlyLifetime lifetime)
        {
            if (_deckCards.Count != 0)
                return;

            var decksCount = _decks.Configurations.Count;

            for (var i = 0; i < decksCount; i++)
            {
                var indexButton = Instantiate(MenuPrefabs.MenuDeckIndex, _indexRoot);
                indexButton.Setup(i);
                _indexButtons.Add(indexButton);

                var index = i;

                indexButton.Clicked.Advise(lifetime, () => {
                    foreach (var button in _indexButtons)
                        button.Deactivate();

                    indexButton.Activate();
                    UpdateDeck(index);
                    _decks.SendUpdate().Forget();
                });
            }

            _indexButtons[_decks.SelectedIndex.Value].Activate();

            foreach (var (type, definition) in _cardsRegistry.Entries)
            {
                var view = Instantiate(MenuPrefabs.MenuPoolSpot, _poolRoot);
                _viewInjector.Inject(view.Card);
                view.Setup(definition);
                _typeToPoolSpot.Add(type, view);
                RegisterPreviewHover(view, lifetime);
                RegisterDrag(view, lifetime);
            }

            var selected = _decks.Configurations[_decks.SelectedIndex.Value];

            foreach (var cardDefinition in selected.Cards)
            {
                var view = Instantiate(MenuPrefabs.MenuDeckCard, _deckRoot);
                _deckCards.Add(view);
                var poolSpot = _typeToPoolSpot[cardDefinition.Type];
                poolSpot.ForceMoveToDeck(view);
                view.Changed.Advise(lifetime, OnDeckChanged);
            }

            RecalculateMana();
            UpdateDeck(_decks.SelectedIndex.Value);
            ResizePoolRoot();

            _cardsProjection.View(lifetime, OnCardsUpdated);
        }

        private void RegisterDrag(MenuDeckPoolSpot spot, IReadOnlyLifetime lifetime)
        {
            spot.PointerHandler.IsDragging.ViewTrue(lifetime, _ => OnDragStarted(spot, lifetime).Forget());
        }

        private async UniTask OnDragStarted(MenuDeckPoolSpot spot, IReadOnlyLifetime lifetime)
        {
            if (spot.IsOwned == false)
                return;
            
            if (spot.Card.gameObject.activeInHierarchy == false)
                return;

            var card = spot.Card;
            var canvas = GetComponentInParent<Canvas>();

            card.BeginDrag();
            spot.PointerHandler.gameObject.SetActive(false);

            var cardTransform = card.Transform;
            var startPosition = cardTransform.anchoredPosition;
            var startPointer = Mouse.current.position.ReadValue();

            await _updater.RunUpdateAction(
                lifetime,
                () => Mouse.current.leftButton.isPressed,
                _ => {
                    var pointer = Mouse.current.position.ReadValue();
                    var delta = (pointer - startPointer) / canvas.scaleFactor;
                    cardTransform.anchoredPosition = startPosition + delta;
                });

            spot.PointerHandler.gameObject.SetActive(true);

            foreach (var deckCard in _deckCards)
            {
                if (deckCard.PointerHandler.IsHovered.Value == false)
                    continue;

                deckCard.OnCardDropped(spot);
                card.gameObject.SetActive(false);
                return;
            }

            spot.ReturnToSpot();
        }

        private void RegisterPreviewHover(MenuDeckPoolSpot spot, IReadOnlyLifetime lifetime)
        {
            return;
            spot.PointerHandler.IsHovered.Advise(lifetime, isHovered => {
                if (spot.Card.gameObject.activeInHierarchy == false)
                    return;

                if (isHovered == false)
                    return;

                var hoverLifetime = spot.PointerHandler.IsHovered.ValueLifetime;
                ShowPreview(hoverLifetime, spot);
            });
        }

        private void ShowPreview(IReadOnlyLifetime lifetime, MenuDeckPoolSpot spot)
        {
            var card = spot.Card;
            var type = card.CardDefinition.Type;

            if (_previewPlayer.HasPreview(type) == false)
                return;

            _previewPlayer.Play(lifetime, type).Forget();
            var rt = _previewPlayer.PreviewTexture;

            _previewPopup.Show(rt);
            PositionPreview(spot);

            lifetime.Listen(() => _previewPopup.Hide());
        }

        private void PositionPreview(MenuDeckPoolSpot spot)
        {
            var spotRect = spot.Transform;
            var popupRect = _previewPopup.GetComponent<RectTransform>();

            Vector3[] corners = new Vector3[4];
            spotRect.GetWorldCorners(corners);

            var worldRightCenter = (corners[2] + corners[3]) * 0.5f;

            var canvas = GetComponentInParent<Canvas>();
            var canvasScale = 1f;
            canvasScale = ((RectTransform)canvas.transform).localScale.x;

            worldRightCenter.x += 100f / canvasScale;

            var targetLocal = popupRect.parent.InverseTransformPoint(worldRightCenter);

            var size = popupRect.sizeDelta;
            var popupScale = popupRect.localScale;
            var pivot = popupRect.pivot;

            var centerOffset = new Vector3(
                (0.5f - pivot.x) * size.x * popupScale.x,
                (0.5f - pivot.y) * size.y * popupScale.y,
                0f);

            popupRect.localPosition = targetLocal - centerOffset;
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
        }

        private void UpdateDeck(int index)
        {
            _decks.SetIndex(index);

            foreach (var spot in _typeToPoolSpot.Values)
                spot.ReturnToSpot();

            var selected = _decks.Configurations[index];

            for (var i = 0; i < selected.Cards.Count; i++)
            {
                var deck = _deckCards[i];
                var cardDefinition = selected.Cards[i];
                var poolSpot = _typeToPoolSpot[cardDefinition.Type];
                poolSpot.ForceMoveToDeck(deck);
            }
        }

        private void OnDeckChanged()
        {
            var cards = new List<ICardDefinition>();

            foreach (var cardView in _deckCards)
                cards.Add(cardView.CurrentDefinition);

            var selected = _decks.Configurations[_decks.SelectedIndex.Value];
            selected.Update(cards);

            RecalculateMana();
            _decks.SendUpdate().Forget();
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