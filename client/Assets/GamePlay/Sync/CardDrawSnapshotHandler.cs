using Cysharp.Threading.Tasks;
using GamePlay.Cards;
using GamePlay.Loop;
using GamePlay.Services;
using Internal;
using Shared;

namespace GamePlay
{
    public class CardDrawSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.CardDraw>
    {
        public CardDrawSnapshotHandler(
            IReadOnlyLifetime lifetime,
            IGameContext gameContext,
            ICardFactory cardFactory) 
        {
            _lifetime = lifetime;
            _gameContext = gameContext;
            _cardFactory = cardFactory;
        }

        private readonly IReadOnlyLifetime _lifetime;
        private readonly IGameContext _gameContext;
        private readonly ICardFactory _cardFactory;

        public UniTask Handle(PlayerSnapshotRecord.CardDraw record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);
            
            if (player.Info.IsLocal == false)
                return UniTask.CompletedTask;

            _cardFactory.Create(_lifetime, record.Type, player.Deck.View.PickPoint);
            
            return UniTask.CompletedTask;
        }
    }
}