using UnityEngine;

namespace GamePlay.Boards
{
    public interface ICellPointerHandler
    {
        bool IsInside(Vector2 pointerPosition);
    }

    [DisallowMultipleComponent]
    public class CellPointerHandler : MonoBehaviour, ICellPointerHandler
    {
        [SerializeField] private Vector2 _size = Vector2.one;

        public bool IsInside(Vector2 pointerPosition)
        {
            var position = (Vector2)transform.position;

            var leftBottom = position - _size / 2f;
            var rightTop = position + _size / 2f;

            return leftBottom.x <= pointerPosition.x &&
                   pointerPosition.x <= rightTop.x &&
                   leftBottom.y <= pointerPosition.y &&
                   pointerPosition.y <= rightTop.y;
        }
    }
}