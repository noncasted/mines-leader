using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Shared;

namespace GamePlay.Services
{
    public class BoardStateUpdateSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.BoardStateUpdate>
    {
        public BoardStateUpdateSnapshotHandler(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;

        public UniTask Handle(PlayerSnapshotRecord.BoardStateUpdate record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            player.Board.UpdateState(record.Mines, record.Flags);
            return UniTask.CompletedTask;
        }
    }
}
