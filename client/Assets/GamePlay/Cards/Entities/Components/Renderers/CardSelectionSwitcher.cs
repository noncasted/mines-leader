using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    [DisallowMultipleComponent]
    public class CardSelectionSwitcher : MonoBehaviour, IEntityComponent, IScopeSetup
    {
        [SerializeField] private GameObject _selectionHighlight;

        private ICardPointerHandler _pointerHandler;

        internal void Construct(ICardPointerHandler pointerHandler)
        {
            _pointerHandler = pointerHandler;
        }

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IScopeSetup>();

            _selectionHighlight.SetActive(false);
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _pointerHandler.IsPressed.Advise(lifetime, value => _selectionHighlight.SetActive(value));
        }
    }
}