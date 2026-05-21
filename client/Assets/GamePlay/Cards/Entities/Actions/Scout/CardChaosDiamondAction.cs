using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    /// <summary>
    /// Random diamond clearance (size 2-5) on own board.
    /// </summary>
    public class CardChaosDiamondAction : ICardAction
    {
        public CardChaosDiamondAction(
            ICardContext context,
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            CardConfigOptions.ChaosDiamond config)
        {
            _context = context;
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _config = config;
        }

        private readonly ICardContext _context;
        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly CardConfigOptions.ChaosDiamond _config;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var pattern = new Pattern(_context.TargetBoard, _config.MaxSize);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult()
            {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.ChaosDiamond()
                {
                    Position = result.Position.ToPosition()
                }
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ChaosDiamond>
        {
            public Snapshot(IBoardCellsAnimator animator, ICardRandomAnimator randomAnimator)
            {
                _animator = animator;
                _randomAnimator = randomAnimator;
            }

            private readonly IBoardCellsAnimator _animator;
            private readonly ICardRandomAnimator _randomAnimator;

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ChaosDiamond payload)
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

                await _randomAnimator.PlayDiceRoll(lifetime, payload.ActualSize);

                foreach (var position in payload.FlaggedCells)
                    _animator.FlagCell(payload.TargetPlayer, position);

                await _animator.OpenCells(lifetime, payload.TargetPlayer, payload.UpdatedFreeCells);
            }
        }

        public class Pattern : ICardDropPattern
        {
            public Pattern(IBoard board, int size)
            {
                _board = board;
                _shape = PatternShapes.Rhombus(size);
            }

            private readonly IBoard _board;
            private readonly IPattenShape _shape;

            public IReadOnlyList<IBoardCell> GetDropData(Vector2Int pointer)
            {
                return _shape.All(_board, pointer);
            }
        }
    }
}
