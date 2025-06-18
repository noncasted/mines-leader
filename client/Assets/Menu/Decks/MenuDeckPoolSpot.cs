using Meta;
using UnityEngine;

namespace Menu.Decks
{
    [DisallowMultipleComponent]
    public class MenuDeckPoolSpot : MonoBehaviour
    {
        [SerializeField] private MenuDeckPoolCard _card;
        [SerializeField] private RectTransform _transform;

        private ICardDefinition _cardDefinition;

        public MenuDeckPoolCard Card => _card;
        public RectTransform Transform => _transform;

        public void Setup(ICardDefinition definition)
        {
            _cardDefinition = definition;
            _card.Setup(definition, this);
        }
    }
}