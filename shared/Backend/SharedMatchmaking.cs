using System;
using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    public static partial class SharedMatchmaking
    {
        public const string GameType = "Game";
        public const string LobbyType = "Lobby";
        
        [MemoryPackable]
        public partial class Search : INetworkContext
        {
            public string Type { get; set; }
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