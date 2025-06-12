using System;
using System.Collections.Generic;
using UnityEngine;

namespace Common.Animations
{
    public class RotatableFrameProvider : IFrameProvider
    {
        public RotatableFrameProvider(
            IAnimationRotationProvider rotationProvider,
            IReadOnlyDictionary<FiveAnimationDirection, ISpriteAnimationData> animations)
        {
            _rotationProvider = rotationProvider;
            _animations = animations;
            FrameCount = animations[0].Sprites.Count;

            foreach (var animation in animations.Values)
            {
                if (animation.Sprites.Count != FrameCount)
                    throw new ArgumentException("All animations must have the same number of frames");
            }
        }

        private readonly IAnimationRotationProvider _rotationProvider;
        private readonly IReadOnlyDictionary<FiveAnimationDirection, ISpriteAnimationData> _animations;

        public int FrameCount { get; }

        public Sprite GetFrame(int index)
        {
            var direction = AngleToDirection(_rotationProvider.AnimationAngle);
            var animation = _animations[direction];
            return animation.Sprites[index];
        }

        private FiveAnimationDirection AngleToDirection(float angle)
        {
            return angle switch
            {
                < 22.5f or >= 337.5f => FiveAnimationDirection.Side,
                >= 22.5f and < 67.5f => FiveAnimationDirection.Diagonal_Up,
                >= 67.5f and < 112.5f => FiveAnimationDirection.Up,
                >= 112.5f and < 157.5f => FiveAnimationDirection.Diagonal_Up,
                >= 157.5f and < 202.5f => FiveAnimationDirection.Side,
                >= 202.5f and < 247.5f => FiveAnimationDirection.Diagonal_Down,
                >= 247.5f and < 292.5f => FiveAnimationDirection.Down,
                >= 292.5f and < 337.5f => FiveAnimationDirection.Diagonal_Down,
                _ => throw new ArgumentException("Invalid angle")
            };
        }
    }
}