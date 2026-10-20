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

    [MemoryPackUnion(26, typeof(SharedGameAction.Open))]
    [MemoryPackUnion(27, typeof(SharedGameAction.OpenMultiple))]
    [MemoryPackUnion(28, typeof(SharedGameAction.SetFlag))]
    [MemoryPackUnion(29, typeof(SharedGameAction.RemoveFlag))]
    [MemoryPackUnion(30, typeof(SharedGameAction.CardUse))]
    [MemoryPackUnion(31, typeof(SharedGameAction.SkipTurn))]
    public partial interface INetworkContext
    {
    }
}
