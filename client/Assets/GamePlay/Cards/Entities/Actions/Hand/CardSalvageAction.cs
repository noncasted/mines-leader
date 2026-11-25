using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Peek 3 cards from deck, take 1.
    /// </summary>
    public class CardSalvageAction : ICardAction
    {
        public CardSalvageAction(
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

            var payload = new CardUsePayload.Salvage()
            {
                Type = _type
            };

            return new CardActionResult(isDropped, payload);
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Salvage>
        {
            public Snapshot(IBoardCellsAnimator animator)
            {
                _animator = animator;
            }

            private readonly IBoardCellsAnimator _animator;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Salvage payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
