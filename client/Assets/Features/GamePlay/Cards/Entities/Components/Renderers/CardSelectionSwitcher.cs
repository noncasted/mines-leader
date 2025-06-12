using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Cards
{
    [DisallowMultipleComponent]
    public class CardSelectionSwitcher : MonoBehaviour, IEntityComponent, IScopeSetup
    {
        [SerializeField] private GameObject _selectionHighlight;

        private ICardPointerHandler _pointerHandler;

        [Inject]
        private void Construct(ICardPointerHandler pointerHandler)
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