using Internal;
using Meta;
using TMPro;
using UnityEngine;

namespace GamePlay.Cards
{
    [DisallowMultipleComponent]
    public class CardDataView : MonoBehaviour, IEntityComponent, IScopeSetup
    {
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private TMP_Text _manaCost;
        [SerializeField] private SpriteRenderer _image;

        private ICardDefinition _definition;
        private ICardConfigs _configs;
        private ICardDescriptionProvider _descriptionProvider;

        internal void Construct(ICardDefinition definition, ICardConfigs configs, ICardDescriptionProvider descriptionProvider)
        {
            _configs = configs;
            _definition = definition;
            _descriptionProvider = descriptionProvider;
        }

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _name.text = _definition.Name;
            _description.text = _descriptionProvider.GetDescription(_definition.Type);
            _manaCost.text = _configs.Value.All[_definition.Type].ManaCost.ToString();
            _image.sprite = _definition.Image;
        }
    }
}