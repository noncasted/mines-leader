using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Discard 1 card, draw 2.
    /// </summary>
    public class CardRecyclerAction : ICardAction
    {
        public CardRecyclerAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Recycler()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Recycler>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Recycler payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
