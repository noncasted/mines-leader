using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardOpponentBombAction : ICardAction
    {
        public CardOpponentBombAction(
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            ICardContext context)
        {
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _context = context;
        }

        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly ICardContext _context;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var pattern = new Pattern(_context.TargetBoard);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult()
            {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.OpponentBomb()
                {
                    Position = result.Position.ToPosition()
                }
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.OpponentBomb>
        {
            public Snapshot(IBoardCellsAnimator animator)
            {
                _animator = animator;
            }

            private readonly IBoardCellsAnimator _animator;

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.OpponentBomb payload)
            {
                // 1. Target animation
                if (payload.TargetCells.Count > 0)
                    await _animator.PlayTargetAnimation(lifetime, payload.TargetPlayer, payload.TargetCells);

                // 2. Action animation
                if (payload.OpenedCells.Count > 0)
                {
                    var positions = payload.OpenedCells.Select(o => o.Position).ToList();
                    await _animator.PlayActionAnimation(lifetime, payload.TargetPlayer, positions);
                }

                if (payload.UpdatedFreeCells.Count == 0)
                    return;

                await _animator.OpenCells(lifetime, payload.TargetPlayer, payload.UpdatedFreeCells);
            }
        }

        public class Pattern : ICardDropPattern
        {
            public Pattern(IBoard board)
            {
                _board = board;
            }

            private readonly IBoard _board;

            public IReadOnlyList<IBoardCell> GetDropData(Vector2Int pointer)
            {
                return new[] { _board.Cells[pointer] };
            }
        }
    }
}
