using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    /// <summary>
    /// Highlights mines in a diamond without flagging.
    /// </summary>
    public class CardThermalVisionAction : ICardAction
    {
        public CardThermalVisionAction(
            ICardContext context,
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            CardConfigOptions.ThermalVision config)
        {
            _context = context;
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _config = config;
        }

        private readonly ICardContext _context;
        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly CardConfigOptions.ThermalVision _config;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var pattern = new Pattern(_context.TargetBoard, _config.Size);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult()
            {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.ThermalVision()
                {
                    Position = result.Position.ToPosition()
                }
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ThermalVision>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ThermalVision payload)
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
