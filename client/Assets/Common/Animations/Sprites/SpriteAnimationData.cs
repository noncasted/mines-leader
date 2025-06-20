using System.Collections.Generic;
using UnityEngine;

namespace Common.Animations
{
    public class SpriteAnimationData : ISpriteAnimationData
    {
        public SpriteAnimationData(IReadOnlyList<Sprite> sprites, float time)
        {
            Sprites = sprites;
            Time = time;
        }
        
        public IReadOnlyList<Sprite> Sprites { get; }
        public float Time { get; }
    }
}