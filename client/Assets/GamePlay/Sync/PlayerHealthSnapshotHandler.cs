using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Services;
using Shared;

namespace GamePlay
{
    public class PlayerHealthSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.HealthUpdate>
    {
        public PlayerHealthSnapshotHandler(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;

        public UniTask Handle(PlayerSnapshotRecord.HealthUpdate record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            player.Health.Set(record.Current, record.Max);

            return UniTask.CompletedTask;
        }
    }
}
