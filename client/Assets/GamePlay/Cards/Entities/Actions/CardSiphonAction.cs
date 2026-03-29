using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class CardSiphonAction : ICardAction
    {
        public CardSiphonAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Siphon()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Siphon>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Siphon payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
