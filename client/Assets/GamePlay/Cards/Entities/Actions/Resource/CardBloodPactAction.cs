using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Sacrifices 1 HP to gain +3 temporary mana and +2 extra turns.
    /// </summary>
    public class CardBloodPactAction : ICardAction
    {
        public CardBloodPactAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.BloodPact()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.BloodPact>
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

            private const float StepDelay = 0.25f;

            private readonly IGameRandom _random;
            private readonly IGameContext _context;
            private readonly ICardConfigs _configs;
            private readonly ICardResourceFloatingText _floatingText;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.BloodPact payload)
            {
                var isOwned = _context.Self.Id == payload.TargetPlayer;
                var position = _random.GetLandingPosition(isOwned);
                var config = _configs.Value.BloodPact_Normal;

                _floatingText.Show(position, CardResource.Health, -config.HpCost);
                _floatingText.Show(position, CardResource.Mana, config.ManaGain, StepDelay);
                _floatingText.Show(position, CardResource.Moves, config.ExtraMoves, StepDelay * 2f);

                return UniTask.CompletedTask;
            }
        }
    }
}
