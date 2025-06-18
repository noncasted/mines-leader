using System.Collections.Generic;
using UnityEngine;

namespace Common.Animations
{
    public interface ISpriteAnimationData
    {
        IReadOnlyList<Sprite> Sprites { get; }
        float Time { get; }
    }
}