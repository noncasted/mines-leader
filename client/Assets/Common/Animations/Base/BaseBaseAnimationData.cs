using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Common.Animations
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "AnimationData", menuName = "Common/Animator/Animation")]
    public class BaseBaseAnimationData : EnvAsset, IBaseAnimationData
    {
        [SerializeField] private AnimationClip _clip;
        [SerializeField] private float _time;
        [SerializeField] private float _fadeDuration;
        [SerializeField] private LayerDefinition _layer;

        public AnimationClip Clip => _clip;
        public float Time => _time;
        public float FadeDuration => _fadeDuration;
    }
}