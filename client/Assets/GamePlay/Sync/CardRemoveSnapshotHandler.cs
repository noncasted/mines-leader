using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Services;
using Shared;

namespace GamePlay
{
    public class CardRemoveSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.CardRemove>
    {
        public CardRemoveSnapshotHandler(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;

        public async UniTask Handle(PlayerSnapshotRecord.CardRemove record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            var card = player.Hand.Entries.FirstOrDefault(c => c.Id == record.CardId);

            if (card == null)
                return;

            await card.Destroy();
        }
    }
}
