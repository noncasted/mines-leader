using GamePlay.UI;
using Meta;
using UnityEngine;

namespace Menu.Decks
{
    [DisallowMultipleComponent]
    public class MenuDeckPoolSpot : MonoBehaviour
    {
        [SerializeField] private GameObject _block;
        [SerializeField] private MenuDeckPoolCard _card;
        [SerializeField] private RectTransform _transform;
        [SerializeField] private UIElementPointerHandler _pointerHandler;

        private bool _isOwned = true;

        public MenuDeckPoolCard Card => _card;
        public RectTransform Transform => _transform;
        public bool IsOwned => _isOwned;
        public UIElementPointerHandler PointerHandler => _pointerHandler;

        public void Setup(ICardDefinition definition)
        {
            _card.Setup(definition, this);
        }

        public void SetOwned(bool owned)
        {
            _isOwned = owned;
            _block.SetActive(!owned);
        }

        public void ForceMoveToDeck(MenuDeckCard deckCard)
        {
            _card.ForceMoveToDeck(deckCard);
        }

        public void ReturnToSpot()
        {
            _card.ReturnToSpot();
            _block.transform.SetAsLastSibling();
            _pointerHandler.transform.SetAsLastSibling();
        }
    }
}