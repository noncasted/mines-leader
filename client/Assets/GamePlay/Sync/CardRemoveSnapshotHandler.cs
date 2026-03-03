using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Cards;
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

        public UniTask Handle(PlayerSnapshotRecord.CardRemove record)
        {
            return UniTask.CompletedTask;
            var player = _gameContext.GetPlayer(record.PlayerId);

            var card = player.Hand.Entries.First(t => t.EntityId == record.EntityId)!;

            if (card is ILocalCard local)
                local.Destroy().Forget();

            return UniTask.CompletedTask;
        }
    }
}