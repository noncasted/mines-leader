using System.Collections.Generic;
using UnityEngine;

namespace Global.UI
{
    /// <summary>
    /// Lays children out in a single row or column.
    /// The main axis mode decides how the free space is used (group, distribute or fill),
    /// the cross axis mode decides whether the children keep their size or stretch,
    /// and the two pivots align the content inside the container.
    /// </summary>
    [AddComponentMenu("Design/Design Container")]
    public class DesignContainer : DesignLayoutBase
    {
        [SerializeField] private DesignAxis _axis = DesignAxis.Vertical;
        [SerializeField] private DesignMainAxisMode _mainAxis = DesignMainAxisMode.Group;
        [SerializeField] private DesignCrossAxisMode _crossAxis = DesignCrossAxisMode.Keep;
        [SerializeField] private float _spacing;

        [Tooltip("How the container width follows its content.")]
        [SerializeField] private DesignContainerFit _widthFit = DesignContainerFit.None;

        [Tooltip("How the container height follows its content.")]
        [SerializeField] private DesignContainerFit _heightFit = DesignContainerFit.None;

        private readonly List<float> _mainSizes = new();
        private readonly List<float> _weights = new();

        public DesignAxis Axis => _axis;

        public DesignMainAxisMode MainAxis => _mainAxis;

        public DesignCrossAxisMode CrossAxis => _crossAxis;

        public float Spacing => _spacing;

        public DesignContainerFit WidthFit => _widthFit;

        public DesignContainerFit HeightFit => _heightFit;

        public void SetAxis(DesignAxis axis)
        {
            _axis = axis;
            SetDirty();
        }

        public void SetMainAxis(DesignMainAxisMode mode)
        {
            _mainAxis = mode;
            SetDirty();
        }

        public void SetCrossAxis(DesignCrossAxisMode mode)
        {
            _crossAxis = mode;
            SetDirty();
        }

        public void SetSpacing(float spacing)
        {
            _spacing = spacing;
            SetDirty();
        }

        public void SetFit(DesignContainerFit width, DesignContainerFit height)
        {
            _widthFit = width;
            _heightFit = height;
            SetDirty();
        }

        protected override void Arrange(List<RectTransform> children)
        {
            foreach (var child in children)
                RebuildNested(child);

            var vertical = _axis == DesignAxis.Vertical;
            var mainAxis = vertical ? RectTransform.Axis.Vertical : RectTransform.Axis.Horizontal;
            var crossAxis = vertical ? RectTransform.Axis.Horizontal : RectTransform.Axis.Vertical;
            var mainPadding = vertical ? _padding.vertical : _padding.horizontal;
            var crossPadding = vertical ? _padding.horizontal : _padding.vertical;
            var mainFit = vertical ? _heightFit : _widthFit;
            var crossFit = vertical ? _widthFit : _heightFit;

            if (children.Count == 0)
            {
                FitAxis(mainAxis, mainFit, mainPadding);
                FitAxis(crossAxis, crossFit, crossPadding);
                return;
            }

            var gaps = _spacing * (children.Count - 1);
            var available = AxisSize(mainAxis) - mainPadding;

            Measure(children, available, vertical);

            var fittedMain = FitAxis(mainAxis, mainFit, Total(_mainSizes) + gaps + mainPadding);

            if (Mathf.Approximately(fittedMain - mainPadding, available) == false)
            {
                available = fittedMain - mainPadding;
                Measure(children, available, vertical);
            }

            var crossAvailable = FitAxis(crossAxis, crossFit, MaxCross(children, vertical) + crossPadding) - crossPadding;

            var content = Total(_mainSizes);
            var gap = _mainAxis == DesignMainAxisMode.Distribute && children.Count > 1
                ? Mathf.Max(_spacing, (available - content) / (children.Count - 1))
                : _spacing;

            var used = content + gap * (children.Count - 1);
            var mainAlignment = vertical ? VerticalAlignment : HorizontalAlignment;
            var crossAlignment = vertical ? HorizontalAlignment : VerticalAlignment;

            var mainOffset = (vertical ? _padding.top : _padding.left) + (available - used) * mainAlignment;
            var crossStart = vertical ? _padding.left : _padding.top;

            // An end pivot also reverses the order: the first child ends up at the far end of the axis.
            var reversed = vertical
                ? _verticalPivot == DesignVerticalPivot.Bottom
                : _horizontalPivot == DesignHorizontalPivot.Right;

            for (var step = 0; step < children.Count; step++)
            {
                var i = reversed ? children.Count - 1 - step : step;
                var child = children[i];
                var mainSize = _mainSizes[i];
                var crossSize = CrossSize(child, crossAvailable, vertical);
                var crossOffset = crossStart + (crossAvailable - crossSize) * crossAlignment;

                var position = vertical
                    ? new Vector2(crossOffset, mainOffset)
                    : new Vector2(mainOffset, crossOffset);

                var size = vertical
                    ? new Vector2(crossSize, mainSize)
                    : new Vector2(mainSize, crossSize);

                Place(child, position, size);
                RebuildNested(child);

                mainOffset += mainSize + gap;
            }
        }

