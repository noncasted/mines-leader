using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class CardPurgeAction : ICardAction
    {
        public CardPurgeAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Purge()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Purge>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Purge payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}