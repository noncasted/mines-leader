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
        public CardsStashedSnapshotHandler(
            IGameContext gameContext,
            IAudioPlayer audioPlayer,
            ILog<CardsStashedSnapshotHandler> log)
        {
            _log = log;
            _gameContext = gameContext;
            _audioPlayer = audioPlayer;
        }

        private readonly ILog _log;
        private readonly IGameContext _gameContext;
        private readonly IAudioPlayer _audioPlayer;

        public UniTask Handle(PlayerSnapshotRecord.CardsStashed record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            var cards = player.Table.Collect();

            _log.Debug("Handling stash snapshot for player {PlayerId}, cards {Count}", record.PlayerId, cards.Count);

            foreach (var card in cards)
                StashThenDestroy(card).NoAwait();

            if (cards.Count > 0)
                _audioPlayer.PlaySound(GamePlayAudio.GameCardStash);

            return UniTask.CompletedTask;
        }

        private async UniTask StashThenDestroy(ICard card)
        {
            // The flight is canceled with the card lifetime (the match is torn down): nothing is left to destroy then.
            await card.Stash.Enter(card.Lifetime).SuppressCancellationThrow();

            if (card.Lifetime.IsTerminated == false)
                await card.Destroy();
        }
    }
}
