using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Plants mines in a cross on opponent's field.
    /// </summary>
    public class CardMineClusterAction : ICardAction
    {
        public CardMineClusterAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.MineCluster()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.MineCluster>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.MineCluster payload)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
