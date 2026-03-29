using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class CardLockdownAction : ICardAction
    {
        public CardLockdownAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Lockdown()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Lockdown>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Lockdown payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
