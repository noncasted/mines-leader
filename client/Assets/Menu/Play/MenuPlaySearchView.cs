using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Play
{
    [DisallowMultipleComponent]
    public class MenuPlaySearchView : MonoBehaviour
    {
        [SerializeField] private GameObject _idleRoot;
        [SerializeField] private GameObject _searchingRoot;
        [SerializeField] private Button _search;
        [SerializeField] private Button _cancel;
        [SerializeField] private TMP_Text _timer;

        public Button SearchButton => _search;
        public Button CancelButton => _cancel;

        public void ShowIdle()
        {
            _idleRoot.SetActive(true);
            _searchingRoot.SetActive(false);
        }

        public void ShowSearching()
        {
            _idleRoot.SetActive(false);
            _searchingRoot.SetActive(true);
        }

        public void SetTimer(string text)
        {
            _timer.text = text;
        }
    }
}
