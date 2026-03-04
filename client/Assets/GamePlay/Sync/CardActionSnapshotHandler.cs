using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Cards;
using GamePlay.Loop;
using GamePlay.Services;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay
{
    public class CardActionSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.CardUse>
    {
        public CardActionSnapshotHandler(
            IReadOnlyLifetime lifetime,
            IGameContext gameContext)
        {
            _lifetime = lifetime;
            _gameContext = gameContext;
        }

        private readonly IReadOnlyLifetime _lifetime;
        private readonly IGameContext _gameContext;

        public async UniTask Handle(PlayerSnapshotRecord.CardUse record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);

            Debug.Log(
                $"Handling card action snapshot for player {record.PlayerId}, card {record.CardId}, data {record.Data}"
            );
            var card = player.Hand.Entries.First(t => t.Id == record.CardId)!;

            await card.Use(_lifetime, record.Data);

            UniTask.Create(async () =>
                    {
                        if (card is ILocalCard localCard)
                            await localCard.Drop.Enter(card.Lifetime);
                        else if (card is IRemoteCard remoteCard)
                            await remoteCard.Drop.Enter(card.Lifetime);

                        await card.Destroy();

                    }
                )
                .Forget();
        }
    }
}