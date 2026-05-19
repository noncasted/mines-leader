using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Services;
using Shared;

namespace GamePlay
{
    public class PlayerModifierSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.ModifierUpdate>
    {
        public PlayerModifierSnapshotHandler(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;

        public UniTask Handle(PlayerSnapshotRecord.ModifierUpdate record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            player.Modifiers.UpdateOverview(record.Overview);
            return UniTask.CompletedTask;
        }
    }
}
