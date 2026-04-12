using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Random diamond clearance (size 2-5) on own board.
    /// </summary>
    public class CardChaosDiamondAction : ICardAction
    {
        public CardChaosDiamondAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.ChaosDiamond()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ChaosDiamond>
        {
            public Snapshot(ICardRandomAnimator randomAnimator)
            {
                _randomAnimator = randomAnimator;
            }

            private readonly ICardRandomAnimator _randomAnimator;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ChaosDiamond payload)
            {
                return _randomAnimator.PlayDiceRoll(lifetime, payload.ActualSize);
            }
        }
    }
}
