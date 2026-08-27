using System;
using GamePlay.UI;
using Internal;
using Meta;
using Shared;
using TMPro;
using Tools;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Unlocks
{
    [DisallowMultipleComponent]
    public class MenuUnlockOption : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _outline;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private UIElementPointerHandler _pointerHandler;

        private bool _isLocked;
        
        public CardType Card { get; private set; }

        public void Setup(ICardDefinition definition, string description)
        {
            Card = definition.Type;

            _icon.sprite = definition.Image;
            _name.text = definition.Name;
            _description.text = description;

            if (_outline != null)
                _outline.color = ToGroupColor(definition.Group);
        }

        public void ListenClick(IReadOnlyLifetime lifetime, Action<MenuUnlockOption> callback)
        {
            _pointerHandler.Clicked.Advise(lifetime, () => {
                if (_isLocked == true)
                    return;

                callback.Invoke(this);
            });
        }

        public void SetInteractable(bool value)
        {
            _isLocked = value;
        }

        private static Color ToGroupColor(CardGroup group)
        {
            return group switch
            {
                CardGroup.Scout => Colors.Deck.Scout,
                CardGroup.Defense => Colors.Deck.Defense,
                CardGroup.Attack => Colors.Deck.Attack,
                CardGroup.Buff => Colors.Deck.Buff,
                CardGroup.Debuff => Colors.Deck.Debuff,
                _ => Color.white
            };
        }
    }
}
