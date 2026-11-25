using System;
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
        public CardMirrorMatchAction(
            ICardDropDetector dropDetector,
            CardType type)
        {
            _dropDetector = dropDetector;
            _type = type;
        }

        private readonly ICardDropDetector _dropDetector;
        private readonly CardType _type;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var isDropped = await _dropDetector.Wait(lifetime);

            var payload = new CardUsePayload.MirrorMatch()
            {
                Type = _type
            };

            return new CardActionResult(isDropped, payload);
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
                    throw new InvalidOperationException($"Mirror Match of {payload.CopiedCard} came without the copied action");

                return _syncDispatcher.Dispatch(lifetime, payload.CopiedAction);
            }
        }
    }
}
