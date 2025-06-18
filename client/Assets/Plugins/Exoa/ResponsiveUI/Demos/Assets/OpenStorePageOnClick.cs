using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Exoa.Responsive.Demos
{
    public class OpenStorePageOnClick : MonoBehaviour
    {
        private Button btn;
        void Start()
        {
            btn = GetComponent<Button>();
            btn.onClick.AddListener(OnClick);
        }

        void OnClick()
        {
            Application.OpenURL("https://u3d.as/2Gg0");
        }
    }
}
