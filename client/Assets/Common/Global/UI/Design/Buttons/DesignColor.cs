using UnityEngine;

namespace Global.UI
{
    [CreateAssetMenu(fileName = "DesignColor", menuName = "UI/Design/Color")]
    public class DesignColor : ScriptableObject
    {
        [SerializeField] private Color _color;

        public Color Color => _color;
    }
}