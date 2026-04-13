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
            builder.AddSnapshotHandler<CardAddSnapshotHandler, PlayerSnapshotRecord.CardAdd>();
            builder.AddSnapshotHandler<CardRemoveSnapshotHandler, PlayerSnapshotRecord.CardRemove>();
            builder.AddSnapshotHandler<CardActionSnapshotHandler, PlayerSnapshotRecord.CardUse>();
            builder.AddSnapshotHandler<GameStartedSnapshotHandler, GameStartedRecord>();
            builder.AddSnapshotHandler<PlayerManaSnapshotHandler, PlayerSnapshotRecord.ManaUpdate>();
            builder.AddSnapshotHandler<PlayerHealthSnapshotHandler, PlayerSnapshotRecord.HealthUpdate>();
            builder.AddSnapshotHandler<PlayerMovesSnapshotHandler, PlayerSnapshotRecord.MovesUpdate>();

            return builder;
        }
    }
}