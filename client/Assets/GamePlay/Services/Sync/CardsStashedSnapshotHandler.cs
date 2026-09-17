using System;
using Cysharp.Threading.Tasks;
using GamePlay.Cards;
using GamePlay.Loop;
using Global.Audio;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Services
{
    public class CardsStashedSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.CardsStashed>
    {
        public CardsStashedSnapshotHandler(IGameContext gameContext, IAudioPlayer audioPlayer)
        {
            _gameContext = gameContext;
            _audioPlayer = audioPlayer;
        }

        private readonly IGameContext _gameContext;
        private readonly IAudioPlayer _audioPlayer;

        public UniTask Handle(PlayerSnapshotRecord.CardsStashed record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            var cards = player.Table.Collect();

            Debug.Log($"Handling stash snapshot for player {record.PlayerId}, cards {cards.Count}");

            foreach (var card in cards)
                StashThenDestroy(card).NoAwait();

            if (cards.Count > 0)
                _audioPlayer.PlaySound(GamePlayAudio.GameCardStash);

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
