using System;
using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    public static partial class SharedMatchmaking
    {
        [MemoryPackable]
        public partial class SearchLobby : INetworkContext
        {
        }

        [MemoryPackable]
        public partial class SearchMatch : INetworkContext
        {
            public GameMatchType Type { get; set; }
        }

        [MemoryPackable]
        public partial class Create : INetworkContext
        {
            public GameMatchType Type { get; set; }
        }

        [MemoryPackable]
        public partial class CancelSearch : INetworkContext
        {
        }

        [MemoryPackable]
        public partial class MatchResult : INetworkContext
        {
            public string ServerUrl { get; set; }
            public Guid SessionId { get; set; }
            public GameMatchType Type { get; set; }
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
                .Add<SearchLobby>()
                .Add<SearchMatch>()
                .Add<Create>()
                .Add<CancelSearch>()
                .Add<MatchResult>()
                .Add<LobbyResult>();
        }
    }
}