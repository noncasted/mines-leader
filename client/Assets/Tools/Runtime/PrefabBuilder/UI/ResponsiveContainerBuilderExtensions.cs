using System;
using Exoa.Responsive;
using UnityEngine;

namespace Tools
{
    public class ResponsiveContainerBuilder
    {
        private RectTransform.Axis _axis = RectTransform.Axis.Vertical;
        private ResponsiveContainer.Behaviour _hBehaviour = ResponsiveContainer.Behaviour.None;
        private ResponsiveContainer.Behaviour _vBehaviour = ResponsiveContainer.Behaviour.None;
        private ResponsiveContainer.HAlignType _hAlign = ResponsiveContainer.HAlignType.Left;
        private ResponsiveContainer.VAlignType _vAlign = ResponsiveContainer.VAlignType.Top;
        private float _spacing;
        private int _marginTop;
        private int _marginBottom;
        private int _marginLeft;
        private int _marginRight;
        private ResponsiveContainer.RefreshType _refreshType = ResponsiveContainer.RefreshType.Manual;

        // Axis

        public ResponsiveContainerBuilder AsVertical()
        {
            _axis = RectTransform.Axis.Vertical;
            return this;
        }

        public ResponsiveContainerBuilder AsHorizontal()
        {
            _axis = RectTransform.Axis.Horizontal;
            return this;
        }

        // Behaviours -- horizontal

        public ResponsiveContainerBuilder WithHorizontalFitInContainer()
        {
            _hBehaviour = ResponsiveContainer.Behaviour.FitContentInContainer;
            return this;
        }

        public ResponsiveContainerBuilder WithHorizontalFitToContent()
        {
            _hBehaviour = ResponsiveContainer.Behaviour.FitContainerToContent;
            return this;
        }

        public ResponsiveContainerBuilder WithHorizontalSpread()
        {
            _hBehaviour = ResponsiveContainer.Behaviour.SpreadContentInContainer;
            return this;
        }

        public ResponsiveContainerBuilder WithHorizontalGroup()
        {
            _hBehaviour = ResponsiveContainer.Behaviour.GroupContentInContainer;
            return this;
        }

        // Behaviours -- vertical

        public ResponsiveContainerBuilder WithVerticalFitInContainer()
        {
            _vBehaviour = ResponsiveContainer.Behaviour.FitContentInContainer;
            return this;
        }

        public ResponsiveContainerBuilder WithVerticalFitToContent()
        {
            _vBehaviour = ResponsiveContainer.Behaviour.FitContainerToContent;
            return this;
        }

        public ResponsiveContainerBuilder WithVerticalSpread()
        {
            _vBehaviour = ResponsiveContainer.Behaviour.SpreadContentInContainer;
            return this;
        }

        public ResponsiveContainerBuilder WithVerticalGroup()
        {
            _vBehaviour = ResponsiveContainer.Behaviour.GroupContentInContainer;
            return this;
        }

        public ResponsiveContainerBuilder WithVerticalGroupAndExpand()
        {
            _vBehaviour = ResponsiveContainer.Behaviour.GroupContentInContainerAndExpand;
            return this;
        }

        // Alignment

        public ResponsiveContainerBuilder WithHorizontalAlign(ResponsiveContainer.HAlignType align)
        {
            _hAlign = align;
            return this;
        }

        public ResponsiveContainerBuilder WithVerticalAlign(ResponsiveContainer.VAlignType align)
        {
            _vAlign = align;
            return this;
        }

        // Spacing

        public ResponsiveContainerBuilder WithSpacing(float spacing)
        {
            _spacing = spacing;
            return this;
        }

        // Margins

        public ResponsiveContainerBuilder WithMargins(int top = 0, int bottom = 0, int left = 0, int right = 0)
        {
            _marginTop = top;
            _marginBottom = bottom;
            _marginLeft = left;
            _marginRight = right;
            return this;
        }

        public ResponsiveContainerBuilder WithBottomMargin(int bottom)
        {
            _marginBottom = bottom;
            return this;
        }

        // Refresh

        public ResponsiveContainerBuilder WithRefreshOnChange()
        {
            _refreshType = ResponsiveContainer.RefreshType.OnChange;
            return this;
        }

        // Apply

        internal void Apply(ResponsiveContainer rc)
        {
            rc.axis = _axis;
            rc.hBehaviour = _hBehaviour;
            rc.vBehaviour = _vBehaviour;
            rc.hAlign = _hAlign;
            rc.vAlign = _vAlign;
            rc.spacing = _spacing;
            rc.margins = new RectOffset(_marginLeft, _marginRight, _marginTop, _marginBottom);
            rc.refreshType = _refreshType;
        }
    }

    public static class ResponsiveContainerBuilderExtensions
    {
        public static PrefabBuilder WithResponsiveContainer(
            this PrefabBuilder builder,
            Action<ResponsiveContainerBuilder> configure)
        {
            var rcb = new ResponsiveContainerBuilder();
            configure(rcb);
            builder.WithComponent<ResponsiveContainer>(rc => rcb.Apply(rc));
            return builder;
        }

        public static PrefabBuilder WithResponsiveContainer(
            this PrefabBuilder builder,
            Action<ResponsiveContainerBuilder> configure,
            out ResponsiveContainer container)
        {
            ResponsiveContainer captured = null;
            var rcb = new ResponsiveContainerBuilder();
            configure(rcb);

            builder.WithComponent<ResponsiveContainer>(rc => {
                        rcb.Apply(rc);
                        captured = rc;
                    }
                );
            container = captured;
            return builder;
        }

        // Exclude from layout

        public static PrefabBuilder ExcludeFromLayout(this PrefabBuilder builder)
        {
            builder
                .GameObject.AddComponent<ResponsiveItem>()
                .settings = new ResponsiveContainer.ResponsiveItemData(
                    ResponsiveContainer.ResponsiveItemData.Type.Exclude
                );

            return builder;
        }

        // Resize

        public static PrefabBuilder ResizeResponsive(this PrefabBuilder builder, bool recursive = true)
        {
            var rc = builder.GameObject.GetComponent<ResponsiveContainer>();

            if (rc != null)
                rc.Resize(recursive);
            return builder;
        }

        public static PrefabBuilder RecalculateResponsiveContainers(this PrefabBuilder builder)
        {
            var rcs = builder.GameObject.GetComponentsInChildren<ResponsiveContainer>(true);

            foreach (var rc in rcs)
            {
                var rt = rc.GetComponent<RectTransform>();

                if (rt != null && rt.sizeDelta.y < 10f)
                    rt.sizeDelta = new Vector2(rt.sizeDelta.x, 10f);

                rc.Resize(true);
            }

            return builder;
        }

        // Child with ResponsiveContainer

        public static PrefabBuilder WithResponsiveChild(
            this PrefabBuilder builder,
            string name,
            Action<ResponsiveContainerBuilder> configure,
            Action<PrefabBuilder> setup = null)
        {
            builder.WithChildObject(name, child => {
                        child.WithResponsiveContainer(configure);
                        setup?.Invoke(child);
                    }
                );
            return builder;
        }
    }
}