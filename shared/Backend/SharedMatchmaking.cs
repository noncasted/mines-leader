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
        public partial class CreateWithBot : INetworkContext
        {
            public GameMatchType Type { get; set; }
            public AgentMatchFixture Fixture { get; set; }
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

            // S4: the entry into the session for this user (SharedSessionAuth.Request.Ticket), opaque to the client.
            public string Ticket { get; set; }
        }

        [MemoryPackable]
        public partial class LobbyResult : INetworkContext
        {
            public string ServerUrl { get; set; }
            public Guid SessionId { get; set; }

            // S4: the entry into the session for this user (SharedSessionAuth.Request.Ticket), opaque to the client.
            public string Ticket { get; set; }
        }
    }

    [MemoryPackUnion(18, typeof(SharedMatchmaking.SearchLobby))]
    [MemoryPackUnion(19, typeof(SharedMatchmaking.SearchMatch))]
    [MemoryPackUnion(20, typeof(SharedMatchmaking.Create))]
    [MemoryPackUnion(21, typeof(SharedMatchmaking.CreateWithBot))]
    [MemoryPackUnion(22, typeof(SharedMatchmaking.CancelSearch))]
    [MemoryPackUnion(23, typeof(SharedMatchmaking.MatchResult))]
    [MemoryPackUnion(24, typeof(SharedMatchmaking.LobbyResult))]
    public partial interface INetworkContext
    {
    }
}
