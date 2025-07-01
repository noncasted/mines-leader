using System;
using Global.UI;
using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Global.Settings
{
    public enum SelectionGroupValue
    {
        On,
        Off
    }

    [DisallowMultipleComponent]
    public class DesignGroupSelection : MonoBehaviour
    {
        [SerializeField] private Color _textOnColor;
        [SerializeField] private Color _textOffColor;
        [SerializeField] private Color _plateOnColor;
        [SerializeField] private Color _plateOffColor;

        [SerializeField] private TMP_Text _onText;
        [SerializeField] private MaskableGraphic _onPlate;
        [SerializeField] private TMP_Text _offText;
        [SerializeField] private MaskableGraphic _offPlate;

        [SerializeField] private DesignButton _onButton;
        [SerializeField] private DesignButton _offButton;
        
        private readonly ViewableProperty<SelectionGroupValue> _value = new();

        public IViewableProperty<SelectionGroupValue> Value => _value;

        private void OnEnable()
        {
            var lifetime = this.GetObjectLifetime();
            
            _onButton.ListenClick(lifetime, () => Set(SelectionGroupValue.On));
            _offButton.ListenClick(lifetime, () => Set(SelectionGroupValue.Off));
        }

        public void Set(SelectionGroupValue value)
        {
            _value.Set(value);
            Refresh();
        }

        private void Refresh()
        {
            switch (_value.Value)
            {
                case SelectionGroupValue.On:
                    _onPlate.color = _plateOnColor;
                    _onText.color = _textOnColor;
                    _offPlate.color = _plateOffColor;
                    _offText.color = _textOffColor;
                    break;
                case SelectionGroupValue.Off:
                    _onPlate.color = _plateOffColor;
                    _onText.color = _textOffColor;
                    _offPlate.color = _plateOnColor;
                    _offText.color = _textOnColor;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}