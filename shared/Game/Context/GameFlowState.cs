using System;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class GameFlowState
    {
        public Guid Winner { get; set; }
    }
}