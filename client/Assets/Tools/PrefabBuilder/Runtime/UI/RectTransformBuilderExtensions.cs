#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Tools.UI
{
    public static class RectTransformBuilderExtensions
    {
        public static PrefabBuilder WithRectTransform(
            this PrefabBuilder builder,
            Action<RectTransform> configure = null)
        {
            var rt = builder.GameObject.GetComponent<RectTransform>();
            if (rt == null)
                rt = builder.GameObject.AddComponent<RectTransform>();
            configure?.Invoke(rt);
            return builder;
        }

        // --- Stretch presets ---

        public static RectTransform StretchFull(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        public static RectTransform StretchHorizontal(this RectTransform rt)
        {
            rt.anchorMin = new Vector2(0, rt.anchorMin.y);
            rt.anchorMax = new Vector2(1, rt.anchorMax.y);
            rt.sizeDelta = new Vector2(0, rt.sizeDelta.y);
            return rt;
        }

        public static RectTransform StretchVertical(this RectTransform rt)
        {
            rt.anchorMin = new Vector2(rt.anchorMin.x, 0);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 1);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 0);
            return rt;
        }

        // --- Anchor to edge (stretch along edge) ---

        public static RectTransform WithAnchorTop(this RectTransform rt)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        public static RectTransform AnchorBottom(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        public static RectTransform AnchorLeft(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        public static RectTransform AnchorRight(this RectTransform rt)
        {
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        // --- Point anchors ---

        public static RectTransform AnchorTopLeft(this RectTransform rt)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        // --- Sizing ---

        public static RectTransform WithHeight(this RectTransform rt, float height)
        {
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            return rt;
        }

        public static RectTransform WithWidth(this RectTransform rt, float width)
        {
            rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
            return rt;
        }

        public static RectTransform WithSize(this RectTransform rt, float width, float height)
        {
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        public static RectTransform WithSize(this RectTransform rt, Vector2 size)
        {
            rt.sizeDelta = size;
            return rt;
        }

        // --- Padding (inset from stretch) ---

        public static RectTransform WithPadding(this RectTransform rt, float padding)
        {
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
            return rt;
        }

        public static RectTransform WithPadding(this RectTransform rt, float horizontal, float vertical)
        {
            rt.offsetMin = new Vector2(horizontal, vertical);
            rt.offsetMax = new Vector2(-horizontal, -vertical);
            return rt;
        }

        // --- Position ---

        public static RectTransform WithPosition(this RectTransform rt, float x, float y)
        {
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        // --- Pivot ---

        public static RectTransform WithPivot(this RectTransform rt, float x, float y)
        {
            rt.pivot = new Vector2(x, y);
            return rt;
        }

        public static RectTransform TopPivot(this RectTransform rt)
        {
            rt.pivot = new Vector2(0.5f, 1f);
            return rt;
        }

        public static RectTransform BottomPivot(this RectTransform rt)
        {
            rt.pivot = new Vector2(0.5f, 0f);
            return rt;
        }

        public static RectTransform CenterPivot(this RectTransform rt)
        {
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }
    }
}
#endif
