using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Services;
using Shared;

namespace GamePlay
{
    public class PlayerManaSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.ManaUpdate>
    {
        public PlayerManaSnapshotHandler(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;

        public UniTask Handle(PlayerSnapshotRecord.ManaUpdate record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            player.Mana.Set(record.Current, record.BaseMax, record.ResultMax);

            return UniTask.CompletedTask;
        }
    }
}
