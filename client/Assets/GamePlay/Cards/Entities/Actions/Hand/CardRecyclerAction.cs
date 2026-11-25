using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Discard 1 card, draw 2.
    /// </summary>
    public class CardRecyclerAction : ICardAction
    {
        public CardRecyclerAction(
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

            var payload = new CardUsePayload.Recycler()
            {
                Type = _type
            };

            return new CardActionResult(isDropped, payload);
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Recycler>
        {
            public Snapshot(IBoardCellsAnimator animator)
            {
                _animator = animator;
            }

            private readonly IBoardCellsAnimator _animator;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Recycler payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
