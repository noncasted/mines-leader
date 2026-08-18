using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Coin flip: heads doubles mana, tails zeroes it.
    /// </summary>
    public class CardDoubleOrNothingAction : ICardAction
    {
        public CardDoubleOrNothingAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.DoubleOrNothing()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.DoubleOrNothing>
        {
            public Snapshot(IGameRandom random, IGameContext context)
            {
                _random = random;
                _context = context;
            }

            private readonly IGameRandom _random;
            private readonly IGameContext _context;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.DoubleOrNothing payload)
            {
                var isOwned = _context.Self.Id == payload.TargetPlayer;
                return _random.PlayCoinFlip(lifetime, payload.IsHeads, isOwned);
            }
        }
    }
}
