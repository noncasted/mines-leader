using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Shared;

namespace GamePlay.Services
{
    public class GameCompletedSnapshotHandler : ISnapshotHandler<GameCompletedRecord>
    {
        public GameCompletedSnapshotHandler(IGameState gameState)
        {
            _gameState = gameState;
        }

        private readonly IGameState _gameState;

        public UniTask Handle(GameCompletedRecord record)
        {
            _gameState.SetWinner(record);
            return UniTask.CompletedTask;
        }
    }
}
