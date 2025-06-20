using System.Collections.Generic;
using Internal;
using UnityEngine;

namespace Common.Animations
{
    public static class SpriteAnimationExtensions
    {
        public static void RegisterSpriteRotatableAnimation<T>(
            this IEntityBuilder builder,
            RotatableAnimationAsset asset) where T : RotatableSpriteAnimation
        {
            var time = asset.Up.length;
            var animations = new Dictionary<FiveAnimationDirection, ISpriteAnimationData>
            {
                { FiveAnimationDirection.Up, asset.Up.CreateSpriteAnimationData() },
                { FiveAnimationDirection.Diagonal_Up, asset.DiagonalUp.CreateSpriteAnimationData() },
                { FiveAnimationDirection.Side, asset.Side.CreateSpriteAnimationData() },
                { FiveAnimationDirection.Diagonal_Down, asset.DiagonalDown.CreateSpriteAnimationData() },
                { FiveAnimationDirection.Down, asset.Down.CreateSpriteAnimationData() }
            };

            var options = new RotatableSpriteAnimation.Options(time, animations);

            builder.Register<T>()
                .As<IScopeSetup>()
                .WithParameter(options);
        }

        public static void RegisterSpriteForwardAnimation<T>(this IEntityBuilder builder, ForwardAnimationAsset asset)
            where T : ForwardSpriteAnimation
        {
            var data = new SpriteAnimationData(asset.Sprites, asset.Time);

            builder.Register<T>()
                .As<IScopeSetup>()
                .WithParameter<ISpriteAnimationData>(data);
        }

        public static void RegisterSpriteForwardAnimation<T>(this IEntityBuilder builder, ForwardAnimationData data)
            where T : ForwardSpriteAnimation
        {
            var animationData = new SpriteAnimationData(data.Sprites, data.Time);

            builder.Register<T>()
                .As<IScopeSetup>()
                .WithParameter<ISpriteAnimationData>(animationData);
        }

        public static ISpriteAnimationData CreateSpriteAnimationData(this AnimationClip animation)
        {
            var time = animation.length;
            var frames = animation.GetSprites();

            return new SpriteAnimationData(frames, time);
        }

        public static IReadOnlyList<Sprite> GetSprites(this AnimationClip clip)
        {
            // var sprites = new List<Sprite>();
            // var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            //
            // foreach (var binding in bindings)
            // {
            //     var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            //
            //     foreach (var frame in frames)
            //     {
            //         sprites.Add((Sprite)frame.value);
            //     }
            // }
            //
            // return sprites;

            return null;
        }
    }
}