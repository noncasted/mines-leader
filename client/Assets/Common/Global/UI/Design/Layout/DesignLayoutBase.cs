using System.Collections.Generic;
using UnityEngine;

namespace Global.UI
{
    /// <summary>
    /// Shared plumbing for the design layouts: child collection, padding, pivots and rebuild scheduling.
    /// Children are placed with a top left anchor and pivot, so the layout fully owns their rect.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public abstract class DesignLayoutBase : MonoBehaviour
    {
        private static readonly Vector2 TopLeft = new(0f, 1f);

        [SerializeField] protected RectOffset _padding = new();

        [Tooltip("Horizontal alignment: of the whole block for a horizontal layout, of each item for a vertical one.")]
        [SerializeField] protected DesignHorizontalPivot _horizontalPivot = DesignHorizontalPivot.Center;

        [Tooltip("Vertical alignment: of each item for a horizontal layout, of the whole block for a vertical one.")]
        [SerializeField] protected DesignVerticalPivot _verticalPivot = DesignVerticalPivot.Middle;

        [SerializeField] protected DesignRefreshMode _refreshMode = DesignRefreshMode.OnChange;

        private readonly List<RectTransform> _children = new();

        private RectTransform _rect;
        private bool _dirty = true;
        private bool _rebuilding;
        private int _signature;

        public RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        public RectOffset Padding => _padding ??= new RectOffset();

        public DesignHorizontalPivot HorizontalPivot => _horizontalPivot;

        public DesignVerticalPivot VerticalPivot => _verticalPivot;

        public DesignRefreshMode RefreshMode => _refreshMode;

        public void SetPadding(RectOffset padding)
        {
            _padding = padding ?? new RectOffset();
            SetDirty();
        }

        public void SetPivot(DesignHorizontalPivot horizontal, DesignVerticalPivot vertical)
        {
            _horizontalPivot = horizontal;
            _verticalPivot = vertical;
            SetDirty();
        }

        public void SetHorizontalPivot(DesignHorizontalPivot pivot)
        {
            _horizontalPivot = pivot;
            SetDirty();
        }

        public void SetVerticalPivot(DesignVerticalPivot pivot)
        {
            _verticalPivot = pivot;
            SetDirty();
        }

        public void SetRefreshMode(DesignRefreshMode mode)
        {
            _refreshMode = mode;
        }

        public void SetDirty()
        {
            if (_rebuilding == false)
                _dirty = true;
        }

        /// <summary>Lays the children out right now.</summary>
        public void Rebuild()
        {
            if (_rebuilding || isActiveAndEnabled == false)
                return;

            _padding ??= new RectOffset();
            _rebuilding = true;

            try
            {
                CollectChildren(_children);
                Arrange(_children);
            }
            finally
            {
                _rebuilding = false;
                _dirty = false;
                _signature = CalculateSignature(_children);
            }
        }

        protected abstract void Arrange(List<RectTransform> children);

        /// <summary>Lays a nested design layout out first, so its own size is up to date before we measure it.</summary>
        protected static void RebuildNested(RectTransform child)
        {
            if (child.TryGetComponent(out DesignLayoutBase nested) && nested.isActiveAndEnabled)
                nested.Rebuild();
        }

        protected static void Place(RectTransform child, Vector2 topLeftPosition, Vector2 size)
        {
            child.anchorMin = TopLeft;
            child.anchorMax = TopLeft;
            child.pivot = TopLeft;
            child.anchoredPosition = new Vector2(topLeftPosition.x, -topLeftPosition.y);
            child.sizeDelta = size;
        }

        /// <summary>Applies <see cref="DesignContainerFit"/> to one axis and reports the resulting size.</summary>
        protected float FitAxis(RectTransform.Axis axis, DesignContainerFit fit, float needed)
        {
            var current = axis == RectTransform.Axis.Horizontal ? Rect.rect.width : Rect.rect.height;

            if (fit == DesignContainerFit.None || needed < 0f)
                return current;

            var grow = needed > current && fit is DesignContainerFit.Expand or DesignContainerFit.ExpandAndShrink;
            var shrink = needed < current && fit is DesignContainerFit.Shrink or DesignContainerFit.ExpandAndShrink;

            if (grow == false && shrink == false)
                return current;

            Rect.SetSizeWithCurrentAnchors(axis, needed);
            return needed;
        }

        protected float HorizontalAlignment => _horizontalPivot switch
        {
            DesignHorizontalPivot.Left => 0f,
            DesignHorizontalPivot.Center => 0.5f,
            _ => 1f
        };

        protected float VerticalAlignment => _verticalPivot switch
        {
            DesignVerticalPivot.Top => 0f,
            DesignVerticalPivot.Middle => 0.5f,
            _ => 1f
        };

        protected void CollectChildren(List<RectTransform> result)
        {
            result.Clear();

            for (var i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i) is not RectTransform child)
                    continue;

                if (child.gameObject.activeSelf == false)
                    continue;

                if (child.TryGetComponent(out DesignItem item) && item.enabled && item.Sizing == DesignItemSizing.Ignore)
                    continue;

                result.Add(child);
            }
        }

        private int CalculateSignature(List<RectTransform> children)
        {
            var hash = new System.HashCode();

            hash.Add(children.Count);
            hash.Add(Rect.rect.size);

            foreach (var child in children)
                hash.Add(child.rect.size);

            return hash.ToHashCode();
        }

        private bool HasChanged()
        {
            CollectChildren(_children);
            return CalculateSignature(_children) != _signature;
        }

        private void OnEnable() => Rebuild();

        private void OnTransformChildrenChanged() => SetDirty();

        private void OnRectTransformDimensionsChange() => SetDirty();

        private void OnValidate()
        {
            _padding ??= new RectOffset();
            SetDirty();
        }

        private void LateUpdate()
        {
            switch (_refreshMode)
            {
                case DesignRefreshMode.EveryFrame:
                    Rebuild();
                    break;

                case DesignRefreshMode.OnChange:
                    if (_dirty || HasChanged())
                        Rebuild();
                    break;

                case DesignRefreshMode.Manual:
                    if (_dirty && Application.isPlaying == false)
                        Rebuild();
                    break;
            }
        }
    }
}
