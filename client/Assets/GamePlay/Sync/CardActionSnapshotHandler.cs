using System.Linq;
using GamePlay.Loop;
using GamePlay.Services;
using Internal;
using Shared;

namespace GamePlay
{
    public class CardActionSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.Card>
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

        public void Handle(PlayerSnapshotRecord.Card record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);

            if (player.Info.IsLocal == false)
                return;

            var card = player.Hand.Entries.First(t => t.EntityId == record.EntityId)!;
            card.Use(_lifetime);
        }
    }
}