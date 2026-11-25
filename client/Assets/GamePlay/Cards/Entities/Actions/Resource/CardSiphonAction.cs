using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    public class CardSiphonAction : ICardAction
    {
        public CardSiphonAction(
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

            var payload = new CardUsePayload.Siphon()
            {
                Type = _type
            };

            return new CardActionResult(isDropped, payload);
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Siphon>
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

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Siphon payload)
            {
                // TargetPlayer is the drained opponent, the invoker gains the same amount.
                var isDrainedOwned = _context.Self.Id == payload.TargetPlayer;
                var drain = _configs.Value.Siphon_Normal.DrainAmount;

                _floatingText.Show(_random.GetLandingPosition(isDrainedOwned), CardResource.Mana, -drain);
                _floatingText.Show(_random.GetLandingPosition(isDrainedOwned == false), CardResource.Mana, drain);

                return UniTask.CompletedTask;
            }
        }
    }
}
