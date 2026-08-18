using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Flips a coin: heads +2 moves, tails -1 move.
    /// </summary>
    public class CardCoinTossAction : ICardAction
    {
        public CardCoinTossAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.CoinToss()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.CoinToss>
        {
            public Snapshot(IGameRandom random, IGameContext context)
            {
                _random = random;
                _context = context;
            }

            private readonly IGameRandom _random;
            private readonly IGameContext _context;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.CoinToss payload)
            {
                var isOwned = _context.Self.Id == payload.TargetPlayer;
                return _random.PlayCoinFlip(lifetime, payload.IsHeads, isOwned);
            }
        }
    }
}