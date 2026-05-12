using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Screens
{
    [DisallowMultipleComponent]
    public class ProgressionMilestone : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _xpLabel;
        [SerializeField] private Image _background;
        [SerializeField] private Button _button;
        [SerializeField] private Color _lockedColor = new(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color _reachedColor = new(0.4f, 0.5f, 0.45f, 1f);
        [SerializeField] private Color _availableColor = new(1f, 0.84f, 0f, 1f);
        [SerializeField] private Color _claimedColor = new(0.3f, 0.6f, 0.35f, 0.7f);

        private RectTransform _rectTransform;
        private bool _reached;
        private bool _claimed;
        private bool _hasAvailableBox;
        private Guid _boxId;

        public int RequiredXp { get; private set; }
        public Button Button => _button;
        public Guid BoxId => _boxId;
        public bool HasAvailableBox => _hasAvailableBox;

        public void Setup(int requiredXp, float normalizedPosition)
        {
            RequiredXp = requiredXp;
            _rectTransform = GetComponent<RectTransform>();

            _xpLabel.text = $"{requiredXp}";

            _rectTransform.anchorMin = new Vector2(normalizedPosition, 0);
            _rectTransform.anchorMax = new Vector2(normalizedPosition, 1);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.anchoredPosition = Vector2.zero;
            _rectTransform.sizeDelta = new Vector2(60, 0);

            UpdateVisual();
        }

        public void SetReached(bool reached)
        {
            _reached = reached;
            UpdateVisual();
        }

        public void SetClaimed(bool claimed)
        {
            _claimed = claimed;
            UpdateVisual();
        }

        public void SetAvailableBox(Guid boxId)
        {
            _boxId = boxId;
            _hasAvailableBox = boxId != Guid.Empty;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (!_reached)
            {
                _background.color = _lockedColor;
                _icon.color = _lockedColor;
            }
            else if (_hasAvailableBox)
            {
                _background.color = _availableColor;
                _icon.color = Color.white;
            }
            else if (_claimed)
            {
                _background.color = _claimedColor;
                _icon.color = _claimedColor;
            }
            else
            {
                // Reached but box not awarded yet
                _background.color = _reachedColor;
                _icon.color = _reachedColor;
            }
        }
    }
}
