using System;
using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    public static partial class SharedMatchmaking
    {
        // Answered with LobbyResult: the server creates the session inside the command.
        [MemoryPackable]
        public partial class SearchLobby : INetworkContext
        {
        }

        // Answered with EmptyResponse when the player is queued. The result comes later as a MatchResult projection
        // with the same RequestId, when the queue pairs the player or fails to create the match.
        [MemoryPackable]
        public partial class SearchMatch : INetworkContext
        {
            public GameMatchType Type { get; set; }

            // Chosen by the client for each search: a result of an earlier (cancelled, replaced) search is told apart.
            public Guid RequestId { get; set; }
        }

        // Answered with MatchResult: the server creates the session inside the command.

        [MemoryPackable]
        public partial class Create : INetworkContext
        {
            public GameMatchType Type { get; set; }
        }

        // Answered with MatchResult: the server creates the session inside the command.
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

        // The response to Create and CreateWithBot, and the projection that ends a SearchMatch.
        // ServerUrl, SessionId and Ticket are set only when Status is Matched.
        [MemoryPackable]
        public partial class MatchResult : INetworkContext
        {
            public string ServerUrl { get; set; }
            public Guid SessionId { get; set; }
            public GameMatchType Type { get; set; }

            // S4: the entry into the session for this user (SharedSessionAuth.Request.Ticket), opaque to the client.
            public string Ticket { get; set; }

            // SearchMatch.RequestId of the search this result ends; Guid.Empty in a command response.
            public Guid RequestId { get; set; }

            public MatchSearchStatus Status { get; set; }
        }

        public enum MatchSearchStatus
        {
            Matched = 0,

            // The queue paired the player, but the match was not created: the search is over, the player is out of
            // the queue.
            Failed = 1
        }

        // The response to SearchLobby.
        [MemoryPackable]
        public partial class LobbyResult : INetworkContext
        {
            public string ServerUrl { get; set; }
            public Guid SessionId { get; set; }

            // S4: the entry into the session for this user (SharedSessionAuth.Request.Ticket), opaque to the client.
            public string Ticket { get; set; }
        }
    }

    [MemoryPackUnion(150, typeof(SharedMatchmaking.SearchLobby))]
    [MemoryPackUnion(151, typeof(SharedMatchmaking.SearchMatch))]
    [MemoryPackUnion(152, typeof(SharedMatchmaking.Create))]
    [MemoryPackUnion(153, typeof(SharedMatchmaking.CreateWithBot))]
    [MemoryPackUnion(154, typeof(SharedMatchmaking.CancelSearch))]
    [MemoryPackUnion(155, typeof(SharedMatchmaking.MatchResult))]
    [MemoryPackUnion(156, typeof(SharedMatchmaking.LobbyResult))]
    public partial class NetworkContextUnion
    {
    }
}
