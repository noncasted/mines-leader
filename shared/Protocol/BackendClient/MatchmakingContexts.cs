using System;
using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    public static partial class MatchmakingContexts
    {
        public const string SearchEndpoint = "/matchmaking/search";
        public const string CancelEndpoint = "/matchmaking/cancel";
        public const string CreateEndpoint = "/matchmaking/create";
        
        public class Search
        {
            public string Type { get; set; }
            public Guid UserId { get; set; }
        }
    
        public class Create
        {
            public Guid UserId { get; set; }
        }
        
        public class CancelSearch
        {
            public Guid UserId { get; set; }
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
    }
}