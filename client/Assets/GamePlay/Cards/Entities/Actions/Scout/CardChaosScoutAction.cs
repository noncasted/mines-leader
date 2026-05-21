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
    /// Random line clearance (length 3-7) on own board.
    /// Shows max-length line preview, picks best direction (horizontal/vertical).
    /// </summary>
    public class CardChaosScoutAction : ICardAction
    {
        public CardChaosScoutAction(
            ICardContext context,
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            CardConfigOptions.ChaosScout config)
        {
            _context = context;
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _config = config;
        }

        private readonly ICardContext _context;
        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly CardConfigOptions.ChaosScout _config;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var pattern = new Pattern(_context.TargetBoard, _config.MaxLength);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult()
            {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.ChaosScout()
                {
                    Position = result.Position.ToPosition()
                }
            };
        }

        public class Pattern : ICardDropPattern
        {
            public Pattern(IBoard board, int maxLength)
            {
                _board = board;
                _horizontalShape = PatternShapes.Line(maxLength, horizontal: true);
                _verticalShape = PatternShapes.Line(maxLength, horizontal: false);
            }

            private readonly IBoard _board;
            private readonly IPattenShape _horizontalShape;
            private readonly IPattenShape _verticalShape;

            public IReadOnlyList<IBoardCell> GetDropData(Vector2Int pointer)
            {
                var horizontal = _horizontalShape.SelectTaken(_board, pointer);
                var vertical = _verticalShape.SelectTaken(_board, pointer);
                return horizontal.Count >= vertical.Count ? horizontal : vertical;
            }
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ChaosScout>
        {
            public Snapshot(IBoardCellsAnimator animator, ICardRandomAnimator randomAnimator)
            {
                _animator = animator;
                _randomAnimator = randomAnimator;
            }

            private readonly IBoardCellsAnimator _animator;
            private readonly ICardRandomAnimator _randomAnimator;

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ChaosScout payload)
            {
                // 1. Target animation
                if (payload.TargetCells.Count > 0)
                    await _animator.PlayTargetAnimation(lifetime, payload.TargetPlayer, payload.TargetCells);

                await _randomAnimator.PlayDiceRoll(lifetime, payload.ActualLength);
                
                // 2. Action animation
                if (payload.OpenedCells.Count > 0)
                {
                    var positions = payload.OpenedCells.Select(o => o.Position).ToList();
                    await _animator.PlayActionAnimation(lifetime, payload.TargetPlayer, positions);
                }

                foreach (var position in payload.FlaggedCells)
                    _animator.FlagCell(payload.TargetPlayer, position);

                await _animator.OpenCells(lifetime, payload.TargetPlayer, payload.UpdatedFreeCells);
            }
        }
    }
}
