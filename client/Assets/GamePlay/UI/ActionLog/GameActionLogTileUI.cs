using System;
using MPUIKIT;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamePlay.UI.ActionLog
{
    public class GameActionLogTileUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private MPImage _background;
        [SerializeField] private TMP_Text _messageText;

        private static readonly Color SelfColor = new(0.18f, 0.35f, 0.58f, 0.9f);
        private static readonly Color OpponentColor = new(0.58f, 0.22f, 0.18f, 0.9f);

        private GameActionLogEntry _entry;
        private Action<GameActionLogTileUI> _onHoverEnter;
        private Action<GameActionLogTileUI> _onHoverExit;

        public GameActionLogEntry Entry => _entry;
        public RectTransform RectTransform => (RectTransform)transform;

        public void Setup(
            GameActionLogEntry entry,
            Action<GameActionLogTileUI> onHoverEnter,
            Action<GameActionLogTileUI> onHoverExit)
        {
            _entry = entry;
            _onHoverEnter = onHoverEnter;
            _onHoverExit = onHoverExit;

            _messageText.text = $"{entry.PlayerName}: {entry.CardName}";
            _background.color = entry.Type == GameActionLogEntryType.CardPlayedSelf ? SelfColor : OpponentColor;
        }

        public void SetOpacity(float alpha)
        {
            _canvasGroup.alpha = alpha;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _onHoverEnter?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _onHoverExit?.Invoke(this);
        }
    }
}