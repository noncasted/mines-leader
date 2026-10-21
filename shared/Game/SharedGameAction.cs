using System;
using MemoryPack;

namespace Shared
{
    public partial class SharedGameAction
    {
        [MemoryPackable]
        public partial class Open : INetworkContext
        {
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class OpenMultiple : INetworkContext
        {
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class SetFlag : INetworkContext
        {
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class RemoveFlag : INetworkContext
        {
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class CardUse : INetworkContext
        {
            public Guid CardId { get; set; }
            public ICardUsePayload Payload { get; set; }
            public bool HasDropPosition { get; set; }
            public float DropX { get; set; }
            public float DropY { get; set; }
        }

        [MemoryPackable]
        public partial class SkipTurn : INetworkContext
        {
        }
    }

    [MemoryPackUnion(1016, typeof(SharedGameAction.Open))]
    [MemoryPackUnion(1017, typeof(SharedGameAction.OpenMultiple))]
    [MemoryPackUnion(1018, typeof(SharedGameAction.SetFlag))]
    [MemoryPackUnion(1019, typeof(SharedGameAction.RemoveFlag))]
    [MemoryPackUnion(1020, typeof(SharedGameAction.CardUse))]
    [MemoryPackUnion(1021, typeof(SharedGameAction.SkipTurn))]
    public partial class NetworkContextUnion
    {
    }
}
