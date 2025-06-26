using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardBloodhoundAction : ICardAction
    {
        public CardBloodhoundAction(
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler)
        {
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
        }

        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;

        public async UniTask<bool> Execute(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = lifetime.Child();

            _pointerHandler.IsPressed.Advise(selectionLifetime, value =>
            {
                if (value == true)
                    return;

                selectionLifetime.Terminate();
            });

            var selected = await _dropArea.Show(lifetime, selectionLifetime, new Pattern());

            if (selected == null || selected.Cells.Count == 0 || lifetime.IsTerminated == true)
                return false;

            foreach (var cell in selected.Cells)
            {
                if (cell.HasMine() == true)
                    cell.EnsureTaken().Flag();
                else
                    cell.EnsureFree();
            }

            selected.Cells.CleanupAround();
            selected.Board.InvokeUpdated();

            return true;
        }

        public class Pattern : ICardDropPattern
        {
            private readonly int[][] _targets =
            {
                new[] { 0, 0, 1, 0, 0 },
                new[] { 0, 1, 1, 1, 0 },
                new[] { 1, 1, 1, 1, 1 },
                new[] { 0, 1, 1, 1, 0 },
                new[] { 0, 0, 1, 0, 0 },
            };

            public CardDropData GetDropData(IBoard board, Vector2Int pointer)
            {
                var cells = board.Cells;

                if (ValidateStart() == false)
                    return CardDropData.Empty(board);

                var selected = new List<IBoardCell>();
                var size = new Vector2Int(_targets.Length, _targets[0].Length);
                var offset = new Vector2Int(-Mathf.FloorToInt(size.x / 2f), -Mathf.FloorToInt(size.y / 2f));

                for (var x = 0; x < size.x; x++)
                {
                    for (var y = 0; y < size.y; y++)
                    {
                        if (_targets[x][y] == 0)
                            continue;
                        
                        var position = pointer + offset + new Vector2Int(x, y);

                        if (board.Cells.TryGetValue(position, out var cell) == false)
                            continue;

                        if (cell.IsTaken() == false)
                            continue;

                        selected.Add(cell);
                    }
                }

                return new CardDropData(selected, board);

                bool ValidateStart()
                {
                    if (board.IsMine == false)
                        return false;

                    if (cells.TryGetValue(pointer, out var startCheck) == false)
                        return false;

                    if (startCheck.State.Value.Status == CellStatus.Free)
                        return false;

                    return true;
                }
            }
        }
    }
}