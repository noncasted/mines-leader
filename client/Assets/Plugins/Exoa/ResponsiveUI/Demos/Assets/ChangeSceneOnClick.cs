using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Exoa.Responsive.Demos
{
    public class ChangeSceneOnClick : MonoBehaviour
    {
        private Button btn;
        public string sceneName;
        void Start()
        {
            btn = GetComponent<Button>();
            btn.onClick.AddListener(OnClickBtn);
        }

        void OnClickBtn()
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
