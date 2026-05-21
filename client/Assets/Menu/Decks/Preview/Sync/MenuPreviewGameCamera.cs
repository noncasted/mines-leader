using GamePlay.Services;
using UnityEngine;

namespace Menu.Decks
{
    /// <summary>
    /// Stub <see cref="IGameCamera"/> wired to the Menu_Board preview camera.
    /// Syncs occasionally call <c>BaseShake()</c> — we perform a cheap in-place shake on
    /// the preview camera so the hover animation feels alive; <c>Enable</c> is a no-op.
    /// </summary>
    public sealed class MenuPreviewGameCamera : IGameCamera
    {
        public MenuPreviewGameCamera(IMenuBoard menuBoard)
        {
            _camera = menuBoard.PreviewCamera;

            if (_camera != null)
                _origin = _camera.transform.position;
        }

        private readonly Camera _camera;
        private readonly Vector3 _origin;

        public Camera Camera => _camera;

        public void Enable()
        {
        }

        public void Shake(float time, float intensity)
        {
            if (_camera == null)
                return;

            // One-shot random offset. The scene has no updater hooked up on this stub, so we
            // just nudge once — enough to register as a hit on the preview.
            var offsetX = Random.Range(-intensity, intensity);
            var offsetY = Random.Range(-intensity, intensity);

            _camera.transform.position = _origin + new Vector3(offsetX, offsetY, 0f);
        }
    }
}
