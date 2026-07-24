using Meta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace Menu.Screens
{
    [DisallowMultipleComponent]
    public class MenuProgressionCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private TMP_Text _manaCost;
        [SerializeField] private GameObject _highlight;
        [SerializeField] private Button _button;

        private ICardDefinition _definition;
        private ICardDescriptionProvider _descriptionProvider;
        private ICardConfigs _configs;

        public ICardDefinition Definition => _definition;
        public Button Button => _button;

        [Inject]
        private void Construct(ICardConfigs configs, ICardDescriptionProvider descriptionProvider)
        {
            _configs = configs;
            _descriptionProvider = descriptionProvider;
        }

        public void Setup(ICardDefinition definition)
        {
            _definition = definition;
            _image.sprite = definition.Image;
            _name.text = definition.Name;
            _description.text = _descriptionProvider.GetDescription(definition.Type);
            _manaCost.text = _configs.Value.All[definition.Type].ManaCost.ToString();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _highlight.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _highlight.SetActive(false);
        }
    }
}