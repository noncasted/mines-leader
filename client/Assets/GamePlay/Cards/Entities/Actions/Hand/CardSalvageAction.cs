using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Peek 3 cards from deck, take 1.
    /// </summary>
    public class CardSalvageAction : ICardAction
    {
        public CardSalvageAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Salvage()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Salvage>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Salvage payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
