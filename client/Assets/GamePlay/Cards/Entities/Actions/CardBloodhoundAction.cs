using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
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
            ICardPointerHandler pointerHandler)
        {
            _context = context;
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
        }

        private readonly ICardContext _context;
        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;

        public UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            return UniTask.FromResult(new CardActionResult());
        }
        
        public async UniTask<bool> Execute(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var size = _context.Type.GetSize();
            var pattern = new Pattern(_context.TargetBoard, size);
            var selected = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            if (selected == null || selected.Count == 0 || lifetime.IsTerminated == true)
                return false;

            foreach (var cell in selected)
            {
                // if (cell.HasMine() == true)
                //     cell.EnsureTaken().Flag();
                // else
                //     cell.EnsureFree();
            }

            return true;
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