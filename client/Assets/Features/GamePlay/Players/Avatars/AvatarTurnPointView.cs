using UnityEngine;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class AvatarTurnPointView : MonoBehaviour
    {
        [SerializeField] private Sprite _active;
        [SerializeField] private Sprite _inactive;

        [SerializeField] private SpriteRenderer _renderer;
        
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