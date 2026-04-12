using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    /// <summary>
    /// Hides numbers on opponent's cells for 2 rounds.
    /// </summary>
    public class CardBlackoutAction : ICardAction
    {
        public CardBlackoutAction(
            ICardContext context,
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            CardConfigOptions.Blackout config)
        {
            _context = context;
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _config = config;
        }

        private readonly ICardContext _context;
        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly CardConfigOptions.Blackout _config;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var pattern = new Pattern(_context.TargetBoard, _config.Size);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult()
            {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.Blackout()
                {
                    Position = result.Position.ToPosition()
                }
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Blackout>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Blackout payload)
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
                return _shape.All(_board, pointer);
            }
        }
    }
}
