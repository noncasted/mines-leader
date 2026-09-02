using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Copies opponent's last played card.
    /// </summary>
    public class CardMirrorMatchAction : ICardAction
    {
        public CardMirrorMatchAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.MirrorMatch()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.MirrorMatch>
        {
            public Snapshot(ICardActionSyncDispatcher syncDispatcher)
            {
                _syncDispatcher = syncDispatcher;
            }

            private readonly ICardActionSyncDispatcher _syncDispatcher;

            /// <summary>
            /// The backend folds the copied card's own CardUse record into this payload, so the
            /// visuals of the copied action have to be played from here.
            /// </summary>
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.MirrorMatch payload)
            {
                if (payload.CopiedAction == null)
                    return UniTask.CompletedTask;

                return _syncDispatcher.Dispatch(lifetime, payload.CopiedAction);
            }
        }
    }
}
