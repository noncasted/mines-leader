using Internal;
using Shared;

namespace GamePlay.Services
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
            builder.AddSnapshotHandler<PlayerModifierSnapshotHandler, PlayerSnapshotRecord.ModifierUpdate>();
            builder.AddSnapshotHandler<DeckSnapshotHandler, PlayerSnapshotRecord.DeckUpdate>();
            builder.AddSnapshotHandler<StashSnapshotHandler, PlayerSnapshotRecord.StashUpdate>();
            builder.AddSnapshotHandler<CardsStashedSnapshotHandler, PlayerSnapshotRecord.CardsStashed>();
            builder.AddSnapshotHandler<BoardStateUpdateSnapshotHandler, PlayerSnapshotRecord.BoardStateUpdate>();
            builder.AddSnapshotHandler<GameCompletedSnapshotHandler, GameCompletedRecord>();

            return builder;
        }
    }
}