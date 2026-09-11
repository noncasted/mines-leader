using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using Menu.Common;
using Meta;
using Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Decks
{
    public interface IMenuDecks : IUIState
    {
    }

    public class MenuDecks : IMenuDecks, IMetaSetupCompleted, IUIStateAsyncEnterHandler
    {
        public MenuDecks(
            IDecks decks,
            ICardsRegistry cardsRegistry,
            IViewInjector viewInjector,
            ICardConfigs configs,
            IBackendProjection<SharedBackendUser.CardsProjection> cardsProjection,
            IMenuCardPreviewPlayer previewPlayer,
            IUpdater updater,
            MenuDecksBindings bindings,
            MenuCanvasBindings canvasBindings)
        {
            _configs = configs;
            _viewInjector = viewInjector;
            _cardsRegistry = cardsRegistry;
            _decks = decks;
            _cardsProjection = cardsProjection;
            _previewPlayer = previewPlayer;
            _updater = updater;

            _deckRoot = bindings.Cards.RectTransform;
            _poolRoot = bindings.Pool.Viewport.Content.RectTransform;
            _indexRoot = bindings.Indexes.RectTransform;
            _avgManaText = bindings.Average.Top.Value.TextMeshProUGUI;
            _previewPopup = bindings.MenuCardPreviewPopup.MenuCardPreviewPopup;
            _previewPopupRect = bindings.MenuCardPreviewPopup.RectTransform;
            _gameObject = bindings.GameObject;
            _canvas = canvasBindings.Canvas;
            _canvasRect = canvasBindings.RectTransform;

            _gameObject.SetActive(false);
        }

        private readonly RectTransform _deckRoot;
        private readonly RectTransform _poolRoot;
        private readonly RectTransform _indexRoot;
        private readonly TMP_Text _avgManaText;
        private readonly MenuCardPreviewPopup _previewPopup;
        private readonly RectTransform _previewPopupRect;
        private readonly GameObject _gameObject;
        private readonly Canvas _canvas;
        private readonly RectTransform _canvasRect;

        private readonly List<MenuDeckCard> _deckCards = new();
        private readonly List<MenuDeckIndexButton> _indexButtons = new();
        private readonly Dictionary<CardType, MenuDeckPoolSpot> _typeToPoolSpot = new();

        private readonly IDecks _decks;
        private readonly ICardsRegistry _cardsRegistry;
        private readonly IViewInjector _viewInjector;
        private readonly ICardConfigs _configs;
        private readonly IBackendProjection<SharedBackendUser.CardsProjection> _cardsProjection;
        private readonly IMenuCardPreviewPlayer _previewPlayer;
        private readonly IUpdater _updater;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        // Колоды и конфиги карт к этому моменту уже приехали: ждать обновления колод не нужно.
        public void OnMetaSetupCompleted(IReadOnlyLifetime lifetime)
        {
            OnInitialized(lifetime);
        }

        private void OnInitialized(IReadOnlyLifetime lifetime)
        {
            if (_deckCards.Count != 0)
                return;

            var decksCount = _decks.Configurations.Count;

            for (var i = 0; i < decksCount; i++)
            {
                var indexButton = Object.Instantiate(MenuPrefabs.MenuDeckIndex, _indexRoot);
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
                var view = Object.Instantiate(MenuPrefabs.MenuPoolSpot, _poolRoot);
                _viewInjector.Inject(view.Card);
                view.Setup(definition);
                _typeToPoolSpot.Add(type, view);
                RegisterPreviewHover(view, lifetime);
                RegisterDrag(view, lifetime);
            }

            var selected = _decks.Configurations[_decks.SelectedIndex.Value];

            foreach (var cardDefinition in selected.Cards)
            {
                var view = Object.Instantiate(MenuPrefabs.MenuDeckCard, _deckRoot);
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

            card.BeginDrag();
            spot.PointerHandler.gameObject.SetActive(false);

            var cardTransform = card.Transform;
            var startPosition = cardTransform.anchoredPosition;
            Vector2 startPointer = Input.mousePosition;

            await _updater.RunUpdateAction(
                lifetime,
                () => Input.GetMouseButton(0),
                _ => {
                    Vector2 pointer = Input.mousePosition;
                    var delta = (pointer - startPointer) / _canvas.scaleFactor;
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
            var popupRect = _previewPopupRect;

            Vector3[] corners = new Vector3[4];
            spotRect.GetWorldCorners(corners);

            var worldRightCenter = (corners[2] + corners[3]) * 0.5f;

            var canvasScale = _canvasRect.localScale.x;

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
            handle.AttachGameObject(_gameObject);
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
