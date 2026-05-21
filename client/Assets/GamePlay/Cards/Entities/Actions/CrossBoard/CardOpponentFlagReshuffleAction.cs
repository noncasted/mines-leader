using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardOpponentFlagReshuffleAction : ICardAction
    {
        public CardOpponentFlagReshuffleAction(
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            ICardContext context,
            CardConfigOptions.OpponentFlagReshuffle config)
        {
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _context = context;
            _config = config;
        }

        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly ICardContext _context;
        private readonly CardConfigOptions.OpponentFlagReshuffle _config;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);
            var size = _config.Size;

            var pattern = new Pattern(_context.TargetBoard, size);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult()
            {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.OpponentFlagReshuffle()
                {
                    Position = result.Position.ToPosition()
                }
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.OpponentFlagReshuffle>
        {
            public Snapshot(IBoardCellsAnimator animator)
            {
                _animator = animator;
            }

            private readonly IBoardCellsAnimator _animator;

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.OpponentFlagReshuffle payload)
            {
                if (payload.TargetCells.Count > 0)
                    await _animator.PlayTargetAnimation(lifetime, payload.TargetPlayer, payload.TargetCells);

                foreach (var position in payload.UnflaggedCells)
                    _animator.UnflagCell(payload.TargetPlayer, position);

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
                return _shape.SelectTaken(_board, pointer);
            }
        }
    }
}
