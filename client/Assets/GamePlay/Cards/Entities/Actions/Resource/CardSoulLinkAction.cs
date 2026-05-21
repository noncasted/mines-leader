using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Links damage: mine hits also damage opponent for 2 rounds.
    /// </summary>
    public class CardSoulLinkAction : ICardAction
    {
        public CardSoulLinkAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.SoulLink()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.SoulLink>
        {
            public Snapshot(IBoardCellsAnimator animator)
            {
                _animator = animator;
            }

            private readonly IBoardCellsAnimator _animator;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.SoulLink payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
