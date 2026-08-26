using GamePlay.UI;
using Internal;
using Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Menu.Progression
{
    [DisallowMultipleComponent]
    public class MenuProgressionCard : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private TMP_Text _manaCost;
        [SerializeField] private GameObject _highlight;
        [SerializeField] private UIElementPointerHandler _pointerHandler;

        private ICardDefinition _definition;
        private ICardDescriptionProvider _descriptionProvider;
        private ICardConfigs _configs;

        public ICardDefinition Definition => _definition;
        public UIElementPointerHandler PointerHandler => _pointerHandler;

        [Inject]
        internal void Construct(ICardConfigs configs, ICardDescriptionProvider descriptionProvider)
        {
            _configs = configs;
            _descriptionProvider = descriptionProvider;
        }

        public void Setup(IReadOnlyLifetime lifetime, ICardDefinition definition)
        {
            _definition = definition;
            _image.sprite = definition.Image;
            _name.text = definition.Name;
            _description.text = _descriptionProvider.GetDescription(definition.Type);
            _manaCost.text = _configs.Value.All[definition.Type].ManaCost.ToString();

            _pointerHandler.IsHovered.Advise(lifetime, isHovered => _highlight.SetActive(isHovered));
        }
    }
}