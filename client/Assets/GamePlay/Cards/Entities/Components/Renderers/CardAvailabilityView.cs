using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Cards
{
    [DisallowMultipleComponent]
    public class CardAvailabilityView : MonoBehaviour, IEntityComponent, IScopeSetup
    {
        [SerializeField] private CardRenderer _renderer;

        // Sprite colors
        [SerializeField] private Color _availableSpriteColor;
        [SerializeField] private Color _lockedSpriteColor;

        // Text colors (CardRenderer applies them in order: 0 = name, 1 = description)
        [SerializeField] private Color _availableNameColor;
        [SerializeField] private Color _availableDescriptionColor;
        [SerializeField] private Color _lockedNameColor;
        [SerializeField] private Color _lockedDescriptionColor;
        
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
                {
                    _renderer.SetAllColor(_availableSpriteColor);
                    _renderer.SetNameTextColor(_availableNameColor);
                    _renderer.SetDescriptionTextColor(_availableDescriptionColor);
                }
                else
                {
                    _renderer.SetAllColor(_lockedSpriteColor);
                    _renderer.SetNameTextColor(_lockedNameColor);
                    _renderer.SetDescriptionTextColor(_lockedDescriptionColor);
                }
            });
        }
    }
}