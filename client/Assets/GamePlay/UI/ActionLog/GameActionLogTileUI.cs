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

        private static readonly Color CardSelfColor = new(0.18f, 0.35f, 0.58f, 0.9f);
        private static readonly Color CardOpponentColor = new(0.58f, 0.22f, 0.18f, 0.9f);
        private static readonly Color ManaColor = new(0.28f, 0.22f, 0.58f, 0.9f);
        private static readonly Color HealthColor = new(0.18f, 0.48f, 0.28f, 0.9f);
        private static readonly Color MovesColor = new(0.55f, 0.42f, 0.18f, 0.9f);

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

            _messageText.text = $"{entry.PlayerName}: {entry.Message}";
            _background.color = GetColor(entry.Type);
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

        private static Color GetColor(GameActionLogEntryType type)
        {
            return type switch
            {
                GameActionLogEntryType.CardPlayedSelf => CardSelfColor,
                GameActionLogEntryType.CardPlayedOpponent => CardOpponentColor,
                GameActionLogEntryType.ManaChanged => ManaColor,
                GameActionLogEntryType.HealthChanged => HealthColor,
                GameActionLogEntryType.MaxMovesChanged => MovesColor,
                _ => CardSelfColor,
            };
        }
    }
}