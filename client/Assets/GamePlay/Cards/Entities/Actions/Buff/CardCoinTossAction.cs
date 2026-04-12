using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Flips a coin: heads +2 moves, tails -1 move.
    /// </summary>
    public class CardCoinTossAction : ICardAction
    {
        public CardCoinTossAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.CoinToss()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.CoinToss>
        {
            public Snapshot(ICardRandomAnimator randomAnimator)
            {
                _randomAnimator = randomAnimator;
            }

            private readonly ICardRandomAnimator _randomAnimator;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.CoinToss payload)
            {
                return _randomAnimator.PlayCoinFlip(lifetime, payload.IsHeads);
            }
        }
    }
}
