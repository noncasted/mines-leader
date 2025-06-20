using System.Collections.Generic;
using GamePlay.Loop;
using UnityEngine;

namespace GamePlay.Boards
{
    public interface IBoardRevealer
    {
        void Reveal(Vector2Int position);
    }
    
    public class BoardRevealer : IBoardRevealer
    {
        public BoardRevealer(IGameContext context)
        {
            _context = context;
        }
        
        private readonly IGameContext _context;

        public void Reveal(Vector2Int position)
        {
            var board = _context.Self.Board;

            var passed = new HashSet<Vector2Int> { position };

            Check(position);

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
    }
}