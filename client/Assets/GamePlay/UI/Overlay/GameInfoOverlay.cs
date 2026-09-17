using GamePlay.Services;
using Internal;
using UnityEngine;

namespace GamePlay.UI
{
    public interface IGameInfoOverlay
    {
        T Spawn<T>(T prefab) where T : Component;
    }

    // Тултипы живут на отдельном канвасе поверх карт: карты в руке сортируются на слое UI
    // с order до CardSorting.SelectedOrder, и канвас игроков оказывается под ними.
    public class GameInfoOverlay : IScopeBaseSetup, IGameInfoOverlay
    {
        public GameInfoOverlay(IGameCamera camera, InfoOverlayUIBindings bindings)
        {
            _camera = camera;
            _bindings = bindings;
        }

        private readonly IGameCamera _camera;
        private readonly InfoOverlayUIBindings _bindings;

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            _bindings.Canvas.worldCamera = _camera.Camera;
        }

        public T Spawn<T>(T prefab) where T : Component
        {
            return Object.Instantiate(prefab, _bindings.RectTransform);
        }
    }
}
