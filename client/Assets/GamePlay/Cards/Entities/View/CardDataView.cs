using Internal;
using Meta;
using TMPro;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardDataView : IScopeSetup
    {
        public CardDataView(
            GameCardBindings bindings,
            ICardDefinition definition,
            ICardConfigs configs,
            ICardDescriptionProvider descriptionProvider)
        {
            var body = bindings.View.Body;

            _name = body.Name.TextMeshPro;
            _description = body.Description.TextMeshPro;
            _manaCost = body.ManaCost.TextMeshPro;
            _image = body.Image.SpriteRenderer;

            _definition = definition;
            _configs = configs;
            _descriptionProvider = descriptionProvider;
        }

        private readonly TMP_Text _name;
        private readonly TMP_Text _description;
        private readonly TMP_Text _manaCost;
        private readonly SpriteRenderer _image;

        private readonly ICardDefinition _definition;
        private readonly ICardConfigs _configs;
        private readonly ICardDescriptionProvider _descriptionProvider;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _name.text = _definition.Name;
            _description.text = _descriptionProvider.GetDescription(_definition.Type);
            _manaCost.text = _configs.Value.All[_definition.Type].ManaCost.ToString();
            _image.sprite = _definition.Image;
        }
    }
}
