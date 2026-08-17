using Tools;
using UnityEngine;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class PlayerManaPointView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;

        private bool _isBase;
        
        public void SetEmpty()
        {
            if (_isBase == true)
                _renderer.sprite = Sprites.GameUI.ManaInactive;
            else
                _renderer.sprite = Sprites.GameUI.ManaAdditionalInactive;
        }

        public void SetFull()
        {
            if (_isBase == true)
                _renderer.sprite = Sprites.GameUI.ManaActive;
            else
                _renderer.sprite = Sprites.GameUI.ManaAdditionalActive;
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