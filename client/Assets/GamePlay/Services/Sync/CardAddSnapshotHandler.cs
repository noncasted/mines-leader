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
    public class CardAddSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.CardAdd>
    {
        public CardAddSnapshotHandler(
            IReadOnlyLifetime lifetime,
            IGameContext gameContext,
            CardFactory cardFactory,
            IAudioPlayer audioPlayer,
            ILog<CardAddSnapshotHandler> log)
        {
            _log = log;
            _lifetime = lifetime;
            _gameContext = gameContext;
            _cardFactory = cardFactory;
            _audioPlayer = audioPlayer;
        }

        private readonly ILog _log;
        private readonly IReadOnlyLifetime _lifetime;
        private readonly IGameContext _gameContext;
        private readonly CardFactory _cardFactory;
        private readonly IAudioPlayer _audioPlayer;

        public async UniTask Handle(PlayerSnapshotRecord.CardAdd record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            var isLocal = player == _gameContext.Self;

            if (record.IsStash == true)
            {
                _log.Debug("Handling stash add snapshot for player {PlayerId}, card {CardId}, type {Type}",
                    record.PlayerId, record.CardId, record.Type);
                return;
            }

            _log.Debug("Handling card add snapshot for player {PlayerId}, card {CardId}, type {Type}",
                record.PlayerId, record.CardId, record.Type);
            _cardFactory.Create(_lifetime, isLocal, record.CardId, record.Type).Forget();
            _audioPlayer.PlayRandomFromGroup(GamePlayAudio.GameCardSpawn);
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f));
        }
    }
}