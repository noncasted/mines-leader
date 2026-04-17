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
            public Snapshot(ICardRandomAnimator randomAnimator, IGameContext gameContext)
            {
                _randomAnimator = randomAnimator;
                _gameContext = gameContext;
            }

            private readonly ICardRandomAnimator _randomAnimator;
            private readonly IGameContext _gameContext;

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.FortuneCookie payload)
            {
                await _randomAnimator.PlayDiceRoll(lifetime, payload.RevealedMines.Count);

                if (payload.AffectedCells == null || payload.AffectedCells.Length == 0)
                    return;

                var board = _gameContext.GetPlayer(payload.TargetPlayer).Board;

                foreach (var position in payload.AffectedCells)
                {
                    if (board.Cells.TryGetValue(position.ToVector(), out var cell) && cell is CellView cellView)
                        cellView.Effects.AddEffect(payload.EffectId, CellEffectType.MineHighlight);
                }
            }
        }
    }
}
