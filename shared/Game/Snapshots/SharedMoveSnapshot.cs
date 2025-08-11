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
    [MemoryPackUnion(0, typeof(SharedBoardSnapshot))]
    public partial interface IMoveSnapshotRecord
    {
    }
}