using GamePlay.Services;
using UnityEngine;

namespace GamePlay.UI
{
    public interface IGameInfoOverlay
    {
        T Spawn<T>(T prefab) where T : Component;
    }

    // Тултипы живут на отдельном канвасе поверх карт: карты в руке сортируются на слое UI
    // с order до CardSorting.SelectedOrder, и канвас игроков оказывается под ними.
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
