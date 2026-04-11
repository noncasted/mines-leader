using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Injects a Dud card into opponent's deck.
    /// </summary>
    public class CardSabotageDeckAction : ICardAction
    {
        public CardSabotageDeckAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.SabotageDeck()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.SabotageDeck>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.SabotageDeck payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
