using System;
using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class TimeLimitedRoundState
    {
        public Guid CurrentPlayer { get; set; }
        public Dictionary<Guid, long> SecondsLeft { get; set; }
    }
}