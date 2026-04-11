using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Sacrifices 1 HP to gain +3 temporary mana and +2 extra moves.
    /// </summary>
    public class CardBloodPactAction : ICardAction
    {
        public CardBloodPactAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.BloodPact()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.BloodPact>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.BloodPact payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
