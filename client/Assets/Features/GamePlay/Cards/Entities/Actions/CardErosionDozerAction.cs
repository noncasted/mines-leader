using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardErosionDozerAction : ICardAction
    {
        public CardErosionDozerAction(
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
                cell.EnsureFree();

            Cleanup(selected.Cells);

            selected.Board.InvokeUpdated();

            return true;
        }

        private void Cleanup(IReadOnlyList<IBoardCell> cells)
        {
            var board = cells.First().Source;

            var passed = new HashSet<Vector2Int>();

            foreach (var cell in cells)
                passed.Add(cell.BoardPosition);
            
            foreach (var cell in cells)
                Check(cell.BoardPosition);

            foreach (var target in passed)
            {
                var cell = board.Cells[target];
                cell.EnsureFree();
            }

            void Check(Vector2Int target)
            {
                var neighbours = board.NeighbourPositions(target);
                neighbours.RemoveWhere(t => passed.Contains(t));
                neighbours.RemoveWhere(t => board.Cells[t].State.Value.Status == CellStatus.Free);

                foreach (var neighbour in neighbours)
                {
                    var cell = board.Cells[neighbour];

                    if (cell.State.Value.Status != CellStatus.Taken)
                        continue;

                    var taken = cell.EnsureTaken();

                    if (taken.HasMine.Value == true)
                        return;
                }

                foreach (var neighbour in neighbours)
                {
                    passed.Add(neighbour);
                    Check(neighbour);
                }
            }
        }

        public class Pattern : ICardDropPattern
        {
            private readonly List<Vector2Int> _directions = new()
            {
                new(0, 1),
                new(1, 1),
                new(1, 0),
                new(1, -1),
                new(0, -1),
                new(-1, -1),
                new(-1, 0),
                new(-1, 1),
            };
            
            public CardDropData GetDropData(IBoard board, Vector2Int pointer)
            {
                var cells = board.Cells;

                if (ValidateStart() == false)
                    return CardDropData.Empty(board);

                var selected = new HashSet<Vector2Int>();
                var checkedCache = new HashSet<Vector2Int>();
                
                Check(pointer);
                
                var result = new List<IBoardCell>();
                
                foreach (var position in selected)
                {
                    if (cells.TryGetValue(position, out var cell) == false)
                        throw new KeyNotFoundException();
                    
                    result.Add(cell);
                }

                return new CardDropData(result, board);

                bool Check(Vector2Int position)
                {
                    if (cells.TryGetValue(position, out var cell) == false)
                        return false;
                    
                    if (cell.State.Value.Status == CellStatus.Free)
                        return true;
                    
                    if (checkedCache.Contains(position) == true)
                        return false;
                    
                    checkedCache.Add(position);

                    foreach (var direction in _directions)
                    {
                        var next = position + direction;
                        
                        if (Check(next) == true)
                            selected.Add(position);
                    }
                    
                    return false;
                }
                
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