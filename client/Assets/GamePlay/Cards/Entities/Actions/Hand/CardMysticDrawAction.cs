using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Coin flip: heads +2 cards, tails -2 cards.
    /// </summary>
    public class CardMysticDrawAction : ICardAction
    {
        public CardMysticDrawAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.MysticDraw()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.MysticDraw>
        {
            public Snapshot(ICardRandomAnimator randomAnimator)
            {
                _randomAnimator = randomAnimator;
            }

            private readonly ICardRandomAnimator _randomAnimator;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.MysticDraw payload)
            {
                return _randomAnimator.PlayCoinFlip(lifetime, payload.IsHeads);
            }
        }
    }
}
