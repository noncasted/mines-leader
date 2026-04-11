using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Hides numbers on opponent's cells for 2 rounds.
    /// </summary>
    public class CardBlackoutAction : ICardAction
    {
        public CardBlackoutAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Blackout()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Blackout>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Blackout payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
