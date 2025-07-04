using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Players;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardTrebuchetAction : ICardAction
    {
        public CardTrebuchetAction(
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            IPlayerModifiers modifiers,
            CardType type,
            ICardContext context)
        {
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _modifiers = modifiers;
            _type = type;
            _context = context;
        }

        private int _minesAmount => _type switch
        {
            CardType.Trebuchet => CardsConfigs.Trebuchet.NormalMines,
            CardType.Trebuchet_Max => CardsConfigs.Trebuchet.MaxMines,
            _ => throw new ArgumentOutOfRangeException()
        };

        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly IPlayerModifiers _modifiers;
        private readonly CardType _type;
        private readonly ICardContext _context;

        public async UniTask<bool> Execute(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var size = _type.GetSize() + (int)_modifiers.Values[PlayerModifier.TrebuchetBoost] * 2;
            var pattern = new Pattern(_context.TargetBoard, size);
            var selected = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            if (selected == null || selected.Count == 0 || lifetime.IsTerminated == true)
                return false;

            var shuffled = new List<IBoardCell>(selected);
            shuffled.Shuffle();

            for (var index = 0; index < shuffled.Count; index++)
            {
                var cell = shuffled[index];
                var taken = cell.EnsureTaken();

                if (index < _minesAmount)
                    taken.SetMine();
            }

            _context.TargetBoard.InvokeUpdated();

            _modifiers.Reset(PlayerModifier.TrebuchetBoost);

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