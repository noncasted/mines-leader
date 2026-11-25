using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class CardHandScrambleAction : ICardAction
    {
        public CardHandScrambleAction(
            ICardDropDetector dropDetector,
            CardType type)
        {
            _dropDetector = dropDetector;
            _type = type;
        }

        private readonly ICardDropDetector _dropDetector;
        private readonly CardType _type;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var isDropped = await _dropDetector.Wait(lifetime);

            var payload = new CardUsePayload.HandScramble()
            {
                Type = _type
            };

            return new CardActionResult(isDropped, payload);
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.HandScramble>
        {
            public Snapshot(IBoardCellsAnimator animator)
            {
                _animator = animator;
            }

            private readonly IBoardCellsAnimator _animator;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.HandScramble payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
