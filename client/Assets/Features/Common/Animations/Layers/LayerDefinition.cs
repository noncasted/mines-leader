using Sirenix.OdinInspector;
using UnityEngine;

namespace Common.Animations
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "AnimatorLayer", menuName = "Common/Animator/AnimatorLayer")]
    public class LayerDefinition : ScriptableObject, ILayerDefinition
    {
        [SerializeField] private int _value;

        public int Value => _value;
    }
}