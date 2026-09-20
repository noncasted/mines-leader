using UnityEngine;

namespace Internal
{
    [CreateAssetMenu(fileName = "FloatValue", menuName = "Structs/FloatValue")]
    public class FloatValue : ScriptableObject
    {
        [SerializeField] private float _value;

        public float Value => _value;

        public static implicit operator float(FloatValue value)
        {
            return value.Value;
        }
    }
}