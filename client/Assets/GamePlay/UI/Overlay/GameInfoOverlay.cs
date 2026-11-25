using GamePlay.Services;
using UnityEngine;

namespace GamePlay.UI
{
    public interface IGameInfoOverlay
    {
        T Spawn<T>(T prefab) where T : Component;
    }

    // Tooltips live on a separate canvas above the cards: hand cards are sorted on the UI layer
    // with order up to CardSorting.SelectedOrder, and the players canvas would end up under them.
    public class GameInfoOverlay : IGameInfoOverlay
    {
        public GameInfoOverlay(IGameCamera camera, InfoOverlayUIBindings bindings)
        {
            _bindings = bindings;

            _bindings.Canvas.worldCamera = camera.Camera;
        }

        private readonly InfoOverlayUIBindings _bindings;

        public T Spawn<T>(T prefab) where T : Component
        {
            return Object.Instantiate(prefab, _bindings.RectTransform);
        }
    }
}
