using UnityEngine;
using UnityEngine.UIElements;

namespace Global.UI.Toolkit {
    public class NavButton : Button {
        public new class UxmlFactory : UxmlFactory<NavButton, UxmlTraits> {}
        public new class UxmlTraits : Button.UxmlTraits {}

        public NavButton() {
            AddToClassList("nav-button");
            style.backgroundImage = Resources.Load<Texture2D>("NavButtonBackdrop");
        }
    }
}
