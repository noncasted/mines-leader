using System;
using Cysharp.Threading.Tasks;
using GamePlay.Cards;
using GamePlay.Loop;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Services
{
    public class CardsStashedSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.CardsStashed>
    {
        public CardsStashedSnapshotHandler(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;

        public UniTask Handle(PlayerSnapshotRecord.CardsStashed record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            var cards = player.Table.Collect();

            Debug.Log($"Handling stash snapshot for player {record.PlayerId}, cards {cards.Count}");

            foreach (var card in cards)
                StashThenDestroy(card).NoAwait();

            return UniTask.CompletedTask;
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
