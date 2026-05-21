using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Rolls d5: grants 1-5 temporary mana this turn.
    /// </summary>
    public class CardManaFountainAction : ICardAction
    {
        public CardManaFountainAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.ManaFountain()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ManaFountain>
        {
            public Snapshot(IBoardCellsAnimator animator, ICardRandomAnimator randomAnimator)
            {
                _animator = animator;
                _randomAnimator = randomAnimator;
            }

            private readonly IBoardCellsAnimator _animator;
            private readonly ICardRandomAnimator _randomAnimator;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ManaFountain payload)
            {
                return _randomAnimator.PlayDiceRoll(lifetime, payload.RolledAmount);
            }
        }
    }
}
