using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamePlay.Editor
{
    // Shared elements of the tabs of the cheats window: coloured buttons, rows, labels, separators.
    internal static class CheatsUi
    {
        private static Color GetBg(CheatsButtonColor c) => c switch
        {
            CheatsButtonColor.Red => new Color(0.6f, 0.2f, 0.2f),
            CheatsButtonColor.Orange => new Color(0.55f, 0.35f, 0.15f),
            CheatsButtonColor.Green => new Color(0.2f, 0.5f, 0.2f),
            CheatsButtonColor.Blue => new Color(0.25f, 0.4f, 0.65f),
            _ => new Color(0.3f, 0.3f, 0.3f)
        };

        private static Color GetHover(CheatsButtonColor c) => c switch
        {
            CheatsButtonColor.Red => new Color(0.7f, 0.25f, 0.25f),
            CheatsButtonColor.Orange => new Color(0.65f, 0.42f, 0.2f),
            CheatsButtonColor.Green => new Color(0.25f, 0.6f, 0.25f),
            CheatsButtonColor.Blue => new Color(0.3f, 0.5f, 0.75f),
            _ => new Color(0.4f, 0.4f, 0.4f)
        };

        private static Button ColorButton(string text, Action onClick, CheatsButtonColor color)
        {
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

        public static VisualElement ButtonRow(params (string text, Action action, CheatsButtonColor color)[] buttons)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4;

            foreach (var (text, action, color) in buttons)
            {
                var btn = ColorButton(text, action, color);
                btn.style.flexGrow = 1;
                btn.style.marginLeft = 2;
                btn.style.marginRight = 2;
                row.Add(btn);
            }

            return row;
        }

        public static Label SectionLabel(string text)
        {
            var label = new Label(text);
            label.style.color = new Color(0.7f, 0.7f, 0.7f);
            label.style.fontSize = 12;
            label.style.marginBottom = 4;
            label.style.marginTop = 4;
            return label;
        }

        public static VisualElement Separator()
        {
            var sep = new VisualElement();
            sep.style.height = 1;
            sep.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f);
            sep.style.marginTop = 8;
            sep.style.marginBottom = 8;
            return sep;
        }
    }
}
