using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Coin flip: heads +3 cards +2 mana, tails -2 cards.
    /// </summary>
    public class CardGamblersRuinAction : ICardAction
    {
        public CardGamblersRuinAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.GamblersRuin()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.GamblersRuin>
        {
            public Snapshot(ICardRandomAnimator randomAnimator)
            {
                _randomAnimator = randomAnimator;
            }

            private readonly ICardRandomAnimator _randomAnimator;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.GamblersRuin payload)
            {
                return _randomAnimator.PlayCoinFlip(lifetime, payload.IsHeads);
            }
        }
    }
}
