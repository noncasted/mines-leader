using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    public class CardSiphonAction : ICardAction
    {
        public CardSiphonAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.Siphon()
            };
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
