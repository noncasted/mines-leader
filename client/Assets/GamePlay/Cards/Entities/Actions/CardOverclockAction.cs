using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class CardOverclockAction : ICardAction
    {
        public CardOverclockAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Overclock()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Overclock>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Overclock payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
