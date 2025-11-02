using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardOpponentFlagEraseAction : ICardAction
    {
        public CardOpponentFlagEraseAction(
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
            var size = _context.Config.Size;

            var pattern = new Pattern(_context.TargetBoard, size);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult()
            {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.OpponentFlagErase()
                {
                    Position = result.Position.ToPosition()
                }
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.OpponentFlagErase>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.OpponentFlagErase payload)
            {
                return UniTask.CompletedTask;
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