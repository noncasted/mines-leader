using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardErosionDozerAction : ICardAction
    {
        public CardErosionDozerAction(
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            CardConfigOptions.ErosionDozer config,
            ICardContext context)
        {
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _config = config;
            _context = context;
        }

        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly CardConfigOptions.ErosionDozer _config;
        private readonly ICardContext _context;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var size = _config.Size;
            var pattern = new Pattern(_context.TargetBoard, size);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult()
            {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.ErosionDozer()
                {
                    Position = result.Position.ToPosition()
                }
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ErosionDozer>
        {
            public Snapshot(IBoardCellsAnimator animator)
            {
                _animator = animator;
            }

            private readonly IBoardCellsAnimator _animator;

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ErosionDozer payload)
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

                await _animator.OpenCells(lifetime, payload.TargetPlayer, payload.UpdatedFreeCells);
            }
        }

        public class Pattern : ICardDropPattern
        {
            public Pattern(IBoard board, int size)
            {
                _board = board;
                _size = size;
            }

            private readonly IBoard _board;
            private readonly int _size;

            public IReadOnlyList<IBoardCell> GetDropData(Vector2Int pointer)
            {
                var selected = _board.GetClosedShape(pointer);
                var ordered = selected.OrderBy(t => Vector2Int.Distance(t.BoardPosition, pointer));

                var limited = ordered.Take(_size).ToList();
                return limited;
            }
        }
    }
}
