using System;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class GameRoundState
    {
        public Guid CurrentPlayer { get; set; }
        public int SecondsLeft { get; set; }
    }
}