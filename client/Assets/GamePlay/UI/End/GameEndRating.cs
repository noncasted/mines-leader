using TMPro;
using UnityEngine;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class GameEndRating : MonoBehaviour
    {
        [SerializeField] private TMP_Text _current;
        [SerializeField] private TMP_Text _change;

        [SerializeField] private Color _lossColor = Color.softRed;
        [SerializeField] private Color _winColor = Color.mediumSeaGreen;

        public void Show(int current, int change)
        {
            _current.text = current.ToString();
            _change.text = change > 0 ? "+" : "-" + change;
            _change.color = change > 0 ? _lossColor : _winColor;
        }
    }
}