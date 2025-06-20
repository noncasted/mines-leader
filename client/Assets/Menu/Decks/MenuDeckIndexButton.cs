using Global.UI;
using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Decks
{
    [DisallowMultipleComponent]
    public class MenuDeckIndexButton : MonoBehaviour
    {
        [SerializeField] private Color _active;
        [SerializeField] private Color _disabled;
        
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Image _plate;
        [SerializeField] private DesignButton _button;

        public IViewableDelegate Clicked => _button.Clicked;
        
        public void Setup(int index)
        {
            _text.text = index.ToString();
        }

        public void Activate()
        {
            _plate.color = _active;
        }
        
        public void Deactivate()
        {
            _plate.color = _disabled;
        }
    }
}