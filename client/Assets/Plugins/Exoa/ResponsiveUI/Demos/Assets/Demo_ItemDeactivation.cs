using Exoa.Responsive;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Exoa.Responsive.Demos
{
    public class Demo_ItemDeactivation : MonoBehaviour
    {
        private ResponsiveContainer rc;

        void Start()
        {
            rc = GetComponent<ResponsiveContainer>();
        }

        // Update is called once per frame
        void Update()
        {
            Transform tr = null;
            if (Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.U))
            {
                tr = transform.GetChild(0);
                tr.gameObject.SetActive(!tr.gameObject.activeSelf);
                rc.Resize(true);
            }
            if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.I))
            {
                tr = transform.GetChild(1);
                tr.gameObject.SetActive(!tr.gameObject.activeSelf);
                rc.Resize(true);
            }
            if (Input.GetKeyDown(KeyCode.Keypad3) || Input.GetKeyDown(KeyCode.O))
            {
                tr = transform.GetChild(2);
                tr.gameObject.SetActive(!tr.gameObject.activeSelf);
                rc.Resize(true);
            }
            if (Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.P))
            {
                tr = transform.GetChild(3);
                tr.gameObject.SetActive(!tr.gameObject.activeSelf);
                rc.Resize(true);
            }
        }
    }
}
