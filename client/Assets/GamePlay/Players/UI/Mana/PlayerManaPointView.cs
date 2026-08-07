using UnityEngine;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class PlayerManaPointView : MonoBehaviour
    {
        [SerializeField] private Sprite _baseEmpty;
        [SerializeField] private Sprite _baseFull;
        [SerializeField] private Sprite _additionalEmpty;
        [SerializeField] private Sprite _additionalFull;
        [SerializeField] private SpriteRenderer _renderer;

        private bool _isBase;
        
        public void SetEmpty()
        {
            if (_isBase == true)
                _renderer.sprite = _baseEmpty;
            else
                _renderer.sprite = _additionalEmpty;
        }

        public void SetFull()
        {
            if (_isBase == true)
                _renderer.sprite = _baseFull;
            else
                _renderer.sprite = _additionalFull;
        }

        public void SetBase()
        {
            _isBase = true;
        }

        public void SetAdditional()
        {
            _isBase = false;
        }
    }
}