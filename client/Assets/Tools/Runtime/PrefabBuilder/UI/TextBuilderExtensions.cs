using System;
using TMPro;
using UnityEngine;

namespace Tools.PrefabBuilder
{
    public static class TextBuilderExtensions
    {
        private static readonly Color DefaultTextColor = new(0.2f, 0.2f, 0.2f, 1f);
        private const float DefaultFontSize = 14f;

        public static PrefabBuilder AddLabel(
            this PrefabBuilder builder,
            string name,
            string text,
            Action<TextMeshProUGUI> configure = null)
        {
            builder.WithChildObject(name, child => {
                        child.WithComponent<CanvasRenderer>()
                             .WithComponent<TextMeshProUGUI>(tmp => {
                                         ApplyDefaults(tmp, text);
                                         configure?.Invoke(tmp);
                                     }
                                 );
                    }
                );
            return builder;
        }

        public static PrefabBuilder AddLabel(
            this PrefabBuilder builder,
            string name,
            string text,
            Action<RectTransform> configureRect)
        {
            builder.WithChildObject(name, child => {
                        child.WithComponent<CanvasRenderer>()
                             .WithComponent<TextMeshProUGUI>(tmp => ApplyDefaults(tmp, text))
                             .WithRectTransform(rt => configureRect?.Invoke(rt));
                    }
                );
            return builder;
        }

        public static PrefabBuilder AddLabel(
            this PrefabBuilder builder,
            string name,
            Action<RectTransform> configureRect,
            Action<TextMeshProUGUI> configureText)
        {
            builder.WithChildObject(name, child => {
                        child.WithComponent<CanvasRenderer>()
                             .WithComponent<TextMeshProUGUI>(tmp => {
                                         ApplyDefaults(tmp, name);
                                         configureText?.Invoke(tmp);
                                     }
                                 )
                             .WithRectTransform(rt => configureRect?.Invoke(rt));
                    }
                );
            return builder;
        }

        public static PrefabBuilder AddLabel(
            this PrefabBuilder builder,
            string name,
            string text,
            out TextMeshProUGUI label,
            Action<RectTransform> configureRect = null)
        {
            TextMeshProUGUI captured = null;

            builder.WithChildObject(name, child => {
                        child.WithComponent<CanvasRenderer>()
                             .WithComponent<TextMeshProUGUI>(tmp => {
                                         ApplyDefaults(tmp, text);
                                         captured = tmp;
                                     }
                                 )
                             .WithRectTransform(rt => configureRect?.Invoke(rt));
                    }
                );
            label = captured;
            return builder;
        }

        private static void ApplyDefaults(TextMeshProUGUI tmp, string text)
        {
            tmp.text = text;
            tmp.color = DefaultTextColor;
            tmp.fontSize = DefaultFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
        }
    }
}