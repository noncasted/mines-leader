using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Grants +3 temporary mana this turn.
    /// </summary>
    public class CardManaSurgeAction : ICardAction
    {
        public CardManaSurgeAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.ManaSurge()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ManaSurge>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ManaSurge payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
