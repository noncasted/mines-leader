using Internal;
using UnityEngine;
using UnityEngine.Rendering;

namespace GamePlay.Cards
{
    public interface ICardRenderer
    {
        void SetSortingOrder(int order);
        void SetAllColor(Color color);
    }
    
    [DisallowMultipleComponent]
    public class CardRenderer : MonoBehaviour, IEntityComponent, ICardRenderer
    {
        [SerializeField] private SortingGroup _sortingGroup;

        private SpriteRenderer[] _all;
        
        public void Register(IEntityBuilder builder)
        {
            _all = GetComponentsInChildren<SpriteRenderer>();
            
            builder.RegisterComponent(this)
                .As<ICardRenderer>();
        }

        public void SetSortingOrder(int order)
        {
            _sortingGroup.sortingOrder = order;
        }

        public void SetAllColor(Color color)
        {
            foreach (var entry in _all)
                entry.color = color;
        }
    }
}