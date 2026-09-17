using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Cards;
using GamePlay.Loop;
using Global.Audio;
using Internal;
using Shared;

namespace GamePlay.Services
{
    public class CardRemoveSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.CardRemove>
    {
        public CardRemoveSnapshotHandler(IGameContext gameContext, IAudioPlayer audioPlayer)
        {
            _gameContext = gameContext;
            _audioPlayer = audioPlayer;
        }

        private readonly IGameContext _gameContext;
        private readonly IAudioPlayer _audioPlayer;

        public async UniTask Handle(PlayerSnapshotRecord.CardRemove record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            var card = player.Hand.Entries.FirstOrDefault(c => c.Id == record.CardId);

            if (card == null)
                return;

            if (record.IsStash == false)
            {
                await card.Destroy();
                return;
            }

            // The card is discarded from hand: it leaves the layout right away and
            // flies off the screen into the stash before being destroyed.
            card.Hand.Remove(card);
            StashThenDestroy(card).NoAwait();
            _audioPlayer.PlaySound(GamePlayAudio.GameCardStash);
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
