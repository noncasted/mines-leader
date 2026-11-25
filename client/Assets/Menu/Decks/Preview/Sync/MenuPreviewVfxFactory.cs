using GamePlay.Cards;
using Internal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Menu.Decks
{
    /// <summary>
    /// <see cref="ICardVfxFactory"/> for the menu preview. The gameplay <c>CardVfxFactory</c> is a
    /// scene MonoBehaviour whose transform parents VFX instances; in the menu scope we create an
    /// anchor GameObject under the menu board and pool VFX under it so the regular VFX prefabs
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

        private CardVfxPools _pools;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            // Parent VFX under the Menu_Board root so pooled instances inherit its scene,
            // world position and — critically — the layer the preview camera is rendering
            // (a standalone root would land on Default layer, which the preview camera may cull).
            var board = (Component)_menuBoard.Board;

            var host = new GameObject("MenuPreviewVfxAnchor");
            host.transform.SetParent(board.transform, worldPositionStays: false);
            host.layer = board.gameObject.layer;

            _pools = new CardVfxPools(lifetime, host.transform, _viewInjector);

            lifetime.Listen(() => {
                // The anchor is a child of the board: an unloaded menu scene destroys it before the scope ends.
                if (host != null)
                    Object.Destroy(host);
            });
        }

        public T Create<T>(IReadOnlyLifetime lifetime, T prefab, Vector2 position, float angle = 0) where T : MonoBehaviour
        {
            return _pools.Get(lifetime, prefab, position, angle);
        }
    }
}
