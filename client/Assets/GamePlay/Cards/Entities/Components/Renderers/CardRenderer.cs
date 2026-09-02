using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace GamePlay.Cards
{
    public interface ICardRenderer
    {
        Color SpritesColor { get; }
        Color NameTextColor { get; }
        Color DescriptionTextColor { get; }

        void SetSortingLayer(string layer);
        void SetSortingOrder(int order);
        void SetAllColor(Color color);
        void SetNameTextColor(Color color);
        void SetDescriptionTextColor(Color color);

        /// <summary>
        /// Applies colors and locks out any further generic color changes,
        /// so state-driven tinting is not overwritten by the availability view.
        /// </summary>
        void OverrideColors(Color sprites, Color name, Color description);
    }

    [DisallowMultipleComponent]
    public class CardRenderer : MonoBehaviour, IEntityComponent, ICardRenderer
    {
        [SerializeField] private SortingGroup _sortingGroup;

        private SpriteRenderer[] _all;

        // Text components in order: [0] = card name, [1] = card description
        private TMP_Text[] _textComponents;

        private bool _isColorOverridden;

        public Color SpritesColor { get; private set; } = Color.white;
        public Color NameTextColor { get; private set; } = Color.white;
        public Color DescriptionTextColor { get; private set; } = Color.white;

        public void Register(IEntityBuilder builder)
        {
            // Include inactive children: a remote card keeps its front side disabled
            // until it is revealed, and those renderers still have to be tinted.
            _all = GetComponentsInChildren<SpriteRenderer>(true);
            _textComponents = GetComponentsInChildren<TMP_Text>(true);

            if (_all.Length > 0)
                SpritesColor = _all[0].color;

            if (_textComponents.Length > 0)
                NameTextColor = _textComponents[0].color;

            if (_textComponents.Length > 1)
                DescriptionTextColor = _textComponents[1].color;

            builder.RegisterComponent(this)
                   .As<ICardRenderer>();
        }

        public void SetSortingLayer(string layer)
        {
            _sortingGroup.sortingLayerName = layer;
        }

        public void SetSortingOrder(int order)
        {
            _sortingGroup.sortingOrder = order;
        }

        public void SetAllColor(Color color)
        {
            if (_isColorOverridden == true)
                return;

            ApplyAllColor(color);
        }

        public void SetNameTextColor(Color color)
        {
            if (_isColorOverridden == true)
                return;

            ApplyNameTextColor(color);
        }

        public void SetDescriptionTextColor(Color color)
        {
            if (_isColorOverridden == true)
                return;

            ApplyDescriptionTextColor(color);
        }

        public void OverrideColors(Color sprites, Color name, Color description)
        {
            _isColorOverridden = true;

            ApplyAllColor(sprites);
            ApplyNameTextColor(name);
            ApplyDescriptionTextColor(description);
        }

        private void ApplyAllColor(Color color)
        {
            SpritesColor = color;

            foreach (var entry in _all)
                entry.color = color;
        }

        private void ApplyNameTextColor(Color color)
        {
            NameTextColor = color;

            if (_textComponents.Length > 0)
                _textComponents[0].color = color;
        }

        private void ApplyDescriptionTextColor(Color color)
        {
            DescriptionTextColor = color;

            if (_textComponents.Length > 1)
                _textComponents[1].color = color;
        }
    }
}
