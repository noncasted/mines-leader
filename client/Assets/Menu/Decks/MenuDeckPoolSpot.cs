using Meta;
using UnityEngine;

namespace Menu.Decks
{
    [DisallowMultipleComponent]
    public class MenuDeckPoolSpot : MonoBehaviour
    {
        [SerializeField] private MenuDeckPoolCard _card;
        [SerializeField] private RectTransform _transform;
        [SerializeField] private CanvasGroup _canvasGroup;

        private ICardDefinition _cardDefinition;
        private bool _isOwned = true;

        public MenuDeckPoolCard Card => _card;
        public RectTransform Transform => _transform;
        public bool IsOwned => _isOwned;

        public void Setup(ICardDefinition definition)
        {
            _cardDefinition = definition;
            _card.Setup(definition, this);
        }

        public void SetOwned(bool owned)
        {
            _isOwned = owned;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = owned ? 1f : 0.35f;
                _canvasGroup.interactable = owned;
                _canvasGroup.blocksRaycasts = owned;
            }
        }
    }
}