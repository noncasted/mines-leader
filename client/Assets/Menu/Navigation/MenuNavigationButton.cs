using TMPro;
using Tools;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Navigation
{
    [DisallowMultipleComponent]
    public class MenuNavigationButton : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Button _button;

        public Button Button => _button;

        public void Active()
        {
            _image.sprite = Sprites.MenuNavigation.ButtonActive;
            _text.color = Colors.Menu.NavigationActive;
        }

        public void Inactive()
        {
            _image.sprite = Sprites.MenuNavigation.ButtonInactive;
            _text.color = Colors.Menu.NavigationInactive;
        }
    }
}