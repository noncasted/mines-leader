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

    public class CardRenderer : ICardRenderer
    {
        public CardRenderer(GameCardBindings bindings)
        {
            var view = bindings.View;
            var body = view.Body;

            _sortingGroup = bindings.SortingGroup;

            // Рубашка и выделение красятся вместе с лицом: у удалённой карты лицо выключено
            // до вскрытия, а тонировать надо всё равно.
            _all = new[]
            {
                body.SpriteRenderer,
                body.Image.SpriteRenderer,
                body.SelectionHighlight.SpriteRenderer,
                view.Back.SpriteRenderer
            };

            _name = body.Name.TextMeshPro;
            _description = body.Description.TextMeshPro;

            SpritesColor = _all[0].color;
            NameTextColor = _name.color;
            DescriptionTextColor = _description.color;
        }

        private readonly SortingGroup _sortingGroup;
        private readonly SpriteRenderer[] _all;
        private readonly TMP_Text _name;
        private readonly TMP_Text _description;

        private bool _isColorOverridden;

        public Color SpritesColor { get; private set; }
        public Color NameTextColor { get; private set; }
        public Color DescriptionTextColor { get; private set; }

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
            _name.color = color;
        }

        private void ApplyDescriptionTextColor(Color color)
        {
            DescriptionTextColor = color;
            _description.color = color;
        }
    }
}
