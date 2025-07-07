using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Players;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardZipZapAction : ICardAction
    {
        public CardZipZapAction(
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            IPlayerModifiers modifiers, 
            ICardContext context)
        {
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _modifiers = modifiers;
            _context = context;
        }

        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly IPlayerModifiers _modifiers;
        private readonly ICardContext _context;

        private const int _searchRadius = 3;

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

            for (var i = 0; i < size; i++)
            {
                var next = SelectTarget(current.BoardPosition);

                if (next == null)
                    break;

                targets.Add(next);
            }

            if (targets.Count == 0)
                return false;

            foreach (var cell in targets)
                cell.EnsureFree();

            targets.CleanupAround();
            _context.TargetBoard.InvokeUpdated();

            _modifiers.Reset(PlayerModifier.TrebuchetBoost);

            return true;

            IBoardCell SelectTarget(Vector2Int center)
            {
                var searchPositions = searchShape.SelectTaken(_context.TargetBoard, center);

                var ordered = searchPositions
                    .Where(x => x.HasMine() == true)
                    .Where(x => targets.Contains(x) == false)
                    .OrderBy(x => Vector2Int.Distance(center, x.BoardPosition))
                    .ToList();

                if (ordered.Count == 0)
                    return null;

                return ordered.First();
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