using System;
using Internal;
using Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Progression
{
    [DisallowMultipleComponent]
    public class ProgressionMilestone : MonoBehaviour
    {
        [SerializeField] private Button _openButton;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _xpLabel;
        [SerializeField] private Image _background;

        [SerializeField] private Sprite _taken;
        [SerializeField] private Sprite _active;
        [SerializeField] private Sprite _locked;

        private RectTransform _rectTransform;
        private IProgressionMilestone _milestone;

        public Button Button => _openButton;
        public IProgressionMilestone Milestone => _milestone;

        public void Setup(
            IProgressionMilestone milestone,
            IReadOnlyLifetime lifetime,
            Vector2 normalizedPosition)
        {
            _openButton.gameObject.SetActive(false);
            _milestone = milestone;
            _rectTransform = GetComponent<RectTransform>();

            _xpLabel.text = $"{milestone.Required}";
            _rectTransform.anchoredPosition = normalizedPosition;

            milestone.Status.View(lifetime, UpdateVisual);
        }

        private void UpdateVisual(ProgressionMilestoneStatus status)
        {
            switch (status)
            {

                case ProgressionMilestoneStatus.Locked:
                    _openButton.gameObject.SetActive(false);
                    _icon.sprite = _locked;
                    break;
                case ProgressionMilestoneStatus.Active:
                    _openButton.gameObject.SetActive(true);
                    _icon.sprite = _active;
                    break;
                case ProgressionMilestoneStatus.Unlocked:
                    _openButton.gameObject.SetActive(false);
                    _icon.sprite = _taken;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }
    }
}
