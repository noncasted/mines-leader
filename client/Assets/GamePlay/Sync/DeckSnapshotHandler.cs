using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Services;
using Shared;

namespace GamePlay
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
            var player = _gameContext.GetPlayer(record.PlayerId);
            player.Deck.SetCount(record.Count);
            return UniTask.CompletedTask;
        }
    }
}
