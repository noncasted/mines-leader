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
        
        private ICardContext _context;

        [Inject]
        private void Construct(ICardContext context)
        {
            _context = context;
        }
        
        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _context.IsAvailable.View(lifetime, isAvailable =>
            {
                if (isAvailable)
                    _renderer.SetAllColor(_available);
                else
                    _renderer.SetAllColor(_locked);
            });
        }
    }
}