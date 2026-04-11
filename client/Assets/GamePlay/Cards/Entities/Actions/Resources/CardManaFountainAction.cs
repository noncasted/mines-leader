using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Rolls d5: grants 1-5 temporary mana this turn.
    /// </summary>
    public class CardManaFountainAction : ICardAction
    {
        public CardManaFountainAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.ManaFountain()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ManaFountain>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ManaFountain payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
