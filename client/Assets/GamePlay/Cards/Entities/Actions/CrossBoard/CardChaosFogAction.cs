using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Random smoke on opponent's field for 3 rounds.
    /// </summary>
    public class CardChaosFogAction : ICardAction
    {
        public CardChaosFogAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.ChaosFog()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ChaosFog>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ChaosFog payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
