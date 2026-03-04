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
        }

        [MemoryPackable]
        public partial class SkipTurn : INetworkContext
        {
        }

        public static IUnionBuilder<INetworkContext> Register(IUnionBuilder<INetworkContext> builder)
        {
            return builder
                .Add<Open>()
                .Add<OpenMultiple>()
                .Add<SetFlag>()
                .Add<RemoveFlag>()
                .Add<CardUse>()
                .Add<SkipTurn>();
        }
    }
}