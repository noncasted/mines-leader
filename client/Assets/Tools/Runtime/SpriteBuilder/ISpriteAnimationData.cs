using System.Collections.Generic;
using UnityEngine;

namespace Tools
{
    public interface ISpriteAnimationData
    {
        IReadOnlyList<Sprite> Sprites { get; }
        float Time { get; }
        Color Color { get; }
    }
}
