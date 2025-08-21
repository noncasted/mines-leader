using Global.UI;
using Internal;
using Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.Cheats
{
    [DisallowMultipleComponent]
    public class CardAddEntry : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private DesignButton _button;
        
        public IViewableDelegate Clicked => _button.Clicked;
        
        public void Setup(ICardDefinition definition)
        {
            _icon.sprite = definition.Image;
            _name.text = definition.Name;
        }
    }
}