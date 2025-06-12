using UnityEngine;

namespace Common.Animations
{
    public interface IBaseAnimationData
    {
        AnimationClip Clip { get; }
        float Time { get; }
        float FadeDuration { get; }
    }
}