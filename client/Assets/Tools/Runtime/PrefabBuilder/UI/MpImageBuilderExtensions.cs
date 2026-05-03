#if UNITY_EDITOR
using System;
using MPUIKIT;
using UnityEngine;

namespace Tools.Runtime.PrefabBuilder
{
    public static class MpImageBuilderExtensions
    {
        public static PrefabBuilder WithCornerRadius(this PrefabBuilder builder, float radius)
        {
            return builder.WithCornerRadius(new Vector4(radius, radius, radius, radius));
        }

        public static PrefabBuilder WithCornerRadius(this PrefabBuilder builder, Vector4 radius)
        {
            var img = builder.GameObject.GetComponent<MPImage>();

            if (img == null)
            {
                Debug.LogError("[PrefabBuilder] WithCornerRadius requires MPImage. Call WithMPImage first.");
                return builder;
            }

            img.DrawShape = DrawShape.Rectangle;
            var rect = img.Rectangle;
            rect.CornerRadius = radius;
            img.Rectangle = rect;
            return builder;
        }

        public static PrefabBuilder WithMPImage(this PrefabBuilder builder, Color color)
        {
            builder.EnsureCanvasRenderer();
            builder.WithComponent<MPImage>(img => img.color = color);
            return builder;
        }

        public static PrefabBuilder WithMPImage(this PrefabBuilder builder, Color color, out MPImage image)
        {
            MPImage captured = null;
            builder.EnsureCanvasRenderer();

            builder.WithComponent<MPImage>(img => {
                        img.color = color;
                        captured = img;
                    }
                );
            image = captured;
            return builder;
        }

        public static PrefabBuilder WithMPImage(this PrefabBuilder builder, Action<MPImage> configure)
        {
            builder.EnsureCanvasRenderer();
            builder.WithComponent<MPImage>(configure);
            return builder;
        }

        private static void EnsureCanvasRenderer(this PrefabBuilder builder)
        {
            if (builder.GameObject.GetComponent<CanvasRenderer>() == null)
                builder.WithComponent<CanvasRenderer>();
        }

        // Rounded Rectangle

        public static PrefabBuilder WithRoundedRect(this PrefabBuilder builder, Color color, float cornerRadius)
        {
            return builder.WithRoundedRect(color, Vector4.one * cornerRadius);
        }

        public static PrefabBuilder WithRoundedRect(this PrefabBuilder builder, Color color, Vector4 cornerRadius)
        {
            builder.WithMPImage(color);
            builder.SetSerialized<MPImage>("m_DrawShape", (int)DrawShape.Rectangle);
            builder.SetSerialized<MPImage>("m_Rectangle.m_CornerRadius", cornerRadius);
            return builder;
        }

        public static MPImage WithRoundedCorners(this MPImage image, float radius)
        {
            image.DrawShape = DrawShape.Rectangle;

            image.Rectangle = new Rectangle()
            {
                CornerRadius = Vector4.one * radius,
            };
            return image;
        }

        public static MPImage WithColor(this MPImage image, Color color)
        {
            image.color = color;
            return image;
        }

        public static PrefabBuilder WithRoundedRect(
            this PrefabBuilder builder,
            Color color,
            float cornerRadius,
            out MPImage image)
        {
            builder.WithMPImage(color, out image);
            builder.SetSerialized<MPImage>("m_DrawShape", (int)DrawShape.Rectangle);
            builder.SetSerialized<MPImage>("m_Rectangle.m_CornerRadius", Vector4.one * cornerRadius);
            return builder;
        }

        // Circle

        public static PrefabBuilder WithCircle(this PrefabBuilder builder, Color color)
        {
            builder.WithMPImage(color);
            builder.SetSerialized<MPImage>("m_DrawShape", (int)DrawShape.Circle);
            builder.SetSerialized<MPImage>("m_Circle.m_FitRadius", true);
            return builder;
        }

        public static PrefabBuilder WithCircle(this PrefabBuilder builder, Color color, out MPImage image)
        {
            builder.WithMPImage(color, out image);
            builder.SetSerialized<MPImage>("m_DrawShape", (int)DrawShape.Circle);
            builder.SetSerialized<MPImage>("m_Circle.m_FitRadius", true);
            return builder;
        }

        // Outline (fill + border)

        public static PrefabBuilder WithOutline(
            this PrefabBuilder builder,
            Color fillColor,
            Color outlineColor,
            float outlineWidth)
        {
            builder.WithMPImage(fillColor);
            builder.SetSerialized<MPImage>("m_OutlineColor", outlineColor);
            builder.SetSerialized<MPImage>("m_OutlineWidth", outlineWidth);
            return builder;
        }

        // Stroke (hollow shape, no fill)

        public static PrefabBuilder WithStroke(this PrefabBuilder builder, Color strokeColor, float strokeWidth)
        {
            builder.WithMPImage(strokeColor);
            builder.SetSerialized<MPImage>("m_StrokeWidth", strokeWidth);
            return builder;
        }

        // Child objects with MPImage

        public static PrefabBuilder WithMPImageChild(
            this PrefabBuilder builder,
            string name,
            Color color,
            Action<PrefabBuilder> configure = null)
        {
            builder.WithChildObject(name, child => {
                        child.WithMPImage(color);
                        configure?.Invoke(child);
                    }
                );
            return builder;
        }

        public static PrefabBuilder WithMPImageChild(
            this PrefabBuilder builder,
            string name,
            bool active,
            Color color,
            Action<PrefabBuilder> configure = null)
        {
            builder.WithChildObject(name, active, child => {
                        child.WithMPImage(color);
                        configure?.Invoke(child);
                    }
                );
            return builder;
        }

        public static PrefabBuilder WithRoundedRectChild(
            this PrefabBuilder builder,
            string name,
            Color color,
            float cornerRadius,
            Action<PrefabBuilder> configure = null)
        {
            builder.WithChildObject(name, child => {
                        child.WithRoundedRect(color, cornerRadius);
                        configure?.Invoke(child);
                    }
                );
            return builder;
        }

        public static PrefabBuilder WithRoundedRectChild(
            this PrefabBuilder builder,
            string name,
            bool active,
            Color color,
            float cornerRadius,
            Action<PrefabBuilder> configure = null)
        {
            builder.WithChildObject(name, active, child => {
                        child.WithRoundedRect(color, cornerRadius);
                        configure?.Invoke(child);
                    }
                );
            return builder;
        }
    }
}
#endif