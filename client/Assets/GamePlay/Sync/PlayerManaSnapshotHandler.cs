using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Players;
using GamePlay.Services;
using GamePlay.UI.ActionLog;
using Shared;

namespace GamePlay
{
    public class PlayerManaSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.ManaUpdate>
    {
        public PlayerManaSnapshotHandler(IGameContext gameContext, IGameActionLog actionLog)
        {
            _gameContext = gameContext;
            _actionLog = actionLog;
        }

        private readonly IGameContext _gameContext;
        private readonly IGameActionLog _actionLog;

        public UniTask Handle(PlayerSnapshotRecord.ManaUpdate record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            var oldCurrent = player.Mana.Current.Value;
            var oldMax = player.Mana.Max.Value;
            player.Mana.Set(record.Current, record.Max);

            _actionLog.LogResourceChange(record.PlayerId, GameActionLogEntryType.ManaChanged, "Mana", oldCurrent, record.Current);
            _actionLog.LogResourceChange(record.PlayerId, GameActionLogEntryType.ManaChanged, "Max Mana", oldMax, record.Max);

            return UniTask.CompletedTask;
        }
    }
}
