using System.Collections.Generic;
using GamePlay.Loop;
using Internal;
using UnityEngine;

namespace GamePlay.Boards
{
    public class BoardMines : IBoardMines
    {
        private readonly IGameContext _gameContext;

        public BoardMines(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        public void Start(IReadOnlyLifetime lifetime)
        {
            foreach (var player in _gameContext.All)
            {
                var board = player.Board;
                board.Updated.Advise(lifetime, () => Recalculate(board));
            }
        }

        private void Recalculate(IBoard board)
        {
            var target = new Dictionary<Vector2Int, int>();
            var cells = board.Cells;

            foreach (var (position, _) in cells)
                target[position] = GetAround(board, position);

            foreach (var (_, cell) in cells)
            {
                if (cell.State.Value.Status != CellStatus.Free)
                    continue;

                var freeState = cell.EnsureFree();

                if (freeState.MinesAround.Value != target[cell.BoardPosition])
                    freeState.SetMinesAround(target[cell.BoardPosition]);
            }
        }

        private int GetAround(IBoard board, Vector2Int position)
        {
            var count = 0;

            var neighbours = board.NeighbourPositions(position);
            
            foreach (var neighbour in neighbours)
            {
                var cell = board.Cells[neighbour];
                var state = cell.State.Value;

                if (state.Status != CellStatus.Taken)
                    continue;

                var takenState = cell.EnsureTaken();

                if (takenState.HasMine.Value)
                    count++;
            }

            return count;
        }
    }
}