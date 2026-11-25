using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using Meta;
using Menu.Common;
using Shared;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Decks
{
    // Every card of the game in a spot: owned ones first, an owned card is dragged from its spot into the deck.
    public class MenuDeckPool
    {
        public MenuDeckPool(
            ICardsRegistry cardsRegistry,
            IViewInjector viewInjector,
            IBackendProjection<SharedBackendUser.CardsProjection> cardsProjection,
            IUpdater updater,
            MenuDeckPoolPreview preview,
            RectTransform root,
            Canvas canvas)
        {
            _cardsRegistry = cardsRegistry;
            _viewInjector = viewInjector;
            _cardsProjection = cardsProjection;
            _updater = updater;
            _preview = preview;
            _root = root;
            _canvas = canvas;
        }

        private readonly ICardsRegistry _cardsRegistry;
        private readonly IViewInjector _viewInjector;
        private readonly IBackendProjection<SharedBackendUser.CardsProjection> _cardsProjection;
        private readonly IUpdater _updater;
        private readonly MenuDeckPoolPreview _preview;
        private readonly RectTransform _root;
        private readonly Canvas _canvas;
        private readonly Dictionary<CardType, MenuDeckPoolSpot> _typeToPoolSpot = new();

        // tryDrop places a dragged card into the deck: false - the card goes back to its spot.
        public void Create(IReadOnlyLifetime lifetime, System.Func<MenuDeckPoolSpot, bool> tryDrop)
        {
            foreach (var (type, definition) in _cardsRegistry.Entries)
            {
                var view = Object.Instantiate(MenuPrefabs.MenuPoolSpot, _root);
                _viewInjector.Inject(view.Card);
                view.Setup(definition);
                _typeToPoolSpot.Add(type, view);
                _preview.RegisterHover(view, lifetime);
                RegisterDrag(view, lifetime, tryDrop);
            }
        }

        public void ViewOwned(IReadOnlyLifetime lifetime)
        {
            _cardsProjection.View(lifetime, OnCardsUpdated);
        }

        public MenuDeckPoolSpot Spot(CardType type)
        {
            return _typeToPoolSpot[type];
        }

        public void ReturnAll()
        {
            foreach (var spot in _typeToPoolSpot.Values)
                spot.ReturnToSpot();
        }

        public void Resize()
        {
            if (_root.childCount == 0)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_root);

            var lastChild = (RectTransform)_root.GetChild(_root.childCount - 1);
            var height = Mathf.Abs(lastChild.anchoredPosition.y) + lastChild.sizeDelta.y * 2;
            _root.sizeDelta = new Vector2(_root.sizeDelta.x, height);
        }

        private void RegisterDrag(MenuDeckPoolSpot spot, IReadOnlyLifetime lifetime, System.Func<MenuDeckPoolSpot, bool> tryDrop)
        {
            spot.PointerHandler.IsDragging.ViewTrue(lifetime, _ => OnDragStarted(spot, lifetime, tryDrop).Forget());
        }

        private async UniTask OnDragStarted(MenuDeckPoolSpot spot, IReadOnlyLifetime lifetime, System.Func<MenuDeckPoolSpot, bool> tryDrop)
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

            // The drag is canceled when the decks are closed mid-drag: the card goes back to its spot, nothing is dropped.
            var isCanceled = await _updater.RunUpdateAction(
                lifetime,
                () => Input.GetMouseButton(0),
                _ => {
                    Vector2 pointer = Input.mousePosition;
                    var delta = (pointer - startPointer) / _canvas.scaleFactor;
                    cardTransform.anchoredPosition = startPosition + delta;
                }).SuppressCancellationThrow();

            spot.PointerHandler.gameObject.SetActive(true);

            if (isCanceled == true)
            {
                spot.ReturnToSpot();
                return;
            }

            if (tryDrop(spot) == true)
            {
                card.gameObject.SetActive(false);
                return;
            }

            spot.ReturnToSpot();
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

            Resize();
        }
    }
}
