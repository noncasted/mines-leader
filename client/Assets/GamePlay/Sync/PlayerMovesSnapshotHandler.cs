using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Services;
using GamePlay.UI.ActionLog;
using Shared;

namespace GamePlay
{
    public class PlayerMovesSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.MovesUpdate>
    {
        public PlayerMovesSnapshotHandler(IGameContext gameContext, IGameActionLog actionLog)
        {
            _gameContext = gameContext;
            _actionLog = actionLog;
        }

        private readonly IGameContext _gameContext;
        private readonly IGameActionLog _actionLog;

        public UniTask Handle(PlayerSnapshotRecord.MovesUpdate record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            var oldMax = player.Moves.Max.Value;
            player.Moves.Set(record.Left, record.Max, record.IsAvailable);

            _actionLog.LogResourceChange(record.PlayerId, GameActionLogEntryType.MaxMovesChanged, "Max Moves", oldMax, record.Max);

            return UniTask.CompletedTask;
        }
    }
}
