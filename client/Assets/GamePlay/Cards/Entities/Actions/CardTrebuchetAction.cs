using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Players;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardTrebuchetAction : ICardAction
    {
        public CardTrebuchetAction(
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            IPlayerModifiers modifiers)
        {
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _modifiers = modifiers;
        }

        private const int _minesAmount = 5;
        private const int _size = 5;

        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly IPlayerModifiers _modifiers;

        public async UniTask<bool> Execute(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = lifetime.Child();

            _pointerHandler.IsPressed.Advise(selectionLifetime, value =>
            {
                if (value == true)
                    return;

                selectionLifetime.Terminate();
            });

            var size = _size + (int)_modifiers.Values[PlayerModifier.TrebuchetBoost] * 2;
            var selected = await _dropArea.Show(lifetime, selectionLifetime, new Pattern(size));

            if (selected == null || selected.Cells.Count == 0 || lifetime.IsTerminated == true)
                return false;

            var shuffled = new List<IBoardCell>(selected.Cells);
            shuffled.Shuffle();

            for (var index = 0; index < shuffled.Count; index++)
            {
                var cell = shuffled[index];
                var taken = cell.EnsureTaken();

                if (index < _minesAmount)
                    taken.SetMine();
            }

            selected.Board.InvokeUpdated();

            _modifiers.Reset(PlayerModifier.TrebuchetBoost);

            return true;
        }

        public class Pattern : ICardDropPattern
        {
            public Pattern(int size)
            {
                var positions = new bool[size][];
                var offsets = new int[size];
                var startOffset = size / 2;
                var currentOffset = startOffset;
                var middlePoint = Mathf.FloorToInt((size - 1) / 2f);

                for (var i = 0; i < size; i++)
                {
                    offsets[i] = currentOffset;

                    if (i < middlePoint)
                    {
                        currentOffset--;
                    }
                    else if (i >= middlePoint)
                    {
                        currentOffset++;
                    }
                }

                for (var i = 0; i < size; i++)
                {
                    positions[i] = new bool[size];
                    var offset = offsets[i];

                    for (var j = 0; j < size; j++)
                    {
                        if (j < offset || j >= size - offset)
                        {
                            positions[i][j] = false;
                        }
                        else
                        {
                            positions[i][j] = true;
                        }
                    }
                }

                _positions = positions;
            }

            private readonly bool[][] _positions;

            public CardDropData GetDropData(IBoard board, Vector2Int pointer)
            {
                if (board.IsMine == true)
                    return new CardDropData(new List<IBoardCell>(), board);

                var size = _positions.Length;
                var halfSize = size / 2;
                var start = pointer - new Vector2Int(halfSize, halfSize);

                var selected = new List<IBoardCell>();

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        if (_positions[y][x] == false)
                            continue;

                        var position = start + new Vector2Int(x, y);

                        if (board.Cells.TryGetValue(position, out var cell) == false)
                            continue;

                        if (cell.State.Value.Status != CellStatus.Free)
                            continue;

                        selected.Add(board.Cells[position]);
                    }
                }

                return new CardDropData(selected, board);
            }
        }
    }
}