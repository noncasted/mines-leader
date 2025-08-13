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
    public class CardZipZapAction : ICardAction
    {
        public CardZipZapAction(
            IGameCamera camera,
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            IPlayerModifiers modifiers,
            ICardContext context,
            ICardVfxFactory vfxFactory,
            ZipZapOptions options)
        {
            _camera = camera;
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _modifiers = modifiers;
            _context = context;
            _vfxFactory = vfxFactory;
            _options = options;
        }

        private readonly IGameCamera _camera;
        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly IPlayerModifiers _modifiers;
        private readonly ICardContext _context;
        private readonly ICardVfxFactory _vfxFactory;
        private readonly ZipZapOptions _options;

        private const int _searchRadius = 6;

        public UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            return UniTask.FromResult(new CardActionResult());
        }

        public async UniTask<bool> Execute(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var pattern = new Pattern(_context.TargetBoard);
            var selected = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            if (selected == null || selected.Count == 0 || lifetime.IsTerminated == true)
                return false;

            var size = _context.Type.GetSize();

            var searchShape = PatternShapes.Rhombus(_searchRadius);

            var targets = new List<IBoardCell>();
            var current = SelectTarget(pattern.LastPointer);
            targets.Add(current);

            for (var i = 1; i < size; i++)
            {
                current = SelectTarget(current.BoardPosition);

                if (current == null)
                    break;

                targets.Add(current);
            }

            if (targets.Count == 0)
                return false;

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

//            _modifiers.Reset(PlayerModifier.TrebuchetBoost);

            return true;

            IBoardCell SelectTarget(Vector2Int center)
            {
                return null;
                // var searchPositions = searchShape.SelectTaken(_context.TargetBoard, center);
                // var hasMine = searchPositions.Where(x => x.HasMine() == true);
                // var hasFlags = hasMine.Where(x => x.HasFlag() == false);
                // var unique = hasFlags.Where(x => targets.Contains(x) == false);
                //
                // var ordered = unique
                //     .OrderBy(x => Vector2Int.Distance(center, x.BoardPosition))
                //     .ToList();
                //
                // if (ordered.Count == 0)
                //     return null;
                //
                // return ordered.First();
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