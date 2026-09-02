using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Shared;

namespace GamePlay.Services
{
    public class DeckSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.DeckUpdate>
    {
        public DeckSnapshotHandler(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;

        public UniTask Handle(PlayerSnapshotRecord.DeckUpdate record)
        {
            return UniTask.CompletedTask;
        }
    }
}
