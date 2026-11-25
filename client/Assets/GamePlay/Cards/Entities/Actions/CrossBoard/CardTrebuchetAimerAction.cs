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
            IPlayerModifiers modifiers,
            CardType type)
        {
            _dropDetector = dropDetector;
            _modifiers = modifiers;
            _type = type;
        }

        private readonly ICardDropDetector _dropDetector;
        private readonly IPlayerModifiers _modifiers;
        private readonly CardType _type;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var isDropped = await _dropDetector.Wait(lifetime);

            var payload = new CardUsePayload.TrebuchetAimer()
            {
                Type = _type
            };

            return new CardActionResult(isDropped, payload);
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
