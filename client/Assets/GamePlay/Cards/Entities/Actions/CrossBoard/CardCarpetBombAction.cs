using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Plants mines in a line on opponent's field.
    /// </summary>
    public class CardCarpetBombAction : ICardAction
    {
        public CardCarpetBombAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.CarpetBomb()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.CarpetBomb>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.CarpetBomb payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
