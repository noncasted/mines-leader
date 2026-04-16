using System.Collections.Generic;
using UnityEngine;

namespace Animations
{
    public class SpriteAnimationData : ISpriteAnimationData
    {
        public SpriteAnimationData(IReadOnlyList<Sprite> sprites, float time, Color? color = null)
        {
            Sprites = sprites;
            Time = time;
            Color = color ?? UnityEngine.Color.white;
        }

        public IReadOnlyList<Sprite> Sprites { get; }
        public float Time { get; }
        public Color Color { get; }
    }
}
