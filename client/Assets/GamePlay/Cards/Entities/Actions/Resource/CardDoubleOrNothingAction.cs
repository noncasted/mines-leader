using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Coin flip: heads doubles mana, tails zeroes it.
    /// </summary>
    public class CardDoubleOrNothingAction : ICardAction
    {
        public CardDoubleOrNothingAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.DoubleOrNothing()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.DoubleOrNothing>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.DoubleOrNothing payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
