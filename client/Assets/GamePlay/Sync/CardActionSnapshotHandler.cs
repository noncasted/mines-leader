using System;
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
                    await remoteCard.Drop.Enter(card.Lifetime, dropPosition);
                    break;
                case ILocalCard localCard:
                    await localCard.Drop.Enter(card.Lifetime, dropPosition);
                    break;
            }

            await card.Use(_lifetime, record.Data);
            StashThenDestroy(card).NoAwait();
        }

        private async UniTask StashThenDestroy(ICard card)
        {
            try
            {
                await card.Stash.Enter(card.Lifetime);
            }
            catch (OperationCanceledException)
            {
            }

            if (card.Lifetime.IsTerminated == false)
                await card.Destroy();
        }
    }
}
