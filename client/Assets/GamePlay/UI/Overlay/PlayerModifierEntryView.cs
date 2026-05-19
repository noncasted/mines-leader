using Internal;
using Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class PlayerModifierEntryView : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _turnsText;
        [SerializeField] private UIElementPointerHandler _pointerHandler;

        private DurationalModifierOverview _overview;

        public DurationalModifierOverview Overview => _overview;
        public UIElementPointerHandler PointerHandler => _pointerHandler;

        public void Setup(DurationalModifierOverview overview, Sprite icon)
        {
            _overview = overview;
            _iconImage.sprite = icon;
            UpdateTurns(overview.TurnsToEnd);
        }

        public void UpdateTurns(int turnsToEnd)
        {
            if (turnsToEnd < 0)
            {
                _turnsText.gameObject.SetActive(false);
            }
            else
            {
                _turnsText.gameObject.SetActive(true);
                _turnsText.text = turnsToEnd.ToString();
            }
        }
    }
}
