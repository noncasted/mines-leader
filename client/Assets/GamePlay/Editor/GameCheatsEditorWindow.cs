using System;
using GamePlay.Cheats;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamePlay.Editor {
    public class GameCheatsEditorWindow : EditorWindow {
        private int _activeTab;
        private VisualElement _tabContent;
        private Button[] _tabButtons;

        private static readonly string[] TabNames = { "Health", "Mana", "Moves", "Cards" };

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
            root.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            rootVisualElement.Add(root);

            root.Add(BuildHeader());
            root.Add(BuildTabs());

            _tabContent = new VisualElement();
            _tabContent.style.flexGrow = 1;
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
                status.text = GameCheatsBridge.IsActive ? "Connected" : "Not in game";
                status.style.color = GameCheatsBridge.IsActive
                    ? new Color(0.4f, 0.8f, 0.4f)
                    : new Color(0.5f, 0.5f, 0.5f);
            }).Every(500);
            header.Add(status);

            return header;
        }

        // --- Tabs ---

        private VisualElement BuildTabs() {
            var tabs = new VisualElement();
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
                case 0: BuildHealthTab(); break;
                case 1: BuildManaTab(); break;
                case 2: BuildMovesTab(); break;
                case 3: BuildCardsTab(); break;
            }
        }

        // --- Health Tab ---

        private void BuildHealthTab() {
            _tabContent.Add(SectionLabel("Current Health"));
            _tabContent.Add(ButtonRow(
                ("Zero", () => GameCheatsBridge.ChangeHealth(-10000), BtnColor.Red),
                ("-1", () => GameCheatsBridge.ChangeHealth(-1), BtnColor.Orange),
                ("+1", () => GameCheatsBridge.ChangeHealth(1), BtnColor.Green),
                ("Full", GameCheatsBridge.RestoreHealth, BtnColor.Blue)));

            _tabContent.Add(Separator());
            _tabContent.Add(SectionLabel("Max Health"));
            _tabContent.Add(ButtonRow(
                ("-1", () => GameCheatsBridge.ChangeMaxHealth(-1), BtnColor.Orange),
                ("+1", () => GameCheatsBridge.ChangeMaxHealth(1), BtnColor.Green),
                ("= 5", () => GameCheatsBridge.SetMaxHealth(5), BtnColor.Blue),
                ("= 10", () => GameCheatsBridge.SetMaxHealth(10), BtnColor.Blue),
                ("= 20", () => GameCheatsBridge.SetMaxHealth(20), BtnColor.Blue)));

            _tabContent.Add(Separator());
            _tabContent.Add(SectionLabel("Match"));
            _tabContent.Add(ButtonRow(
                ("Win", () => GameCheatsBridge.EndMatch(true), BtnColor.Green),
                ("Lose", () => GameCheatsBridge.EndMatch(false), BtnColor.Red)));
        }

        // --- Mana Tab ---

        private void BuildManaTab() {
            _tabContent.Add(SectionLabel("Current Mana"));
            _tabContent.Add(ButtonRow(
                ("Zero", () => GameCheatsBridge.ChangeMana(-10000), BtnColor.Red),
                ("-1", () => GameCheatsBridge.ChangeMana(-1), BtnColor.Orange),
                ("+1", () => GameCheatsBridge.ChangeMana(1), BtnColor.Green),
                ("Full", GameCheatsBridge.RestoreMana, BtnColor.Blue)));

            _tabContent.Add(Separator());
            _tabContent.Add(SectionLabel("Max Mana"));
            _tabContent.Add(ButtonRow(
                ("-1", () => GameCheatsBridge.ChangeMaxMana(-1), BtnColor.Orange),
                ("+1", () => GameCheatsBridge.ChangeMaxMana(1), BtnColor.Green),
                ("= 5", () => GameCheatsBridge.SetMaxMana(5), BtnColor.Blue),
                ("= 10", () => GameCheatsBridge.SetMaxMana(10), BtnColor.Blue),
                ("= 20", () => GameCheatsBridge.SetMaxMana(20), BtnColor.Blue)));
        }

        // --- Moves Tab ---

        private void BuildMovesTab() {
            _tabContent.Add(SectionLabel("Current Moves"));
            _tabContent.Add(ButtonRow(
                ("Zero", () => GameCheatsBridge.ChangeMoves(-10000), BtnColor.Red),
                ("-1", () => GameCheatsBridge.ChangeMoves(-1), BtnColor.Orange),
                ("+1", () => GameCheatsBridge.ChangeMoves(1), BtnColor.Green),
                ("Full", GameCheatsBridge.RestoreMoves, BtnColor.Blue)));

            _tabContent.Add(Separator());
            _tabContent.Add(SectionLabel("Max Moves"));
            _tabContent.Add(ButtonRow(
                ("-1", () => GameCheatsBridge.ChangeMaxMoves(-1), BtnColor.Orange),
                ("+1", () => GameCheatsBridge.ChangeMaxMoves(1), BtnColor.Green),
                ("= 5", () => GameCheatsBridge.SetMaxMoves(5), BtnColor.Blue),
                ("= 10", () => GameCheatsBridge.SetMaxMoves(10), BtnColor.Blue),
                ("= 20", () => GameCheatsBridge.SetMaxMoves(20), BtnColor.Blue)));
        }

        // --- Cards Tab ---

        private void BuildCardsTab() {
            var cards = GameCheatsBridge.GetCards();

            if (cards.Count == 0) {
                _tabContent.Add(SectionLabel("Not in game - cards unavailable"));
                return;
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;

            VisualElement currentRow = null;
            var col = 0;

            foreach (var card in cards) {
                if (col % 4 == 0) {
                    currentRow = new VisualElement();
                    currentRow.style.flexDirection = FlexDirection.Row;
                    currentRow.style.marginBottom = 4;
                    scroll.Add(currentRow);
                }

                var typeId = card.TypeId;
                var tile = new Button(() => GameCheatsBridge.AddCard(typeId));
                tile.style.flexGrow = 0;
                tile.style.width = new StyleLength(new Length(25f, LengthUnit.Percent));
                tile.style.height = 72;
                tile.style.marginLeft = 2;
                tile.style.marginRight = 2;
                tile.style.paddingTop = 4;
                tile.style.paddingBottom = 4;
                tile.style.paddingLeft = 2;
                tile.style.paddingRight = 2;
                tile.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
                tile.style.borderTopLeftRadius = 4;
                tile.style.borderTopRightRadius = 4;
                tile.style.borderBottomLeftRadius = 4;
                tile.style.borderBottomRightRadius = 4;
                tile.style.borderTopWidth = 0;
                tile.style.borderBottomWidth = 0;
                tile.style.borderLeftWidth = 0;
                tile.style.borderRightWidth = 0;
                tile.style.flexDirection = FlexDirection.Column;
                tile.style.alignItems = Align.Center;
                tile.style.justifyContent = Justify.Center;

                var bg = new Color(0.22f, 0.22f, 0.22f);
                var hover = new Color(0.3f, 0.3f, 0.3f);
                tile.RegisterCallback<MouseEnterEvent>(_ => tile.style.backgroundColor = hover);
                tile.RegisterCallback<MouseLeaveEvent>(_ => tile.style.backgroundColor = bg);

                var icon = new VisualElement();
                icon.style.width = 32;
                icon.style.height = 32;
                icon.style.flexShrink = 0;
                icon.style.marginBottom = 4;
                if (card.Icon != null)
                    icon.style.backgroundImage = new StyleBackground(card.Icon);
                tile.Add(icon);

                var label = new Label(card.Name);
                label.style.fontSize = 10;
                label.style.color = new Color(0.75f, 0.75f, 0.75f);
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                label.style.maxWidth = new StyleLength(new Length(100f, LengthUnit.Percent));
                tile.Add(label);

                currentRow.Add(tile);
                col++;
            }

            _tabContent.Add(scroll);
        }

        // --- UI Helpers ---

        private enum BtnColor { Red, Orange, Green, Blue }

        private static Color GetBg(BtnColor c) => c switch {
            BtnColor.Red => new Color(0.6f, 0.2f, 0.2f),
            BtnColor.Orange => new Color(0.55f, 0.35f, 0.15f),
            BtnColor.Green => new Color(0.2f, 0.5f, 0.2f),
            BtnColor.Blue => new Color(0.25f, 0.4f, 0.65f),
            _ => new Color(0.3f, 0.3f, 0.3f)
        };

        private static Color GetHover(BtnColor c) => c switch {
            BtnColor.Red => new Color(0.7f, 0.25f, 0.25f),
            BtnColor.Orange => new Color(0.65f, 0.42f, 0.2f),
            BtnColor.Green => new Color(0.25f, 0.6f, 0.25f),
            BtnColor.Blue => new Color(0.3f, 0.5f, 0.75f),
            _ => new Color(0.4f, 0.4f, 0.4f)
        };

        private static Button ColorButton(string text, Action onClick, BtnColor color) {
            var btn = new Button(onClick) { text = text };
            var bg = GetBg(color);
            var hover = GetHover(color);
            btn.style.height = 28;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.backgroundColor = bg;
            btn.style.color = new Color(0.9f, 0.9f, 0.9f);
            btn.style.fontSize = 12;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = hover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = bg);
            return btn;
        }

        private static VisualElement ButtonRow(params (string text, Action action, BtnColor color)[] buttons) {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4;

            foreach (var (text, action, color) in buttons) {
                var btn = ColorButton(text, action, color);
                btn.style.flexGrow = 1;
                btn.style.marginLeft = 2;
                btn.style.marginRight = 2;
                row.Add(btn);
            }

            return row;
        }

        private static Label SectionLabel(string text) {
            var label = new Label(text);
            label.style.color = new Color(0.7f, 0.7f, 0.7f);
            label.style.fontSize = 12;
            label.style.marginBottom = 4;
            label.style.marginTop = 4;
            return label;
        }

        private static VisualElement Separator() {
            var sep = new VisualElement();
            sep.style.height = 1;
            sep.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f);
            sep.style.marginTop = 8;
            sep.style.marginBottom = 8;
            return sep;
        }
    }
}
