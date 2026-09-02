using System;
using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class SharedMoveSnapshot : INetworkContext
    {
        public IReadOnlyList<IMoveSnapshotRecord> Records { get; set; }

        public static IUnionBuilder<INetworkContext> Register(IUnionBuilder<INetworkContext> builder)
        {
            return builder.Add<SharedMoveSnapshot>();
        }
    }

    [MemoryPackable]
    public partial class SharedBoardSnapshot : IMoveSnapshotRecord
    {
        public Guid BoardOwnerId { get; set; }
        public List<IBoardSnapshotRecord> Records { get; set; }
    }

    [MemoryPackable]
    public partial class GameStartedRecord : IMoveSnapshotRecord
    {
        public int CardMovesCost { get; set; } = 1;
    }

    [MemoryPackable]
    [MemoryPackUnion(0, typeof(SharedBoardSnapshot))]
    [MemoryPackUnion(1, typeof(PlayerSnapshotRecord.CardUse))]
    [MemoryPackUnion(2, typeof(PlayerSnapshotRecord.CardAdd))]
    [MemoryPackUnion(3, typeof(PlayerSnapshotRecord.CardRemove))]
    [MemoryPackUnion(4, typeof(GameStartedRecord))]
    [MemoryPackUnion(5, typeof(PlayerSnapshotRecord.ManaUpdate))]
    [MemoryPackUnion(6, typeof(PlayerSnapshotRecord.HealthUpdate))]
    [MemoryPackUnion(7, typeof(PlayerSnapshotRecord.MovesUpdate))]
    [MemoryPackUnion(8, typeof(PlayerSnapshotRecord.ModifierUpdate))]
    [MemoryPackUnion(9, typeof(PlayerSnapshotRecord.DeckUpdate))]
    [MemoryPackUnion(10, typeof(PlayerSnapshotRecord.StashUpdate))]
    [MemoryPackUnion(11, typeof(PlayerSnapshotRecord.BoardStateUpdate))]
    [MemoryPackUnion(12, typeof(GameCompletedRecord))]
    [MemoryPackUnion(13, typeof(TimeLimitedRoundRecord))]
    [MemoryPackUnion(14, typeof(LastManStandingRoundRecord))]
    [MemoryPackUnion(15, typeof(PlayerSnapshotRecord.CardsStashed))]
    public partial interface IMoveSnapshotRecord
    {
    }
}