using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class CardScavengerAction : ICardAction
    {
        public CardScavengerAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Scavenger()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Scavenger>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Scavenger payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}