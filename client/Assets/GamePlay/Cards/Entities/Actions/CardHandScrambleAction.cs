using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class CardHandScrambleAction : ICardAction
    {
        public CardHandScrambleAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.HandScramble()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.HandScramble>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.HandScramble payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
