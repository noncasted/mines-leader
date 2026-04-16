using UnityEngine;
using UnityEngine.UIElements;

namespace Global.UI.Toolkit {
    public class ChatPanel : VisualElement {
        public new class UxmlFactory : UxmlFactory<ChatPanel, UxmlTraits> {}
        public new class UxmlTraits : VisualElement.UxmlTraits {}

        public ChatPanel() {
            AddToClassList("chat-panel");
            style.backgroundImage = Resources.Load<Texture2D>("ChatBackdrop");
        }
    }
}
