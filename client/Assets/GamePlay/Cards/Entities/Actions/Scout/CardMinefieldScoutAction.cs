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
    public class CardMinefieldScoutAction : ICardAction
    {
        public CardMinefieldScoutAction(
            ICardContext context,
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            CardConfigOptions.MinefieldScout config)
        {
            _context = context;
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _config = config;
        }

        private readonly ICardContext _context;
        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly CardConfigOptions.MinefieldScout _config;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var pattern = new Pattern(_context.TargetBoard, _config.Size);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult()
            {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.MinefieldScout()
                {
                    Position = result.Position.ToPosition()
                }
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.MinefieldScout>
        {
            public Snapshot(IGameContext gameContext)
            {
                _gameContext = gameContext;
            }

            private readonly IGameContext _gameContext;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.MinefieldScout payload)
            {
                var board = _gameContext.GetPlayer(payload.TargetPlayer).Board;

                if (payload.FlaggedCells != null)
                {
                    foreach (var position in payload.FlaggedCells)
                    {
                        var vector = position.ToVector();

                        if (board.Cells.TryGetValue(vector, out var cell))
                            cell.EnsureTaken().OnFlagUpdated(true);
                    }
                }

                if (payload.UpdatedFreeCells != null)
                {
                    foreach (var opened in payload.UpdatedFreeCells)
                    {
                        var vector = opened.Position.ToVector();

                        if (board.Cells.TryGetValue(vector, out var cell))
                            cell.EnsureFree().OnMinesUpdated(opened.MinesAround);
                    }
                }

                return UniTask.CompletedTask;
            }
        }

        public class Pattern : ICardDropPattern
        {
            public Pattern(IBoard board, int size)
            {
                _board = board;
                _shape = PatternShapes.Line(size, horizontal: true);
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