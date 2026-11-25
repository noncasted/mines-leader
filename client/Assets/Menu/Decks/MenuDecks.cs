using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using Menu.Common;
using Meta;
using Shared;
using UnityEngine;

namespace Menu.Decks
{
    public interface IMenuDecks : IUIState
    {
    }

    // The decks screen: the deck buttons, the pool of cards and the editor of the selected deck.
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
            _decks = decks;
            _gameObject = bindings.GameObject;

            var preview = new MenuDeckPoolPreview(
                previewPlayer,
                bindings.MenuCardPreviewPopup.MenuCardPreviewPopup,
                bindings.MenuCardPreviewPopup.RectTransform,
                canvasBindings.RectTransform);

            _indexes = new MenuDeckIndexes(decks, bindings.Indexes.RectTransform);

            _pool = new MenuDeckPool(
                cardsRegistry,
                viewInjector,
                cardsProjection,
                updater,
                preview,
                bindings.Pool.Viewport.Content.RectTransform,
                canvasBindings.Canvas);

            _editor = new MenuDeckEditor(decks, _pool, bindings.Cards.RectTransform, bindings.Average.Top.Value.TextMeshProUGUI);

            _gameObject.SetActive(false);
        }

        private readonly IDecks _decks;
        private readonly GameObject _gameObject;
        private readonly MenuDeckIndexes _indexes;
        private readonly MenuDeckPool _pool;
        private readonly MenuDeckEditor _editor;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        // Decks and card configs have already arrived by now: no need to wait for a deck update.
        public void OnMetaSetupCompleted(IReadOnlyLifetime lifetime)
        {
            OnInitialized(lifetime);
        }

        private void OnInitialized(IReadOnlyLifetime lifetime)
        {
            if (_editor.IsCreated == true)
                return;

            _indexes.Create(lifetime, index => {
                _editor.Show(index);
                _decks.SendUpdate().Forget();
            });

            _pool.Create(lifetime, _editor.TryDrop);
            _editor.Create(lifetime);
            _editor.Show(_decks.SelectedIndex.Value);
            _pool.Resize();
            _pool.ViewOwned(lifetime);
        }

        public async UniTask OnEntered(IUIStateHandle handle)
        {
            handle.AttachGameObject(_gameObject);
            _pool.Resize();
        }
    }
}
