using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Exoa.Responsive;

namespace Exoa.Responsive.Demos
{
    public class ResponsiveButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private ResponsiveItem ri;
        private RectTransform rt;
        private ResizeByRatio rbr;
        private Vector2 prevSizeDelta;
        public float targetSize = 100f;
        public RectTransform.Axis axis;
        private ResponsiveContainer.ResponsiveItemData.Type prevState;
        private bool animatingEnter;
        private bool animatingExit;
        private Vector2 target;
        private float lerp = .1f;

        public void OnPointerEnter(PointerEventData eventData)
        {
            prevSizeDelta = rt.sizeDelta;
            prevState = ri.settings.type;
            ri.settings.type = ResponsiveContainer.ResponsiveItemData.Type.Prefered;
            animatingEnter = true;
            animatingExit = false;
            target = axis == RectTransform.Axis.Vertical ? new Vector2(rt.sizeDelta.x, targetSize) : new Vector2(targetSize, rt.sizeDelta.y);

        }

        public void OnPointerExit(PointerEventData eventData)
        {
            animatingEnter = false;
            animatingExit = true;

        }

        void Start()
        {
            ri = GetComponent<ResponsiveItem>();
            rbr = GetComponent<ResizeByRatio>();
            rt = GetComponent<RectTransform>();
        }

        void Update()
        {
            if (animatingEnter)
            {
                rt.sizeDelta = Vector2.Lerp(rt.sizeDelta, target, lerp);
                if (rbr != null) rbr.Resize(true);
            }
            if (animatingExit)
            {
                rt.sizeDelta = Vector2.Lerp(rt.sizeDelta, prevSizeDelta, lerp);
                if (rbr != null) rbr.Resize(true);
                if (Vector2.Distance(rt.sizeDelta, prevSizeDelta) < 1)
                {
                    animatingExit = false;
                    ri.settings.type = prevState;
                }
            }
        }

    }
}
