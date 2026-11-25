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
        public CardSoulLinkAction(
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

            var payload = new CardUsePayload.SoulLink()
            {
                Type = _type
            };

            return new CardActionResult(isDropped, payload);
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
