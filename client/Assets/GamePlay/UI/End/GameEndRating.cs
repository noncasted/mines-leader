using TMPro;
using UnityEngine;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class GameEndRating : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;

        [SerializeField] private Color _lossColor = Color.red;
        [SerializeField] private Color _winColor = Color.green;

        public void Show(int current, int change)
        {
            var sign = change > 0 ? "+" : "-";
            var color = change > 0 ? _winColor : _lossColor;
            var colorHex = ColorUtility.ToHtmlStringRGB(color);

            _text.text = $"{current}<color=#{colorHex}>{sign}{change}</color>";
        }
    }
}