using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Random diamond mines (size 1-4) on opponent's field.
    /// </summary>
    public class CardFortuneBlastAction : ICardAction
    {
        public CardFortuneBlastAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.FortuneBlast()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.FortuneBlast>
        {
            public Snapshot(ICardRandomAnimator randomAnimator)
            {
                _randomAnimator = randomAnimator;
            }

            private readonly ICardRandomAnimator _randomAnimator;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.FortuneBlast payload)
            {
                return _randomAnimator.PlayDiceRoll(lifetime, payload.ActualSize);
            }
        }
    }
}
