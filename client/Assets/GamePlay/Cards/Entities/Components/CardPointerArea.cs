using UnityEngine;

namespace GamePlay.Cards
{
    // The card area under the pointer instead of BoxCollider2D: a rectangle in the object's local coordinates,
    // so card rotation and scale are accounted for automatically.
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
