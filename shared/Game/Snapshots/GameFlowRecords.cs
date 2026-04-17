using System;
using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class GameCompletedRecord : IMoveSnapshotRecord
    {
        public Guid Winner { get; set; }
    }

    [MemoryPackable]
    public partial class TimeLimitedRoundRecord : IMoveSnapshotRecord
    {
        public Guid CurrentPlayer { get; set; }
        public Dictionary<Guid, long> SecondsLeft { get; set; }
    }

    [MemoryPackable]
    public partial class LastManStandingRoundRecord : IMoveSnapshotRecord
    {
        public Guid CurrentPlayer { get; set; }
        public int CurrentRound { get; set; }
        public int SecondsLeft { get; set; }
    }
}