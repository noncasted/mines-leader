using System;
using Cysharp.Threading.Tasks;
using GamePlay.Cards;
using GamePlay.Loop;
using GamePlay.Services;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay
{
    public class CardAddSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.CardAdd>
    {
        public CardAddSnapshotHandler(
            IReadOnlyLifetime lifetime,
            IGameContext gameContext,
            CardFactory cardFactory)
        {
            _lifetime = lifetime;
            _gameContext = gameContext;
            _cardFactory = cardFactory;
        }

        private readonly IReadOnlyLifetime _lifetime;
        private readonly IGameContext _gameContext;
        private readonly CardFactory _cardFactory;

        public async UniTask Handle(PlayerSnapshotRecord.CardAdd record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            var isLocal = player == _gameContext.Self;

            Debug.Log(
                $"Handling card add snapshot for player {record.PlayerId}, card {record.CardId}, type {record.Type}");
            _cardFactory.Create(_lifetime, isLocal, record.CardId, record.Type).Forget();
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f));
        }
    }
}