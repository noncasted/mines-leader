using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Common.Animations
{
    [InlineEditor]
    public class ScriptableBaseAnimationData : EnvAsset, IBaseAnimationData
    {
        [SerializeField] private AnimationClip _clip;
        [SerializeField] private FloatValue _time;
        [SerializeField] private float _fadeDuration;
        [SerializeField] private LayerDefinition _layer;

        public int AssetId => Id;
        public AnimationClip Clip => _clip;
        public float Time => _time;
        public float FadeDuration => _fadeDuration;
        public int Layer => _layer.Value;
    }
}