using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Shared;

namespace GamePlay.Services
{
    public class PlayerMovesSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.MovesUpdate>
    {
        public PlayerMovesSnapshotHandler(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;

        public UniTask Handle(PlayerSnapshotRecord.MovesUpdate record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            player.Turns.Set(record.Left, record.BaseMax, record.ResultMax, record.IsAvailable);

            return UniTask.CompletedTask;
        }
    }
}
