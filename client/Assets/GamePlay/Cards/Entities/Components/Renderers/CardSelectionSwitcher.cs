using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardSelectionSwitcher : IScopeSetup
    {
        public CardSelectionSwitcher(GameCardBindings bindings, ICardPointerHandler pointerHandler)
        {
            _selectionHighlight = bindings.View.Body.SelectionHighlight.GameObject;
            _pointerHandler = pointerHandler;

            _selectionHighlight.SetActive(false);
        }

        private readonly GameObject _selectionHighlight;
        private readonly ICardPointerHandler _pointerHandler;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _pointerHandler.IsPressed.Advise(lifetime, value => _selectionHighlight.SetActive(value));
        }
    }
}
