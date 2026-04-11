using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Freezes cells on opponent's field for 1 round.
    /// </summary>
    public class CardFrostAction : ICardAction
    {
        public CardFrostAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Frost()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Frost>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Frost payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
