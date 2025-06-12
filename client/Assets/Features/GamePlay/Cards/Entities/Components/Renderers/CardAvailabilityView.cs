using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Cards
{
    [DisallowMultipleComponent]
    public class CardAvailabilityView : MonoBehaviour, IEntityComponent, IScopeSetup
    {
        [SerializeField] private CardRenderer _renderer;

        [SerializeField] private Color _available;
        [SerializeField] private Color _locked;
        
        private ICardActionState _actionState;

        [Inject]
        private void Construct(ICardActionState actionState)
        {
            _actionState = actionState;
        }
        
        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _actionState.IsAvailable.View(lifetime, isAvailable =>
            {
                if (isAvailable)
                    _renderer.SetAllColor(_available);
                else
                    _renderer.SetAllColor(_locked);
            });
        }
    }
}