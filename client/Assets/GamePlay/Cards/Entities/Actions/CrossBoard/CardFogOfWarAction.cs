using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardFogOfWarAction : ICardAction
    {
        public CardFogOfWarAction(
            ICardContext context,
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            CardConfigOptions.FogOfWar config)
        {
            _context = context;
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _config = config;
        }

        private readonly ICardContext _context;
        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly CardConfigOptions.FogOfWar _config;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var pattern = new Pattern(_context.TargetBoard, _config.Size);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult()
            {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.FogOfWar()
                {
                    Position = result.Position.ToPosition()
                }
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.FogOfWar>
        {
            public Snapshot(IGameContext gameContext)
            {
                _gameContext = gameContext;
            }

            private readonly IGameContext _gameContext;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.FogOfWar payload)
            {
                if (payload.AffectedCells == null || payload.AffectedCells.Length == 0)
                    return UniTask.CompletedTask;

                var board = _gameContext.GetPlayer(payload.TargetPlayer).Board;

                foreach (var position in payload.AffectedCells)
                {
                    if (board.Cells.TryGetValue(position.ToVector(), out var cell) && cell is CellView cellView)
                        cellView.Effects.AddEffect(payload.EffectId, CellEffectType.Fog);
                }

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
                return _shape.SelectFree(_board, pointer);
            }
        }
    }
}