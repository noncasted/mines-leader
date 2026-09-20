using Internal;

namespace GamePlay.Cards
{
    // Цвета доступности живут в каталоге: у чистого класса нет инспектора, а подбирать их
    // всё равно надо художнику.
    public class CardAvailabilityView : IScopeSetup
    {
        public CardAvailabilityView(ICardRenderer renderer, ICardContext context)
        {
            _renderer = renderer;
            _context = context;
        }

        private readonly ICardRenderer _renderer;
        private readonly ICardContext _context;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _context.IsAvailable.View(lifetime, isAvailable => {
                if (isAvailable)
                {
                    _renderer.SetAllColor(Colors.Card.AvailableSprite);
                    _renderer.SetNameTextColor(Colors.Card.AvailableName);
                    _renderer.SetDescriptionTextColor(Colors.Card.AvailableDescription);
                }
                else
                {
                    _renderer.SetAllColor(Colors.Card.LockedSprite);
                    _renderer.SetNameTextColor(Colors.Card.LockedName);
                    _renderer.SetDescriptionTextColor(Colors.Card.LockedDescription);
                }
            });
        }
    }
}
