using GamePlay.Cheats;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamePlay.Editor {
    public class GameCheatsEditorWindow : EditorWindow {
        private int _activeTab;
        private VisualElement _tabContent;
        private Button[] _tabButtons;

        private static readonly string[] TabNames = { "Health", "Mana", "Turns", "Cards" };

        [MenuItem("Tools/Game Cheats %t")]
        public static void ToggleWindow() {
            var existing = Resources.FindObjectsOfTypeAll<GameCheatsEditorWindow>();

            if (existing.Length > 0) {
                existing[0].Close();
                return;
            }

            var window = GetWindow<GameCheatsEditorWindow>();
            window.titleContent = new GUIContent("Game Cheats");
            window.minSize = new Vector2(380, 300);
        }

        public void CreateGUI() {
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.overflow = Overflow.Hidden;
            root.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            rootVisualElement.Add(root);

            root.Add(BuildHeader());
            root.Add(BuildTabs());

            _tabContent = new VisualElement();
            _tabContent.style.flexGrow = 1;
            _tabContent.style.flexShrink = 1;
            _tabContent.style.minHeight = 0;
            _tabContent.style.overflow = Overflow.Hidden;
            _tabContent.style.paddingTop = 8;
            _tabContent.style.paddingBottom = 8;
            _tabContent.style.paddingLeft = 10;
            _tabContent.style.paddingRight = 10;
            root.Add(_tabContent);

            SwitchTab(0);
        }

        // --- Header ---

        private VisualElement BuildHeader() {
            var header = new VisualElement();
            header.style.flexShrink = 0;
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.paddingLeft = 10;
            header.style.paddingRight = 10;
            header.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.14f, 0.14f, 0.14f);

            var title = new Label("Game Cheats");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.85f, 0.85f, 0.85f);
            header.Add(title);

            var status = new Label();
            status.style.fontSize = 11;
            status.schedule.Execute(() => {
                status.text = GameCheatsAccess.IsActive ? "Connected" : "Not in game";
                status.style.color = GameCheatsAccess.IsActive
                    ? new Color(0.4f, 0.8f, 0.4f)
                    : new Color(0.5f, 0.5f, 0.5f);
            }).Every(500);
            header.Add(status);

            return header;
        }

        // --- Tabs ---

        private VisualElement BuildTabs() {
            var tabs = new VisualElement();
            tabs.style.flexShrink = 0;
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            tabs.style.borderBottomWidth = 1;
            tabs.style.borderBottomColor = new Color(0.14f, 0.14f, 0.14f);

            _tabButtons = new Button[TabNames.Length];

            for (var i = 0; i < TabNames.Length; i++) {
                var index = i;
                var btn = new Button(() => SwitchTab(index)) { text = TabNames[i] };
                btn.style.flexGrow = 1;
                btn.style.height = 30;
                btn.style.flexShrink = 0;
                btn.style.borderTopWidth = 0;
                btn.style.borderBottomWidth = 2;
                btn.style.borderLeftWidth = 0;
                btn.style.borderRightWidth = 0;
                btn.style.borderTopLeftRadius = 0;
                btn.style.borderTopRightRadius = 0;
                btn.style.borderBottomLeftRadius = 0;
                btn.style.borderBottomRightRadius = 0;
                btn.style.fontSize = 12;
                tabs.Add(btn);
                _tabButtons[i] = btn;
            }

            return tabs;
        }

        private void SwitchTab(int index) {
            _activeTab = index;
            _tabContent.Clear();

            for (var i = 0; i < _tabButtons.Length; i++) {
                var active = i == index;
                _tabButtons[i].style.backgroundColor = active
                    ? new Color(0.25f, 0.25f, 0.25f)
                    : new Color(0.2f, 0.2f, 0.2f);
                _tabButtons[i].style.color = active
                    ? new Color(0.9f, 0.9f, 0.9f)
                    : new Color(0.55f, 0.55f, 0.55f);
                _tabButtons[i].style.borderBottomColor = active
                    ? new Color(0.32f, 0.5f, 0.82f)
                    : new Color(0.14f, 0.14f, 0.14f);
            }

            switch (index) {
                case 0: CheatsHealthTab.Build(_tabContent); break;
                case 1: CheatsManaTab.Build(_tabContent); break;
                case 2: CheatsMovesTab.Build(_tabContent); break;
                case 3: CheatsCardsTab.Build(_tabContent); break;
            }
        }
    }
}
