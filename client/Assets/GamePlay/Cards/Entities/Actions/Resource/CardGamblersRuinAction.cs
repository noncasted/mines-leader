using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Coin flip: heads +3 cards +2 mana, tails -2 cards.
    /// </summary>
    public class CardGamblersRuinAction : ICardAction
    {
        public CardGamblersRuinAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.GamblersRuin()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.GamblersRuin>
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

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.GamblersRuin payload)
            {
                var isOwned = _context.Self.Id == payload.TargetPlayer;
                var position = await _random.PlayCoinFlip(lifetime, payload.IsHeads, isOwned);

                // Tails only discards cards, so there is no resource change to show.
                if (payload.IsHeads == true)
                    _floatingText.Show(position, CardResource.Mana, _configs.Value.GamblersRuin_Normal.WinMana);
            }
        }
    }
}
