using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

namespace Exoa
{
    [AddComponentMenu("Layout/Content Size Fitter", 141)]
    [ExecuteInEditMode]
    [RequireComponent(typeof(RectTransform))]
    public class ContentSizeFitter : UIBehaviour, ILayoutSelfController
    {
        public enum FitMode
        {
            Unconstrained,
            MinSize,
            PreferredSize,
            TextRenderSize
        }

        [SerializeField] protected FitMode m_HorizontalFit = FitMode.Unconstrained;
        public FitMode horizontalFit { get { return m_HorizontalFit; } set { m_HorizontalFit = value; SetDirty(); } }

        [SerializeField] protected FitMode m_VerticalFit = FitMode.Unconstrained;
        public FitMode verticalFit { get { return m_VerticalFit; } set { m_VerticalFit = value; SetDirty(); } }

        [System.NonSerialized] private RectTransform m_Rect;
        [System.NonSerialized] private TMP_Text m_Text;
        private RectTransform rectTransform
        {
            get
            {
                if (m_Rect == null)
                    m_Rect = GetComponent<RectTransform>();
                return m_Rect;
            }
        }
        private TMP_Text text
        {
            get
            {
                if (m_Text == null)
                    m_Text = GetComponent<TMP_Text>();
                return m_Text;
            }
        }

        private DrivenRectTransformTracker m_Tracker;

        protected ContentSizeFitter()
        { }

        #region Unity Lifetime calls

        protected override void OnEnable()
        {
            base.OnEnable();
            SetDirty();
        }

        protected override void OnDisable()
        {
            m_Tracker.Clear();
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            base.OnDisable();
        }

        #endregion

        protected override void OnRectTransformDimensionsChange()
        {
            SetDirty();
        }

        void Update()
        {
            if (horizontalFit == FitMode.TextRenderSize) HandleSelfFittingAlongAxis(RectTransform.Axis.Horizontal);
            if (verticalFit == FitMode.TextRenderSize) HandleSelfFittingAlongAxis(RectTransform.Axis.Vertical);
        }
        private void HandleSelfFittingAlongAxis(RectTransform.Axis axis)
        {
            FitMode fitting = (axis == 0 ? horizontalFit : verticalFit);
            if (fitting == FitMode.Unconstrained)
                return;

            m_Tracker.Add(this, rectTransform, (axis == 0 ? DrivenTransformProperties.SizeDeltaX : DrivenTransformProperties.SizeDeltaY));

            // Set size to min or preferred size
            if (fitting == FitMode.MinSize)
                rectTransform.SetSizeWithCurrentAnchors((RectTransform.Axis)axis, LayoutUtility.GetMinSize(m_Rect, (int)axis));
            else if (fitting == FitMode.TextRenderSize)
                rectTransform.SetSizeWithCurrentAnchors((RectTransform.Axis)axis, GetRenderedTextSize(m_Rect, (RectTransform.Axis)axis));
            else
                rectTransform.SetSizeWithCurrentAnchors((RectTransform.Axis)axis, LayoutUtility.GetPreferredSize(m_Rect, (int)axis));
        }

        private float GetRenderedTextSize(RectTransform m_Rect, RectTransform.Axis axis)
        {
            Vector2 size = text.GetRenderedValues(true);
            return axis == RectTransform.Axis.Horizontal ? size.x : size.y;
        }

        public virtual void SetLayoutHorizontal()
        {
            m_Tracker.Clear();
            HandleSelfFittingAlongAxis(RectTransform.Axis.Horizontal);
        }

        public virtual void SetLayoutVertical()
        {
            HandleSelfFittingAlongAxis(RectTransform.Axis.Vertical);
        }

        protected void SetDirty()
        {
            if (!IsActive())
                return;

            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            SetDirty();
        }

#endif
    }
}