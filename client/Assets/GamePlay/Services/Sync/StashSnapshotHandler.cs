using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Services;
using Shared;

namespace GamePlay
{
    public class StashSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.StashUpdate>
    {
        public StashSnapshotHandler(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;

        public UniTask Handle(PlayerSnapshotRecord.StashUpdate record)
        {
            return UniTask.CompletedTask;
        }
    }
}
