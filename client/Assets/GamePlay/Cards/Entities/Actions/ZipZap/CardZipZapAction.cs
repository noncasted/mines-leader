using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using GamePlay.Services;
using Internal;
using Shared;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GamePlay.Cards
{
    public class CardZipZapAction : ICardAction
    {
        public CardZipZapAction(
            Guid cardId,
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            ICardContext context)
        {
            _cardId = cardId;
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _context = context;
        }

        private readonly Guid _cardId;
        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly ICardContext _context;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var pattern = new Pattern(_context.TargetBoard);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult()
            {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.ZipZap()
                {
                    Position = result.Position.ToPosition(),
                    CardId = _cardId
                }
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ZipZap>
        {
            public Snapshot(
                IBoardCellsAnimator animator,
                IGameCamera camera,
                IGameContext context,
                ICardVfxFactory vfxFactory)
            {
                _animator = animator;
                _camera = camera;
                _context = context;
                _vfxFactory = vfxFactory;
            }

            private readonly IBoardCellsAnimator _animator;
            private readonly IGameCamera _camera;
            private readonly IGameContext _context;
            private readonly ICardVfxFactory _vfxFactory;

            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ZipZap payload)
            {
                var options = GamePlayAssets.ZipZapOptions;

                // 1. Target animation
                if (payload.TargetCells.Count > 0)
                    await _animator.PlayTargetAnimation(lifetime, payload.TargetPlayer, payload.TargetCells);

                // 2. Action animation
                if (payload.OpenedCells.Count > 0)
                {
                    var positions = payload.OpenedCells.Select(o => o.Position).ToList();
                    await _animator.PlayActionAnimation(lifetime, payload.TargetPlayer, positions);
                }
                
                var board = _context.GetPlayer(payload.TargetPlayer).Board;
                var targets = new List<(Position Position, IBoardCell Cell)>();

                foreach (var position in payload.TargetCells)
                {
                    if (board.Cells.TryGetValue(position.ToVector(), out var cell) == false)
                        continue;

                    targets.Add((position, cell));
                }

                if (targets.Count > 0)
                {
                    _animator.ExplodeCell(payload.TargetPlayer, targets[0].Position, CellExplosionType.ZipZap).Forget();
                    _camera.BaseShake();

                    // Lines stay alive until the whole chain is played, so they are tracked and
                    // destroyed in a finally: a terminated lifetime cancels the awaits below and
                    // would otherwise leave the lightning VFX hanging on the board forever.
                    var lines = new List<ZipZapLine>();

                    try
                    {
                        for (var index = 1; index < targets.Count; index++)
                        {
                            if (lifetime.IsTerminated == true)
                                break;

                            var start = targets[index - 1].Cell;
                            var target = targets[index].Cell;

                            var line = _vfxFactory.Create(options.LinePrefab, Vector2.zero);
                            lines.Add(line);

                            await line.Show(lifetime, start, target);
                            _animator.ExplodeCell(payload.TargetPlayer, targets[index].Position, CellExplosionType.ZipZap).Forget();
                            _camera.BaseShake();
                        }
                    }
                    finally
                    {
                        foreach (var line in lines)
                        {
                            if (line != null)
                                Object.Destroy(line.gameObject);
                        }
                    }
                }

                await _animator.OpenCells(lifetime, payload.TargetPlayer, payload.UpdatedFreeCells);
            }
        }

        public class Pattern : ICardDropPattern
        {
            public Pattern(IBoard board)
            {
                _board = board;
            }

            private readonly IBoard _board;
            private readonly int _startArea = 1;

            public Vector2Int LastPointer { get; set; }

            public IReadOnlyList<IBoardCell> GetDropData(Vector2Int pointer)
            {
                LastPointer = pointer;

                var startShape = PatternShapes.Rhombus(_startArea);
                var startPositions = startShape.SelectTaken(_board, pointer);

                return startPositions;
            }
        }
    }
}