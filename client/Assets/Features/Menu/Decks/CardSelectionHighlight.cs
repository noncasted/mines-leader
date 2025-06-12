using UnityEngine;
using UnityEngine.UI;

namespace Menu
{
    [DisallowMultipleComponent]
    public class CardSelectionHighlight : MonoBehaviour
    {
        [SerializeField] private GameObject _outline;
        
        public void OnSelected()
        {
            _outline.SetActive(true);
        }
        
        public void OnDeselected()
        {
            _outline.SetActive(false);
        }
    }
}
