using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Services;
using Shared;

namespace GamePlay
{
    public class GameStartedSnapshotHandler : ISnapshotHandler<GameStartedRecord>
    {
        public GameStartedSnapshotHandler(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;

        public UniTask Handle(GameStartedRecord record)
        {
            _gameContext.SetGameStarted();
            return UniTask.CompletedTask;
        }
    }
}
