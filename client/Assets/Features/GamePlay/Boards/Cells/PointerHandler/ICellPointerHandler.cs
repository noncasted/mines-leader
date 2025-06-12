using UnityEngine;

namespace GamePlay.Boards
{
    public interface ICellPointerHandler
    {
        bool IsInside(Vector2 pointerPosition);
    }
}