using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Common.Animations
{
    [InlineEditor]
    public class RotatableAnimationAsset : EnvAsset
    {
        [SerializeField] private AnimationClip _up;
        [SerializeField] private AnimationClip _diagonalUp;
        [SerializeField] private AnimationClip _side;
        [SerializeField] private AnimationClip _diagonalDown;
        [SerializeField] private AnimationClip _down;
        
        public AnimationClip Up => _up;
        public AnimationClip DiagonalUp => _diagonalUp;
        public AnimationClip Side => _side;
        public AnimationClip DiagonalDown => _diagonalDown;
        public AnimationClip Down => _down;
    }
}