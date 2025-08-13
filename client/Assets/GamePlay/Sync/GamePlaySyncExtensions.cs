using GamePlay.Services;
using Internal;
using Shared;

namespace GamePlay
{
    public static class GamePlaySyncExtensions
    {
        public static IScopeBuilder AddSnapshotSync(this IScopeBuilder builder)
        {
            builder.AddSnapshotHandler<BoardSnapshotHandler, SharedBoardSnapshot>();
            builder.AddSnapshotHandler<CardActionSnapshotHandler, PlayerSnapshotRecord.Card>();
            builder.AddSnapshotHandler<CardDrawSnapshotHandler, PlayerSnapshotRecord.CardDraw>();
            builder.AddSnapshotHandler<DeckFillSnapshotHandler, PlayerSnapshotRecord.DeckFill>();
            builder.AddSnapshotHandler<ReshuffleFromStashSnapshotHandler, PlayerSnapshotRecord.ReshuffleFromStash>();

            return builder;
        }
    }
}