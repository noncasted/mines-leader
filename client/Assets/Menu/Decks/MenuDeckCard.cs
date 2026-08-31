using System;
using GamePlay.UI;
using Internal;
using Meta;
using Shared;
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
        [SerializeField] private Image _outline;
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
            UpdateOutline(false);

            _changed.Invoke();
        }

        public void OnForceMove(MenuDeckPoolSpot stop)
        {
            _spot?.ReturnToSpot();
            _spot = stop;
            UpdateDisplay(CurrentDefinition);
            UpdateOutline(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            UpdateOutline(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UpdateOutline(false);
        }

        private void UpdateDisplay(ICardDefinition definition)
        {
            _image.sprite = definition.Image;
            _name.text = definition.Name;
            _description.text = CurrentCard.ResolvedDescription;
            _manaCost.text = CurrentCard.Config.ManaCost.ToString();
        }

        private void UpdateOutline(bool isSelected)
        {
            if (isSelected == true)
            {
                _outline.color = Color.white;
                return;
            }

            _outline.color = CurrentDefinition.Group switch
            {
                CardGroup.Scout => Colors.Deck.Scout,
                CardGroup.Defense => Colors.Deck.Defense,
                CardGroup.Attack => Colors.Deck.Attack,
                CardGroup.Buff => Colors.Deck.Buff,
                CardGroup.Debuff => Colors.Deck.Debuff,
                _ => Color.black
            };
        }
    }
}