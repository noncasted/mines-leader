using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Cross-shaped clearance on own board.
    /// </summary>
    public class CardExcavatorAction : ICardAction
    {
        public CardExcavatorAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Excavator()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Excavator>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Excavator payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
