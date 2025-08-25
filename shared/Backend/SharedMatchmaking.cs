using System;
using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    public static partial class SharedMatchmaking
    {
        [MemoryPackable]
        public partial class Search : INetworkContext
        {
            public SessionType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Create : INetworkContext
        {
        }

        [MemoryPackable]
        public partial class CancelSearch : INetworkContext
        {
        }

        [MemoryPackable]
        public partial class GameResult : INetworkContext
        {
            public string ServerUrl { get; set; }
            public Guid SessionId { get; set; }
        }

        [MemoryPackable]
        public partial class LobbyResult : INetworkContext
        {
            public string ServerUrl { get; set; }
            public Guid SessionId { get; set; }
        }
        
        public static IUnionBuilder<INetworkContext> Register(IUnionBuilder<INetworkContext> builder)
        {
            return builder
                .Add<Search>()
                .Add<Create>()
                .Add<CancelSearch>()
                .Add<GameResult>()
                .Add<LobbyResult>();
        }
    }
}