using System.Collections.Generic;
using UnityEngine;

namespace Global.UI
{
    /// <summary>
    /// Lays children out in a grid. The flow picks the direction the grid grows in, the line count is either
    /// fixed or derived from the container size,
    /// cells either keep the configured size or stretch to fill the container, and the two pivots
    /// align the whole grid inside the container.
    /// </summary>
    [AddComponentMenu("Design/Design Grid")]
    public class DesignGrid : DesignLayoutBase
    {
        [SerializeField] private DesignGridFlow _flow = DesignGridFlow.Horizontal;
        [SerializeField] private DesignGridConstraint _constraint = DesignGridConstraint.Flexible;
        [SerializeField, Min(1)] private int _constraintCount = 1;
        [SerializeField] private DesignGridCellSizing _cellSizing = DesignGridCellSizing.Fixed;
        [SerializeField] private Vector2 _cellSize = new(100f, 100f);
        [SerializeField] private Vector2 _spacing;

        [SerializeField] private DesignContainerFit _widthFit = DesignContainerFit.None;
        [SerializeField] private DesignContainerFit _heightFit = DesignContainerFit.None;

        public DesignGridFlow Flow => _flow;

        public DesignGridConstraint Constraint => _constraint;

        public int ConstraintCount => _constraintCount;

        public DesignGridCellSizing CellSizing => _cellSizing;

        public Vector2 CellSize => _cellSize;

        public Vector2 Spacing => _spacing;

        public DesignContainerFit WidthFit => _widthFit;

        public DesignContainerFit HeightFit => _heightFit;

        public void SetFlow(DesignGridFlow flow)
        {
            _flow = flow;
            SetDirty();
        }

        public void SetConstraint(DesignGridConstraint constraint, int count = 1)
        {
            _constraint = constraint;
            _constraintCount = Mathf.Max(1, count);
            SetDirty();
        }

        public void SetCellSizing(DesignGridCellSizing sizing)
        {
            _cellSizing = sizing;
            SetDirty();
        }

        public void SetCellSize(Vector2 size)
        {
            _cellSize = size;
            SetDirty();
        }

        public void SetSpacing(Vector2 spacing)
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

            var availableWidth = Rect.rect.width - _padding.horizontal;
            var availableHeight = Rect.rect.height - _padding.vertical;

            if (children.Count == 0)
            {
                FitAxis(RectTransform.Axis.Horizontal, _widthFit, _padding.horizontal);
                FitAxis(RectTransform.Axis.Vertical, _heightFit, _padding.vertical);
                return;
            }

            GetLineCounts(children.Count, availableWidth, availableHeight, out var columns, out var rows);

            var cellWidth = _cellSizing == DesignGridCellSizing.FitContainer
                ? Mathf.Max(0f, (availableWidth - _spacing.x * (columns - 1)) / columns)
                : _cellSize.x;

            var cellHeight = _cellSizing == DesignGridCellSizing.FitContainer
                ? Mathf.Max(0f, (availableHeight - _spacing.y * (rows - 1)) / rows)
                : _cellSize.y;

            var contentWidth = columns * cellWidth + _spacing.x * (columns - 1);
            var contentHeight = rows * cellHeight + _spacing.y * (rows - 1);

            availableWidth = FitAxis(RectTransform.Axis.Horizontal, _widthFit, contentWidth + _padding.horizontal) - _padding.horizontal;
            availableHeight = FitAxis(RectTransform.Axis.Vertical, _heightFit, contentHeight + _padding.vertical) - _padding.vertical;

            var originX = _padding.left + (availableWidth - contentWidth) * HorizontalAlignment;
            var originY = _padding.top + (availableHeight - contentHeight) * VerticalAlignment;

            // An end pivot also reverses the fill order: the first child ends up in the last column or row.
            var reversedColumns = _horizontalPivot == DesignHorizontalPivot.Right;
            var reversedRows = _verticalPivot == DesignVerticalPivot.Bottom;

            for (var i = 0; i < children.Count; i++)
            {
                // Horizontal flow fills a column top to bottom and then appends the next column to the side,
                // vertical flow fills a row and then appends the next row below.
                var column = _flow == DesignGridFlow.Horizontal ? i / rows : i % columns;
                var row = _flow == DesignGridFlow.Horizontal ? i % rows : i / columns;

                if (reversedColumns)
                    column = columns - 1 - column;

                if (reversedRows)
                    row = rows - 1 - row;

                var position = new Vector2(
                    originX + column * (cellWidth + _spacing.x),
                    originY + row * (cellHeight + _spacing.y));

                Place(children[i], position, new Vector2(cellWidth, cellHeight));
                RebuildNested(children[i]);
            }
        }

        private void GetLineCounts(int count, float availableWidth, float availableHeight, out int columns, out int rows)
        {
            switch (_constraint)
            {
                case DesignGridConstraint.FixedColumnCount:
                    columns = Mathf.Max(1, _constraintCount);
                    rows = Mathf.CeilToInt(count / (float)columns);
                    return;

                case DesignGridConstraint.FixedRowCount:
                    rows = Mathf.Max(1, _constraintCount);
                    columns = Mathf.CeilToInt(count / (float)rows);
                    return;

                default:
                    // The axis the grid does not grow along is the one limited by the container size.
                    if (_flow == DesignGridFlow.Horizontal)
                    {
                        rows = Mathf.Clamp(FitCount(availableHeight, _cellSize.y, _spacing.y), 1, count);
                        columns = Mathf.CeilToInt(count / (float)rows);
                    }
                    else
                    {
                        columns = Mathf.Clamp(FitCount(availableWidth, _cellSize.x, _spacing.x), 1, count);
                        rows = Mathf.CeilToInt(count / (float)columns);
                    }

                    return;
            }
        }

        private static int FitCount(float available, float cell, float spacing)
        {
            if (cell + spacing <= 0f)
                return 1;

            return Mathf.Max(1, Mathf.FloorToInt((available + spacing) / (cell + spacing)));
        }
    }
}
