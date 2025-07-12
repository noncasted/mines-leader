using Cysharp.Threading.Tasks;
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
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.TrebuchetAimer payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}