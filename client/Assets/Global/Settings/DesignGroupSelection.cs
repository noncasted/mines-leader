using Internal;
using UnityEngine;

namespace Global.Settings
{
    public enum SelectionGroupValue
    {
        On,
        Off
    }

    public class DesignGroupSelection : MonoBehaviour
    {
        private readonly ViewableProperty<SelectionGroupValue> _value = new();

        public IViewableProperty<SelectionGroupValue> Value => _value;

        public void Set(SelectionGroupValue value)
        {
            _value.Set(value);
            Refresh();
        }

        private void Refresh()
        {
            
        }
    }
}