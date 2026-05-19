using UnityEngine;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class AvatarTurnPointView : MonoBehaviour
    {
        [SerializeField] private Sprite _baseActive;
        [SerializeField] private Sprite _baseInactive;
        [SerializeField] private Sprite _additionalActive;
        [SerializeField] private Sprite _additionalInactive;

        [SerializeField] private SpriteRenderer _renderer;

        private Sprite _active;
        private Sprite _inactive;

        public void SetBase()
        {
            _active = _baseActive;
            _inactive = _baseInactive;
        }

        public void SetAdditional()
        {
            _active = _additionalActive;
            _inactive = _additionalInactive;
        }

        public void Show()
        {
            _renderer.sprite = _active;
        }

        public void Hide()
        {
            _renderer.sprite = _inactive;
        }
    }
}
