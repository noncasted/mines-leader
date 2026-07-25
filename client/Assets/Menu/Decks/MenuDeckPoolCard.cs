using Menu.Services;
using Meta;
using Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Menu.Decks
{
    [DisallowMultipleComponent]
    public class MenuDeckPoolCard : MonoBehaviour
    {
        [SerializeField] private Image _raycastImage;
        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private TMP_Text _manaCost;

        private ICardDefinition _cardDefinition;
        private MenuDeckPoolSpot _parentPoolSpot;
        private RectTransform _rectTransform;
        private IMenuMoveArea _moveArea;
        private ICardConfigs _configs;
        private ICardConfig _config;
        private ICardDescriptionProvider _descriptionProvider;

        public ICardDefinition CardDefinition => _cardDefinition;
        public ICardConfig Config => _config;
        public RectTransform Transform => _rectTransform;

        public string ResolvedDescription { get; private set; }
        
        [Inject]
        private void Construct(IMenuMoveArea moveArea, ICardConfigs configs, ICardDescriptionProvider descriptionProvider)
        {
            _configs = configs;
            _descriptionProvider = descriptionProvider;
            _moveArea = moveArea;
            _rectTransform = GetComponent<RectTransform>();
        }

        public void Setup(ICardDefinition definition, MenuDeckPoolSpot parentPoolSpot)
        {
            _cardDefinition = definition;
            _parentPoolSpot = parentPoolSpot;

            _image.sprite = definition.Image;
            _name.text = definition.Name;
            _description.text = _descriptionProvider.GetDescription(definition.Type);
            ResolvedDescription = _description.text;
            _config = _configs.Value.All[definition.Type];
            _manaCost.text = _config.ManaCost.ToString();
        }

        public void BeginDrag()
        {
            _raycastImage.raycastTarget = false;
            _rectTransform.SetParent(_moveArea.Transform, true);
        }

        public void ForceMoveToDeck(MenuDeckCard deckCard)
        {
            deckCard.OnForceMove(_parentPoolSpot);
            gameObject.SetActive(false);
        }

        public void ReturnToSpot()
        {
            _raycastImage.raycastTarget = true;

            _rectTransform.SetParent(_parentPoolSpot.Transform, true);
            _rectTransform.localPosition = Vector3.zero;
            gameObject.SetActive(true);
        }
    }
}