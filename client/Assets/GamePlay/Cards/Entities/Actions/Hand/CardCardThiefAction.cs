using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Steals 1 random card from opponent's hand.
    /// </summary>
    public class CardCardThiefAction : ICardAction
    {
        public CardCardThiefAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.CardThief()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.CardThief>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.CardThief payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
