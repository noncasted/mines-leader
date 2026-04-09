using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace GamePlay.Cards
{
    public interface ICardRenderer
    {
        void SetSortingOrder(int order);
        void SetAllColor(Color color);
        void SetNameTextColor(Color color);
        void SetDescriptionTextColor(Color color);
    }

    [DisallowMultipleComponent]
    public class CardRenderer : MonoBehaviour, IEntityComponent, ICardRenderer
    {
        [SerializeField] private SortingGroup _sortingGroup;

        private SpriteRenderer[] _all;

        // Text components in order: [0] = card name, [1] = card description
        private TMP_Text[] _textComponents;

        public void Register(IEntityBuilder builder)
        {
            _all = GetComponentsInChildren<SpriteRenderer>();
            _textComponents = GetComponentsInChildren<TMP_Text>();

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

        public void SetNameTextColor(Color color)
        {
            if (_textComponents.Length > 0)
                _textComponents[0].color = color;
        }

        public void SetDescriptionTextColor(Color color)
        {
            if (_textComponents.Length > 1)
                _textComponents[1].color = color;
        }
    }
}