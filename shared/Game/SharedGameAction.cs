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
        public partial class UseBoardCard : INetworkContext
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }
        
        [MemoryPackable]
        public partial class UsePlayerCard : INetworkContext
        {
            public CardType Type { get; set; }
        }
    }
}