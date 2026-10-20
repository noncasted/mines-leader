using UnityEngine;

namespace GamePlay.Cards
{
    // Область карты под указателем вместо BoxCollider2D: прямоугольник в локальных координатах
    // объекта, поэтому повороты и масштаб карты учитываются сами.
    [DisallowMultipleComponent]
    public class CardPointerArea : MonoBehaviour
    {
        [SerializeField] private Vector2 _size = new(3f, 4f);

        public bool IsActive => isActiveAndEnabled;

        public bool IsInside(Vector2 pointerPosition)
        {
            var local = (Vector2)transform.InverseTransformPoint(pointerPosition);
            var half = _size / 2f;

            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
        }
    }
}
