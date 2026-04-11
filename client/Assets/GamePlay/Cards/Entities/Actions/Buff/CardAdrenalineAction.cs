using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Grants +1 extra move this turn.
    /// </summary>
    public class CardAdrenalineAction : ICardAction
    {
        public CardAdrenalineAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Adrenaline()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Adrenaline>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Adrenaline payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
