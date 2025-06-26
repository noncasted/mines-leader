using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Exoa.Responsive.Demos
{
    public class AddRemoveItems : MonoBehaviour
    {
        public Button addBtn;
        public Button removeBtn;
        public RectTransform container1;
        public RectTransform container2;
        public int limit = 13;
        public Color[] randomColors;
        public ResponsiveContainer rc1;
        public ResponsiveContainer rc2;

        void Start()
        {
            addBtn.onClick.AddListener(OnClickAdd);
            removeBtn.onClick.AddListener(OnClickRemove);
        }

        private void OnClickAdd()
        {
            GameObject go = null, go2 = null;
            Image img = null;
            if (container1 != null && container1.childCount < limit)
                go = Instantiate(container1.GetChild(0).gameObject, container1);
            if (container2 != null && container2.childCount < limit)
                go2 = Instantiate(container2.GetChild(0).gameObject, container2);

            Color color = Color.red;
            if (randomColors.Length > 0)
            {
                color = randomColors[UnityEngine.Random.Range(0, randomColors.Length)];
            }
            if (go != null && randomColors.Length > 0)
            {
                img = go.GetComponent<Image>();
                if (img != null) img.color = color;
            }
            if (go2 != null && randomColors.Length > 0)
            {
                img = go2.GetComponent<Image>();
                if (img != null) img.color = color;
            }
            DisplayButtons();

            // Update the two Responsive Containers
            if (rc1 != null) rc1.Resize(true);
            if (rc2 != null) rc2.Resize(true);
        }

        private void DisplayButtons()
        {
            if (container1 != null)
            {
                addBtn.gameObject.SetActive(container1.childCount < limit);
                removeBtn.gameObject.SetActive(container1.childCount > 1);
            }
            else if (container2 != null)
            {
                addBtn.gameObject.SetActive(container2.childCount < limit);
                removeBtn.gameObject.SetActive(container2.childCount > 1);
            }
        }

        private void OnClickRemove()
        {
            if (container1 != null && container1.childCount > 1)
                Remove(container1.GetChild(0));
            if (container2 != null && container2.childCount > 1)
                Remove(container2.GetChild(0));

            // Update the two Responsive Containers
            if (rc1 != null) rc1.Resize(true);
            if (rc2 != null) rc2.Resize(true);

            DisplayButtons();

            //UnityEditor.EditorApplication.isPaused = true;

        }

        private void Remove(Transform tr)
        {
            tr.parent = null;
            tr.gameObject.SetActive(false);
            Destroy(tr.gameObject);
        }
    }
}