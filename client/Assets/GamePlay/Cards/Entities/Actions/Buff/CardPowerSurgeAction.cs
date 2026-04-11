using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// All cards cost 1 less mana this turn.
    /// </summary>
    public class CardPowerSurgeAction : ICardAction
    {
        public CardPowerSurgeAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.PowerSurge()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.PowerSurge>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.PowerSurge payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
