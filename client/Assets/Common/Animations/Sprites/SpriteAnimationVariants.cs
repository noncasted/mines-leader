using System.Collections.Generic;
using Global.Systems;
using UnityEngine;
using VContainer.Internal;

namespace Animations
{
    public class ForwardSpriteAnimation : SpriteAnimation
    {
        public ForwardSpriteAnimation(Utils utils, ISpriteAnimationData data) : base(
                utils.Updater,
                utils.Renderer,
                data.Time,
                data.Color,
                new ForwardFrameProvider(data.Sprites)
            )
        {
        }

        public class Utils
        {
            public Utils(IUpdater updater, ContainerLocal<ISpriteAnimationRenderer> renderer)
            {
                Updater = updater;
                Renderer = renderer.Value;
            }

            public IUpdater Updater { get; }
            public ISpriteAnimationRenderer Renderer { get; }
        }
    }

    public class RotatableSpriteAnimation : SpriteAnimation
    {
        public RotatableSpriteAnimation(Utils utils, Options options) : base(
                utils.Updater,
                utils.Renderer,
                options.Time,
                options.Color,
                new RotatableFrameProvider(utils.RotationProvider, options.Animations)
            )
        {
        }

        public class Options
        {
            public Options(
                float time,
                IReadOnlyDictionary<FiveAnimationDirection, ISpriteAnimationData> animations,
                Color? color = null)
            {
                Time = time;
                Animations = animations;
                Color = color ?? UnityEngine.Color.white;
            }

            public float Time { get; }
            public IReadOnlyDictionary<FiveAnimationDirection, ISpriteAnimationData> Animations { get; }
            public Color Color { get; }
        }

        public class Utils
        {
            public Utils(
                IUpdater updater,
                IAnimationRotationProvider rotationProvider,
                ContainerLocal<ISpriteAnimationRenderer> renderer)
            {
                Updater = updater;
                RotationProvider = rotationProvider;
                Renderer = renderer.Value;
            }

            public IUpdater Updater { get; }
            public IAnimationRotationProvider RotationProvider { get; }
            public ISpriteAnimationRenderer Renderer { get; }
        }
    }
}
