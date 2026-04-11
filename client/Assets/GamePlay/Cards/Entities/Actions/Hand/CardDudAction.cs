using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Useless card. Cannot be played.
    /// </summary>
    public class CardDudAction : ICardAction
    {
        public CardDudAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Dud()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Dud>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Dud payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
