using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Players;
using GamePlay.Services;
using GamePlay.UI.ActionLog;
using Shared;

namespace GamePlay
{
    public class PlayerHealthSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.HealthUpdate>
    {
        public PlayerHealthSnapshotHandler(IGameContext gameContext, IGameActionLog actionLog)
        {
            _gameContext = gameContext;
            _actionLog = actionLog;
        }

        private readonly IGameContext _gameContext;
        private readonly IGameActionLog _actionLog;

        public UniTask Handle(PlayerSnapshotRecord.HealthUpdate record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            var oldCurrent = player.Health.Current.Value;
            var oldMax = player.Health.Max.Value;
            player.Health.Set(record.Current, record.Max);

            _actionLog.LogResourceChange(record.PlayerId, GameActionLogEntryType.HealthChanged, "HP", oldCurrent, record.Current);
            _actionLog.LogResourceChange(record.PlayerId, GameActionLogEntryType.HealthChanged, "Max HP", oldMax, record.Max);

            return UniTask.CompletedTask;
        }
    }
}
