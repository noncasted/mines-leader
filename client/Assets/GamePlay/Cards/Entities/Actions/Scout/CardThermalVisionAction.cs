using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Highlights mines in a diamond without flagging.
    /// </summary>
    public class CardThermalVisionAction : ICardAction
    {
        public CardThermalVisionAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.ThermalVision()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ThermalVision>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ThermalVision payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
