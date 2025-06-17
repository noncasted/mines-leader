using Assets.Meta;
using Global.GameServices;
using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Menu
{
    [DisallowMultipleComponent]
    public class MenuDeckCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private TMP_Text _manaCost;
        [SerializeField] private CardSelectionHighlight _selectionHighlight;

        private readonly ViewableDelegate _changed = new();
        
        private MenuDeckPoolCard _currentCard;

        public ICardDefinition CurrentDefinition => _currentCard.CardDefinition;
        public IViewableDelegate Changed => _changed;

        private void UpdateDisplay(ICardDefinition definition)
        {
            _image.sprite = definition.Image;
            _name.text = definition.Name;
            _description.text = definition.Description;
            _manaCost.text = definition.ManaCost.ToString();
        }

        public void OnCardDropped(MenuDeckPoolCard droppedCard)
        {
            _currentCard?.ReturnToSpot();
            _currentCard = droppedCard;
            UpdateDisplay(_currentCard.CardDefinition);
            _selectionHighlight.OnDeselected();
            
            _changed.Invoke();
        }
        
        public void OnForceMove(MenuDeckPoolCard droppedCard)
        {
            _currentCard?.ReturnToSpot();
            _currentCard = droppedCard;
            UpdateDisplay(_currentCard.CardDefinition);
            _selectionHighlight.OnDeselected();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.pointerDrag != null && eventData.pointerDrag.GetComponent<MenuDeckPoolCard>() != null)
            {
                _selectionHighlight.OnSelected();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _selectionHighlight.OnDeselected();
        }
    }
}