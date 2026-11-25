using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Rolls d5: grants 1-5 temporary mana this turn.
    /// </summary>
    public class CardManaFountainAction : ICardAction
    {
        public CardManaFountainAction(
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

            var payload = new CardUsePayload.ManaFountain()
            {
                Type = _type
            };

            return new CardActionResult(isDropped, payload);
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ManaFountain>
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

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ManaFountain payload)
            {
                var isOwned = _context.Self.Id == payload.TargetPlayer;
                var position = await _random.PlayDiceRoll(lifetime, payload.RolledAmount, isOwned);

                _floatingText.Show(position, CardResource.Mana, payload.RolledAmount);
            }
        }
    }
}
