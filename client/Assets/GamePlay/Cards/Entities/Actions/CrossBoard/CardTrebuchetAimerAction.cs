using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Players;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class CardTrebuchetAimerAction : ICardAction
    {
        public CardTrebuchetAimerAction(
            ICardDropDetector dropDetector,
            IPlayerModifiers modifiers)
        {
            _dropDetector = dropDetector;
            _modifiers = modifiers;
        }

        private readonly ICardDropDetector _dropDetector;
        private readonly IPlayerModifiers _modifiers;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var isDropped = await _dropDetector.Wait(lifetime);

            return new CardActionResult()
            {
                IsSuccess = isDropped,
                Payload = new CardUsePayload.TrebuchetAimer()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.TrebuchetAimer>
        {
            public Snapshot(IBoardCellsAnimator animator)
            {
                _animator = animator;
            }

            private readonly IBoardCellsAnimator _animator;

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.TrebuchetAimer payload)
            {
            }
        }
    }
}
