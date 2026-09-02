using Cysharp.Threading.Tasks;
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
            public Snapshot(IGameRandom random, IGameContext context, ICardResourceFloatingText floatingText)
            {
                _random = random;
                _context = context;
                _floatingText = floatingText;
            }

            private readonly IGameRandom _random;
            private readonly IGameContext _context;
            private readonly ICardResourceFloatingText _floatingText;

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.DoubleOrNothing payload)
            {
                var isOwned = _context.Self.Id == payload.TargetPlayer;
                var position = await _random.PlayCoinFlip(lifetime, payload.IsHeads, isOwned);

                // Heads doubles the current mana, tails zeroes it: the exact delta is
                // only known on the server, so the multiplier is shown instead.
                if (payload.IsHeads == true)
                    _floatingText.Show(position, CardResource.Mana, "x2", true);
                else
                    _floatingText.Show(position, CardResource.Mana, "x0", false);
            }
        }
    }
}
