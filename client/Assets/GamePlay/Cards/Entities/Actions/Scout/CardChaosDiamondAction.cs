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
            public Snapshot(IGameContext gameContext, ICardRandomAnimator randomAnimator)
            {
                _gameContext = gameContext;
                _randomAnimator = randomAnimator;
            }

            private readonly IGameContext _gameContext;
            private readonly ICardRandomAnimator _randomAnimator;

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ChaosDiamond payload)
            {
                await _randomAnimator.PlayDiceRoll(lifetime, payload.ActualSize);

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
