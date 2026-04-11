using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Next card costs 1 less mana.
    /// </summary>
    public class CardFocusAction : ICardAction
    {
        public CardFocusAction(ICardDropDetector dropDetector)
        {
            _dropDetector = dropDetector;
        }

        private readonly ICardDropDetector _dropDetector;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var isDropped = await _dropDetector.Wait(lifetime);

            return new CardActionResult()
            {
                IsSuccess = isDropped,
                Payload = new CardUsePayload.Focus()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Focus>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Focus payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
