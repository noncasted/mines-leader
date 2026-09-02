using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Cards;
using GamePlay.Loop;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Services
{
    public class CardActionSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.CardUse>
    {
        public CardActionSnapshotHandler(IReadOnlyLifetime lifetime, IGameContext gameContext)
        {
            _lifetime = lifetime;
            _gameContext = gameContext;
        }

        private readonly IReadOnlyLifetime _lifetime;
        private readonly IGameContext _gameContext;

        public async UniTask Handle(PlayerSnapshotRecord.CardUse record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            var card = player.Hand.Entries.First(t => t.Id == record.CardId)!;

            Debug.Log(
                $"Handling card action snapshot for player {record.PlayerId}, card {record.CardId}, data {record.Data}");

            Vector2? dropPosition = record.HasDropPosition
                ? new Vector2(record.DropX, record.DropY)
                : null;

            card.Hand.Remove(card);

            switch (card)
            {
                case IRemoteCard remoteCard:
                    remoteCard.Reveal();
                    await remoteCard.Drop.Enter(_lifetime, dropPosition, record.Data);
                    break;
                case ILocalCard localCard:
                    await localCard.Drop.Enter(_lifetime, dropPosition, record.Data);
                    break;
            }

            // The card stays on the table in the dropped state; the flight into the
            // stash is driven by the server (PlayerSnapshotRecord.CardsStashed).
            player.Table.Add(card);
            card.Dropped.Enter(card.Lifetime).NoAwait();
        }
    }
}
