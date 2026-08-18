using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    /// <summary>
    /// Reveals 1-3 random mines as temporary highlights.
    /// </summary>
    public class CardFortuneCookieAction : ICardAction
    {
        public CardFortuneCookieAction(ICardDropDetector dropDetector)
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
                Payload = new CardUsePayload.FortuneCookie()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.FortuneCookie>
        {
            public Snapshot(IBoardCellsAnimator animator, IGameRandom random, IGameContext context)
            {
                _animator = animator;
                _random = random;
                _context = context;
            }

            private readonly IGameRandom _random;
            private readonly IGameContext _context;
            private readonly IBoardCellsAnimator _animator;

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.FortuneCookie payload)
            {
                if (payload.TargetCells.Count > 0)
                    await _animator.PlayTargetAnimation(lifetime, payload.TargetPlayer, payload.TargetCells);

                var isOwned = _context.Self.Id == payload.TargetPlayer;
                await _random.PlayDiceRoll(lifetime, payload.RevealedMines.Count, isOwned);

                if (payload.AffectedCells == null || payload.AffectedCells.Length == 0)
                    return;

                foreach (var position in payload.AffectedCells)
                    _animator.AddEffect(payload.EffectId, CellEffectType.MineHighlight, payload.TargetPlayer, position);
            }
        }
    }
}