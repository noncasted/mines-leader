using System;
using Internal;
using Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Profile
{
    /// <summary>
    /// Строка списка матчей: "{Режим} vs {Соперник}" и цветной тег результата.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuProfileHistoryEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _result;
        [SerializeField] private Image _background;
        [SerializeField] private Button _button;

        [SerializeField] private string _winText = "Win";
        [SerializeField] private string _loseText = "Lose";
        [SerializeField] private Color _winColor = new(0.458823532f, 0.654902f, 0.2627451f, 1f);
        [SerializeField] private Color _loseColor = new(0.647058845f, 0.1882353f, 0.1882353f, 1f);
        [SerializeField] private Color _selectedColor = Color.white;
        [SerializeField] private Color _normalColor = new(1f, 1f, 1f, 0f);

        public ProfileMatchEntry Match { get; private set; }

        public void Setup(ProfileMatchEntry match, string modeName)
        {
            Match = match;

            var opponent = string.IsNullOrEmpty(match.OpponentName) ? "Unknown" : match.OpponentName;
            _title.text = $"{modeName} vs {opponent}";

            _result.text = match.Won == true ? _winText : _loseText;
            _result.color = match.Won == true ? _winColor : _loseColor;

            SetSelected(false);
        }

        public void Bind(IReadOnlyLifetime lifetime, Action<MenuProfileHistoryEntry> clicked)
        {
            if (_button == null)
            {
                Debug.LogError($"[MenuProfileHistoryEntry] {name} has no Button, match is not selectable");
                return;
            }

            _button.ListenClick(lifetime, () => clicked.Invoke(this));
        }

        public void SetSelected(bool selected)
        {
            if (_background == null)
                return;

            _background.color = selected == true ? _selectedColor : _normalColor;
        }
    }
}
