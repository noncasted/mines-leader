using GamePlay.Loop;
using UnityEngine;

namespace GamePlay.Boards
{
    public class BoardGenerator : IBoardGenerator
    {
        public BoardGenerator(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;

        public void Generate(Vector2Int from)
        {
            var board = _gameContext.Self.Board;
            var cells = board.Cells;
            var ignored = board.NeighbourPositions(from);
            ignored.Add(from);

            var minesSpawned = 0;
            var requiredMines = _gameContext.Options.Mines;
            
            while (minesSpawned < requiredMines)
            {
                var random = board.RandomPosition();
                
                if (ignored.Contains(random))
                    continue;

                cells[random].EnsureTaken().SetMine();
                minesSpawned++;
            }
        }
    }
}