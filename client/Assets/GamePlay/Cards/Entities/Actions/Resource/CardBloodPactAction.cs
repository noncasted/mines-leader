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
        public CardBloodPactAction(
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

            var payload = new CardUsePayload.BloodPact()
            {
                Type = _type
            };

            return new CardActionResult(isDropped, payload);
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

            private readonly IGameRandom _random;
            private readonly IGameContext _context;
            private readonly ICardConfigs _configs;
            private readonly ICardResourceFloatingText _floatingText;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.BloodPact payload)
            {
                var isOwned = _context.Self.Id == payload.TargetPlayer;
                var position = _random.GetLandingPosition(isOwned);
                var config = _configs.Value.BloodPact_Normal;

                var step = _floatingText.StepDelay;

                _floatingText.Show(position, CardResource.Health, -config.HpCost);
                _floatingText.Show(position, CardResource.Mana, config.ManaGain, step);
                _floatingText.Show(position, CardResource.Moves, config.ExtraMoves, step * 2f);

                return UniTask.CompletedTask;
            }
        }
    }
}
