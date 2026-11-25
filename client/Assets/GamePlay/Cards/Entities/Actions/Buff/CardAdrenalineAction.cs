using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Grants +1 extra move this turn.
    /// </summary>
    public class CardAdrenalineAction : ICardAction
    {
        public CardAdrenalineAction(
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

            var payload = new CardUsePayload.Adrenaline()
            {
                Type = _type
            };

            return new CardActionResult(isDropped, payload);
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Adrenaline>
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

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Adrenaline payload)
            {
                var isOwned = _context.Self.Id == payload.TargetPlayer;
                var position = _random.GetLandingPosition(isOwned);

                _floatingText.Show(position, CardResource.Moves, _configs.Value.Adrenaline_Normal.ExtraMoves);

                return UniTask.CompletedTask;
            }
        }
    }
}
