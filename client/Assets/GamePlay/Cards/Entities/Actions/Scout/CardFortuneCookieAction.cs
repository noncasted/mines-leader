using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Reveals 1-3 random mines as temporary highlights.
    /// </summary>
    public class CardFortuneCookieAction : ICardAction
    {
        public CardFortuneCookieAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.FortuneCookie()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.FortuneCookie>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.FortuneCookie payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
