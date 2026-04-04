using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class CardMedicAction : ICardAction
    {
        public CardMedicAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Medic()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Medic>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Medic payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}