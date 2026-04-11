using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Swaps a diamond area between both fields.
    /// </summary>
    public class CardDimensionRiftAction : ICardAction
    {
        public CardDimensionRiftAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.DimensionRift()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.DimensionRift>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.DimensionRift payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
