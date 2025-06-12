
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardPositionHandle
    {
        Vector2 SupposedPosition { get; }
        float SupposedRotation { get; }
        int SupposedRenderOrder { get; }
    }
}