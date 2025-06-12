using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class PlayerManaPointView : MonoBehaviour
    {
        [SerializeField] private Sprite _empty;
        [SerializeField] private Sprite _full;
        [SerializeField] private Image _image;
        
        public void SetEmpty()
        {
            _image.sprite = _empty;
        }
        
        public void SetFull()
        {
            _image.sprite = _full;
        }
    }
}