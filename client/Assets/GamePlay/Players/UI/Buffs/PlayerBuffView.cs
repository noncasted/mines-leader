using GamePlay.UI;
using Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.Players.Buffs
{
    [DisallowMultipleComponent]
    public class PlayerBuffView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _turnsText;
        [SerializeField] private UIElementPointerHandler _pointerHandler;

        public UIElementPointerHandler PointerHandler => _pointerHandler;

        public void Setup(DurationalModifierOverview overview, Sprite icon)
        {
            if (_icon != null)
                _icon.sprite = icon;

            Refresh(overview);
        }

        public void Refresh(DurationalModifierOverview overview)
        {
            if (_turnsText == null)
                return;

            if (overview.TurnsToEnd < 0)
            {
                _turnsText.gameObject.SetActive(false);
                return;
            }

            _turnsText.gameObject.SetActive(true);
            _turnsText.text = overview.TurnsToEnd.ToString();
        }
    }
}
