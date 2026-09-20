using UnityEngine;

namespace Global.UI
{
    /// <summary>
    /// Optional per child settings for <see cref="DesignContainer"/>.
    /// Without it a child takes its own size (or a weight of 1 in <see cref="DesignMainAxisMode.Fill"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public class DesignItem : MonoBehaviour
    {
        [SerializeField] private DesignItemSizing _sizing = DesignItemSizing.Preferred;

        [Tooltip("Fraction of the container for Percent, relative share of the free space for Weight.")]
        [SerializeField, Min(0f)] private float _value = 1f;

        [SerializeField] private bool _overrideCrossAxis;
        [SerializeField] private DesignCrossAxisMode _crossAxis = DesignCrossAxisMode.Keep;

        public DesignItemSizing Sizing => _sizing;

        public float Percent => Mathf.Clamp01(_value);

        public float Weight => Mathf.Max(0f, _value);

        public bool TryGetCrossAxis(out DesignCrossAxisMode mode)
        {
            mode = _crossAxis;
            return _overrideCrossAxis;
        }

        public void SetSizing(DesignItemSizing sizing, float value = 1f)
        {
            _sizing = sizing;
            _value = value;
            MarkContainerDirty();
        }

        public void SetCrossAxis(DesignCrossAxisMode mode)
        {
            _overrideCrossAxis = true;
            _crossAxis = mode;
            MarkContainerDirty();
        }

        public void ClearCrossAxis()
        {
            _overrideCrossAxis = false;
            MarkContainerDirty();
        }

        private void OnEnable() => MarkContainerDirty();

        private void OnDisable() => MarkContainerDirty();

        private void OnValidate() => MarkContainerDirty();

        private void MarkContainerDirty()
        {
            if (transform.parent != null && transform.parent.TryGetComponent(out DesignLayoutBase layout))
                layout.SetDirty();
        }
    }
}
