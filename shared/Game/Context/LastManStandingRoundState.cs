using System;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class LastManStandingRoundState
    {
        public Guid CurrentPlayer { get; set; }
        public int CurrentRound { get; set; }
        public int SecondsLeft { get; set; }
    }
}