        /// <summary>Fills <see cref="_mainSizes"/> with the size every child takes along the layout axis.</summary>
        private void Measure(List<RectTransform> children, float available, bool vertical)
        {
            _mainSizes.Clear();
            _weights.Clear();

            var fixedTotal = 0f;
            var weightTotal = 0f;

            foreach (var child in children)
            {
                var sizing = DesignItemSizing.Preferred;
                var item = GetItem(child);

                if (item != null)
                    sizing = item.Sizing;
                else if (_mainAxis == DesignMainAxisMode.Fill)
                    sizing = DesignItemSizing.Weight;

                if (sizing == DesignItemSizing.Weight && _mainAxis != DesignMainAxisMode.Fill)
                    sizing = DesignItemSizing.Preferred;

                switch (sizing)
                {
                    case DesignItemSizing.Percent:
                        var percent = (item != null ? item.Percent : 1f) * available;
                        fixedTotal += percent;
                        _mainSizes.Add(percent);
                        _weights.Add(0f);
                        break;

                    case DesignItemSizing.Weight:
                        var weight = item != null ? item.Weight : 1f;
                        weightTotal += weight;
                        _mainSizes.Add(0f);
                        _weights.Add(weight);
                        break;

                    default:
                        var preferred = MainSize(child, vertical);
                        fixedTotal += preferred;
                        _mainSizes.Add(preferred);
                        _weights.Add(0f);
                        break;
                }
            }

            if (weightTotal <= 0f)
                return;

            var free = Mathf.Max(0f, available - fixedTotal - _spacing * (children.Count - 1));

            for (var i = 0; i < _mainSizes.Count; i++)
            {
                if (_weights[i] > 0f)
                    _mainSizes[i] = free * _weights[i] / weightTotal;
            }
        }

        private float CrossSize(RectTransform child, float crossAvailable, bool vertical)
        {
            var mode = _crossAxis;
            var item = GetItem(child);

            if (item != null && item.TryGetCrossAxis(out var overridden))
                mode = overridden;

            return mode == DesignCrossAxisMode.Stretch
                ? Mathf.Max(0f, crossAvailable)
                : vertical
                    ? child.rect.width
                    : child.rect.height;
        }

        private float MaxCross(List<RectTransform> children, bool vertical)
        {
            var max = 0f;

            foreach (var child in children)
                max = Mathf.Max(max, vertical ? child.rect.width : child.rect.height);

            return max;
        }

        private float AxisSize(RectTransform.Axis axis) =>
            axis == RectTransform.Axis.Horizontal ? Rect.rect.width : Rect.rect.height;

        private static float MainSize(RectTransform child, bool vertical) =>
            vertical ? child.rect.height : child.rect.width;

        private static DesignItem GetItem(RectTransform child) =>
            child.TryGetComponent(out DesignItem item) && item.enabled ? item : null;

        private static float Total(List<float> values)
        {
            var total = 0f;

            foreach (var value in values)
                total += value;

            return total;
        }
    }
}
