using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Opponent's cards cost 1 more mana next turn.
    /// </summary>
    public class CardEmbargoAction : ICardAction
    {
        public CardEmbargoAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Embargo()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Embargo>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Embargo payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
