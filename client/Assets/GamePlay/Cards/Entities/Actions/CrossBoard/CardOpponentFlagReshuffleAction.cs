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
            public Snapshot(IGameContext gameContext)
            {
                _gameContext = gameContext;
            }

            private readonly IGameContext _gameContext;

            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.OpponentFlagReshuffle payload)
            {
                var board = _gameContext.GetPlayer(payload.TargetPlayer).Board;

                if (payload.UnflaggedCells != null)
                {
                    foreach (var position in payload.UnflaggedCells)
                    {
                        var vector = position.ToVector();

                        if (board.Cells.TryGetValue(vector, out var cell))
                            cell.EnsureTaken().OnFlagUpdated(false);
                    }
                }

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