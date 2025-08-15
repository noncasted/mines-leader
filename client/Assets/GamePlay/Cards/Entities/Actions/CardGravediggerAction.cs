using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class CardGravediggerAction : ICardAction, ICardActionSync<CardActionSnapshot.Gravedigger>
    {
        public CardGravediggerAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Gravedigger()
            };
        }

        public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Gravedigger payload)
        {
            return UniTask.CompletedTask;
        }
    }
}