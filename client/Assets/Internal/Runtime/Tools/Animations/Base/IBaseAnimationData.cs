using UnityEngine;

namespace Internal
{
    public interface IBaseAnimationData
    {
        AnimationClip Clip { get; }
        float Time { get; }
        float FadeDuration { get; }
    }
}