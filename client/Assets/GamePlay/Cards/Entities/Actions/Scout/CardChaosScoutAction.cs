using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Random line clearance (length 3-7) on own board.
    /// </summary>
    public class CardChaosScoutAction : ICardAction
    {
        public CardChaosScoutAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.ChaosScout()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ChaosScout>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ChaosScout payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
