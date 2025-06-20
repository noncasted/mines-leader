using Internal;
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
        [SerializeField] private BoxCollider2D _collider;
        
        private readonly ViewableProperty<bool> _isSelected = new();
        
        public IViewableProperty<bool> IsSelected => _isSelected;

        private void OnMouseDown()
        {
            _isSelected.Set(true);
        }

        private void OnMouseUp()
        {
            _isSelected.Set(false);
        }

        public bool IsInside(Vector2 pointerPosition)
        {
            var size = _collider.size;
            var position = (Vector2)transform.position;
            
            var leftBottom = position - size / 2f;
            var rightTop = position + size / 2f;
            
            return leftBottom.x <= pointerPosition.x && pointerPosition.x <= rightTop.x &&
                   leftBottom.y <= pointerPosition.y && pointerPosition.y <= rightTop.y;
        }
    }
}