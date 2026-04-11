using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Absorbs the next mine hit.
    /// </summary>
    public class CardShieldAction : ICardAction
    {
        public CardShieldAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Shield()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Shield>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Shield payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
