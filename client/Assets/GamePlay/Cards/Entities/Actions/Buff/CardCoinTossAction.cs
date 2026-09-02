using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Flips a coin: heads +2 turns, tails -1 move.
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
            public Snapshot(
                IGameRandom random,
                IGameContext context,
                ICardConfigs configs,
                ICardResourceFloatingText floatingText)
            {
                _random = random;
                _context = context;
                _configs = configs;
                _floatingText = floatingText;
            }

            private readonly IGameRandom _random;
            private readonly IGameContext _context;
            private readonly ICardConfigs _configs;
            private readonly ICardResourceFloatingText _floatingText;

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.CoinToss payload)
            {
                var isOwned = _context.Self.Id == payload.TargetPlayer;
                var position = await _random.PlayCoinFlip(lifetime, payload.IsHeads, isOwned);
                var config = _configs.Value.CoinToss_Normal;
                var moves = payload.IsHeads == true ? config.WinMoves : -config.LoseMoves;

                _floatingText.Show(position, CardResource.Moves, moves);
            }
        }
    }
}