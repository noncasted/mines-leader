using System.Collections.Generic;
using System.Linq;
using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Loop;
using GamePlay.Services;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardZipZapAction : ICardAction
    {
        public CardZipZapAction(
            INetworkEntity entity,
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            ICardContext context)
        {
            _entity = entity;
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _context = context;
        }

        private readonly INetworkEntity _entity;
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
                    EntityId = _entity.Id
                }
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ZipZap>
        {
            public Snapshot(
                IGameCamera camera,
                IGameContext context,
                ICardVfxFactory vfxFactory,
                ZipZapOptions options)
            {
                _camera = camera;
                _context = context;
                _vfxFactory = vfxFactory;
                _options = options;
            }

            private readonly IGameCamera _camera;
            private readonly IGameContext _context;
            private readonly ICardVfxFactory _vfxFactory;
            private readonly ZipZapOptions _options;
            
            public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ZipZap payload)
            {
                var targets = new List<IBoardCell>();
                var board = _context.GetPlayer(payload.TargetPlayer).Board;

                foreach (var position in payload.Targets)
                {
                    var boardPosition = position.ToVector();
                    var cell = board.Cells[boardPosition];
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