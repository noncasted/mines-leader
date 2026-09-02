using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Coin flip: heads +2 cards, tails -2 cards.
    /// </summary>
    public class CardMysticDrawAction : ICardAction
    {
        public CardMysticDrawAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.MysticDraw()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.MysticDraw>
        {
            public Snapshot(IGameRandom random, IGameContext context)
            {
                _random = random;
                _context = context;
            }

            private readonly IGameRandom _random;
            private readonly IGameContext _context;

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.MysticDraw payload)
            {
                var isOwned = _context.Self.Id == payload.TargetPlayer;
                await _random.PlayCoinFlip(lifetime, payload.IsHeads, isOwned);
            }
        }
    }
}
