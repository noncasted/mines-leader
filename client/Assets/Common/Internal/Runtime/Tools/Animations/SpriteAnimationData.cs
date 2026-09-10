using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    public interface ISpriteAnimationData
    {
        IReadOnlyList<Sprite> Sprites { get; }
        float Time { get; }
        Color Color { get; }
    }

    public class SpriteAnimationData : ISpriteAnimationData
    {
        public SpriteAnimationData(IReadOnlyList<Sprite> sprites, float time, Color? color = null)
        {
            Sprites = sprites;
            Time = time;
            Color = color ?? Color.white;
        }

        public IReadOnlyList<Sprite> Sprites { get; }
        public float Time { get; }
        public Color Color { get; }
    }
}