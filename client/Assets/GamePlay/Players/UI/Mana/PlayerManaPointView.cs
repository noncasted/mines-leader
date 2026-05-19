using UnityEngine;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class PlayerManaPointView : MonoBehaviour
    {
        [SerializeField] private Sprite _empty;
        [SerializeField] private Sprite _full;
        [SerializeField] private Color _baseColor = Color.white;
        [SerializeField] private Color _additionalColor = Color.white;
        [SerializeField] private SpriteRenderer _renderer;

        public void SetEmpty()
        {
            _renderer.sprite = _empty;
        }

        public void SetFull()
        {
            _renderer.sprite = _full;
        }

        public void SetBase()
        {
            _renderer.color = _baseColor;
        }

        public void SetAdditional()
        {
            _renderer.color = _additionalColor;
        }
    }
}