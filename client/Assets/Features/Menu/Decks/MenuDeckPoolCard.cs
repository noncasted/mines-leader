using Assets.Meta;
using Global.GameServices;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace Menu
{
    [DisallowMultipleComponent]
    public class MenuDeckPoolCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
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

        public ICardDefinition CardDefinition => _cardDefinition;

        [Inject]
        private void Construct(IMenuMoveArea moveArea)
        {
            _moveArea = moveArea;
            _rectTransform = GetComponent<RectTransform>();
            
        }

        public void Setup(ICardDefinition definition, MenuDeckPoolSpot parentPoolSpot)
        {
            _cardDefinition = definition;
            _parentPoolSpot = parentPoolSpot;

            _image.sprite = definition.Image;
            _name.text = definition.Name;
            _description.text = definition.Description;
            _manaCost.text = definition.ManaCost.ToString();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _raycastImage.raycastTarget = false;
            _rectTransform.SetParent(_moveArea.Transform, true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _moveArea.Transform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPointerPosition))
            {
                _rectTransform.localPosition = localPointerPosition;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.pointerEnter != null)
            {
                var dropTarget = eventData.pointerEnter.GetComponentInParent<MenuDeckCard>();

                if (dropTarget != null)
                {
                    dropTarget.OnCardDropped(this);
                    gameObject.SetActive(false);
                    return;
                }
            }

            ReturnToSpot();
        }

        public void ForceMoveToDeck(MenuDeckCard deckCard)
        {
            deckCard.OnForceMove(this);
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