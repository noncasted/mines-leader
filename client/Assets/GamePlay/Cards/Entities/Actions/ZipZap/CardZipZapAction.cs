using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Players;
using GamePlay.Services;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardZipZapAction : ICardAction, ICardActionSync<CardActionSnapshot.ZipZap>
    {
        public CardZipZapAction(
            IGameCamera camera,
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            ICardContext context,
            ICardVfxFactory vfxFactory,
            ZipZapOptions options)
        {
            _camera = camera;
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _context = context;
            _vfxFactory = vfxFactory;
            _options = options;
        }

        private readonly IGameCamera _camera;
        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly ICardContext _context;
        private readonly ICardVfxFactory _vfxFactory;
        private readonly ZipZapOptions _options;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var pattern = new Pattern(_context.TargetBoard);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult()
            {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.Trebuchet()
                {
                    Position = result.Position.ToPosition()
                }
            };
        }

        public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ZipZap payload)
        {
            var targets = new List<IBoardCell>();

            foreach (var position in payload.Targets)
            {
                var boardPosition = position.ToVector();
                var cell = _context.TargetBoard.Cells[boardPosition];
                targets.Add(cell);
            }

            targets.First().Explode(CellExplosionType.ZipZap).Forget();
            _camera.BaseShake();
            var lines = new List<ZipZapLine>();

            for (var index = 1; index < targets.Count; index++)
            {
                var start = targets[index - 1];
                var target = targets[index];

                var line = _vfxFactory.Create(_options.LinePrefab, Vector2.zero);
                await line.Show(lifetime, start, target);
                target.Explode(CellExplosionType.ZipZap).Forget();
                _camera.BaseShake();
                lines.Add(line);
            }

            foreach (var line in lines)
                Object.Destroy(line.gameObject);
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