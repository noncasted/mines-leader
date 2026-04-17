using GamePlay.Cards;
using Internal;
using UnityEngine;

namespace Menu.Screens.Cards.Preview.Sync
{
    /// <summary>
    /// <see cref="ICardVfxFactory"/> for the menu preview. The gameplay <c>CardVfxFactory</c> is a
    /// scene MonoBehaviour whose transform parents VFX instances; in the menu scope we create a
    /// dynamic anchor GameObject on first use and parent VFX under it so the regular VFX prefabs
    /// render on the preview camera without touching the real game scene.
    /// </summary>
    public sealed class MenuPreviewVfxFactory : ICardVfxFactory, IScopeSetup
    {
        public MenuPreviewVfxFactory(IViewInjector viewInjector, IMenuBoard menuBoard)
        {
            _viewInjector = viewInjector;
            _menuBoard = menuBoard;
        }

        private readonly IViewInjector _viewInjector;
        private readonly IMenuBoard _menuBoard;

        private int _index;
        private Transform _anchorTransform;

        public void ClearSpawned()
        {
            if (_anchorTransform == null)
                return;

            // Destroy every previously spawned VFX instance (ZipZap lightning lines, any
            // other per-card visual) so the next preview starts with a clean slate.
            for (var i = _anchorTransform.childCount - 1; i >= 0; i--)
                Object.Destroy(_anchorTransform.GetChild(i).gameObject);
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            // Parent VFX under the Menu_Board root so spawned instances inherit its scene,
            // world position and — critically — the layer the preview camera is rendering
            // (a standalone root would land on Default layer, which the preview camera may cull).
            var board = _menuBoard.Board as Component;

            if (board != null)
            {
                var host = new GameObject("MenuPreviewVfxAnchor");
                host.transform.SetParent(board.transform, worldPositionStays: false);
                host.layer = board.gameObject.layer;
                _anchorTransform = host.transform;

                lifetime.Listen(() => {
                    if (_anchorTransform != null)
                        Object.Destroy(_anchorTransform.gameObject);
                });
            }
        }

        public T Create<T>(T prefab, Vector2 position, float angle = 0) where T : MonoBehaviour
        {
            if (prefab == null)
            {
                Debug.LogError("[Preview] MenuPreviewVfxFactory: prefab is null.");
                return null;
            }

            _index++;

            var instance = Object.Instantiate(prefab, position, Quaternion.Euler(0, 0, angle), _anchorTransform);
            instance.name = $"{prefab.name}_preview_{_index}";

            if (_anchorTransform != null)
                SetLayerRecursively(instance.gameObject, _anchorTransform.gameObject.layer);

            _viewInjector.Inject(instance);

            return instance;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;

            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
