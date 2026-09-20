namespace Global.UI
{
    /// <summary>Direction the container lays its children out in.</summary>
    public enum DesignAxis
    {
        Horizontal,
        Vertical
    }

    /// <summary>How the children are placed along the layout axis.</summary>
    public enum DesignMainAxisMode
    {
        /// <summary>Items keep their size, the gap between them is exactly <c>spacing</c>,
        /// the whole block is aligned by the main axis pivot.</summary>
        Group,

        /// <summary>Items keep their size, the free space is split evenly between them.</summary>
        Distribute,

        /// <summary>Items are resized so the content fills the container.</summary>
        Fill
    }

    /// <summary>How the children are sized across the layout axis.</summary>
    public enum DesignCrossAxisMode
    {
        /// <summary>The child keeps its own size and is aligned by the cross axis pivot.</summary>
        Keep,

        /// <summary>The child is stretched to the container size minus padding.</summary>
        Stretch
    }

    /// <summary>How the container itself reacts to the size of its content.</summary>
    public enum DesignContainerFit
    {
        None,
        Expand,
        Shrink,
        ExpandAndShrink
    }

    /// <summary>Horizontal alignment. <see cref="Right"/> also reverses the order along a horizontal axis.</summary>
    public enum DesignHorizontalPivot
    {
        Left,
        Center,
        Right
    }

    /// <summary>Vertical alignment. <see cref="Bottom"/> also reverses the order along a vertical axis.</summary>
    public enum DesignVerticalPivot
    {
        Top,
        Middle,
        Bottom
    }

    /// <summary>Per child override of the size it takes along the layout axis.</summary>
    public enum DesignItemSizing
    {
        /// <summary>Keep the size the child already has.</summary>
        Preferred,

        /// <summary>Take a fraction (0..1) of the container size.</summary>
        Percent,

        /// <summary>Share the free space with other weighted items. Used by <see cref="DesignMainAxisMode.Fill"/>.</summary>
        Weight,

        /// <summary>The child is not laid out at all.</summary>
        Ignore
    }

    public enum DesignRefreshMode
    {
        /// <summary>Rebuild when the container, its children or their sizes change.</summary>
        OnChange,

        EveryFrame,

        /// <summary>Rebuild only on an explicit <c>Rebuild()</c> call (still previews in the editor).</summary>
        Manual
    }

    public enum DesignGridConstraint
    {
        /// <summary>Line count is derived from the container size and the cell size.</summary>
        Flexible,
        FixedColumnCount,
        FixedRowCount
    }

    public enum DesignGridCellSizing
    {
        /// <summary>Cells use the configured cell size.</summary>
        Fixed,

        /// <summary>Cells are stretched so the lines fill the container.</summary>
        FitContainer
    }

    /// <summary>The direction the grid grows in as children are added.</summary>
    public enum DesignGridFlow
    {
        /// <summary>Fill a column top to bottom, then append the next column to the side.</summary>
        Horizontal,

        /// <summary>Fill a row left to right, then append the next row below.</summary>
        Vertical
    }
}
