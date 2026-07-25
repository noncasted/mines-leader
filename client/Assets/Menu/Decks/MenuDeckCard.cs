using GamePlay.UI;
using Internal;
using Meta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Menu.Decks
{
    [DisallowMultipleComponent]
    public class MenuDeckCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private TMP_Text _manaCost;
        [SerializeField] private CardSelectionHighlight _selectionHighlight;
        [SerializeField] private UIElementPointerHandler _pointerHandler;

        private readonly ViewableDelegate _changed = new();

        private MenuDeckPoolSpot _spot;

        public ICardDefinition CurrentDefinition => _spot.Card.CardDefinition;
        public IViewableDelegate Changed => _changed;
        public MenuDeckPoolCard CurrentCard => _spot.Card;
        public UIElementPointerHandler PointerHandler => _pointerHandler;

        public void OnCardDropped(MenuDeckPoolSpot stop)
        {
            _spot?.ReturnToSpot();
            _spot = stop;
            UpdateDisplay(CurrentDefinition);
            _selectionHighlight.OnDeselected();

            _changed.Invoke();
        }

        public void OnForceMove(MenuDeckPoolSpot stop)
        {
            _spot?.ReturnToSpot();
            _spot = stop;
            UpdateDisplay(CurrentDefinition);
            _selectionHighlight.OnDeselected();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _selectionHighlight.OnSelected();
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            _selectionHighlight.OnDeselected();
        }
        
        private void UpdateDisplay(ICardDefinition definition)
        {
            _image.sprite = definition.Image;
            _name.text = definition.Name;
            _description.text = CurrentCard.ResolvedDescription;
            _manaCost.text = CurrentCard.Config.ManaCost.ToString();
        }
    }
}