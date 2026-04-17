using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using GamePlay.Services;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardBloodhoundAction : ICardAction
    {
        public CardBloodhoundAction(
            ICardContext context,
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            CardConfigOptions.Bloodhound config)
        {
            _context = context;
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _config = config;
        }

        private readonly ICardContext _context;
        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly CardConfigOptions.Bloodhound _config;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var size = _config.Size;
            var pattern = new Pattern(_context.TargetBoard, size);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult()
            {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.Bloodhound()
                {
                    Position = result.Position.ToPosition()
                }
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.Bloodhound>
        {
            public Snapshot(IGameContext gameContext)
            {
                _gameContext = gameContext;
            }

            private readonly IGameContext _gameContext;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Bloodhound payload)
            {
                if (payload.UpdatedFreeCells == null || payload.UpdatedFreeCells.Count == 0)
                    return UniTask.CompletedTask;

                var board = _gameContext.GetPlayer(payload.TargetPlayer).Board;

                foreach (var opened in payload.UpdatedFreeCells)
                {
                    var vector = opened.Position.ToVector();

                    if (board.Cells.TryGetValue(vector, out var cell))
                        cell.EnsureFree().OnMinesUpdated(opened.MinesAround);
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
                var selected = _shape.SelectTaken(_board, pointer);
                return selected;
            }
        }
    }
}