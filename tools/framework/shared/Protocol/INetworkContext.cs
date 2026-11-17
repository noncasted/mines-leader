namespace Shared
{
    // Every message of the protocol (a request, a response, a push). The union over INetworkContext is declared by
    // the project, not here: its types live in two assemblies (the framework ones here, the game ones in the project
    // shared) and a [MemoryPackable] partial interface cannot span assemblies. The project declares
    //
    //     [MemoryPackUnionFormatter(typeof(INetworkContext))]
    //     [MemoryPackUnion(NetworkContextTags.EmptyResponse, typeof(EmptyResponse))]
    //     [MemoryPackUnion(NetworkContextTags.ErrorResponse, typeof(ErrorResponse))]
    //     public partial class NetworkContextUnion { }
    //
    // and adds the tags of its types next to them, in partial declarations of the same class. The generated
    // NetworkContextUnionInitializer.RegisterFormatter() must run before the first message: on .NET it is a module
    // initializer, in Unity the project calls it on load.
    //
    // A member of type INetworkContext needs [MemoryPackAllowSerialize]: the formatter is registered at runtime.
    public interface INetworkContext
    {
    }

    // Tag ranges of the INetworkContext union, shared by the framework and every project:
    // - 0-249 framework (one byte on the wire): 0 EmptyResponse, 1 ErrorResponse, 10-19 session auth, 20-29 entity,
    //   30-39 object, 40-49 player, 50-59 service, 60-69 latency, 70-79 peers, 100-149 backend meta, 150-179 session
    //   requests, 180-199 lobbies, 200-219 client telemetry, 220-229 match metrics; one exception: SessionCreateSolo (a session request) took 184 in the
    //   lobbies range;
    // - 1000+ game.
    // A tag is never moved: shipped clients have it on the wire. A removed type leaves a hole: tags are never reused.
    // A duplicated tag fails the MemoryPack generator.
    public static class NetworkContextTags
    {
        public const ushort EmptyResponse = 0;
        public const ushort ErrorResponse = 1;

        // Session (Session/*): a project with real-time sessions lists all of them in its union.
        public const ushort SessionAuthRequest = 10;
        public const ushort SessionAuthResponse = 11;

        public const ushort EntityCreateRequest = 20;
        public const ushort EntityCreateResponse = 21;
        public const ushort EntityCreatedOverview = 22;
        public const ushort EntityDestroy = 23;
        public const ushort EntityDestroyUpdate = 24;

        public const ushort ObjectSetProperty = 30;
        public const ushort ObjectPropertyUpdate = 31;
        public const ushort ObjectEvent = 32;
        public const ushort ObjectSetProperties = 33;

        public const ushort PlayerLocalUpdate = 40;
        public const ushort PlayerRemoteUpdate = 41;
        public const ushort PlayerRemoteDisconnect = 42;
        public const ushort PlayerRemoteAway = 43;
        public const ushort PlayerLeave = 44;

        public const ushort ServiceGetRequest = 50;
        public const ushort ServiceGetResponse = 51;
        // Unused: SharedSessionService.Overview is never sent on its own (GetResponse carries the properties). The
        // tag stays reserved for the type in the unions of the projects.
        public const ushort ServiceOverview = 52;

        public const ushort LatencyPing = 60;
        public const ushort LatencyPong = 61;
        public const ushort LatencyPeerProbe = 62;
        public const ushort LatencyPeerEcho = 63;

        public const ushort PeerSignal = 70;
        public const ushort PeerTunnel = 71;

        // Session requests (Matchmaking/SharedSessionRequests) and lobbies (Lobbies/SharedLobbies): a project with the
        // lobbies module lists all of them.
        public const ushort SessionSearch = 150;
        public const ushort SessionResult = 151;
        public const ushort LobbyCreate = 152;
        public const ushort LobbyJoin = 153;
        public const ushort SessionTicketRequest = 154;
        public const ushort SessionTicketResponse = 155;

        public const ushort LobbiesSubscribe = 180;
        public const ushort LobbiesUnsubscribe = 181;
        public const ushort LobbiesList = 182;
        public const ushort LobbyJoinMatch = 183;

        // A session request (SharedSessionRequests.CreateSolo) in the lobbies range: added after it, kept for the wire.
        public const ushort SessionCreateSolo = 184;

        // The client telemetry (ClientTelemetry/SharedClientTelemetry): a project with the telemetry module lists all of them.
        public const ushort TelemetrySessionStart = 200;
        public const ushort TelemetrySessionStarted = 201;
        public const ushort TelemetryBatch = 202;
        public const ushort TelemetryBatchResult = 203;
        public const ushort TelemetrySessionEnd = 204;
        public const ushort TelemetryStartupTrace = 205;
        public const ushort TelemetryStartupTraceResult = 206;

        // The game data of a match (MatchMetrics/SharedMatchMetrics): a project with the match-metrics module lists all of them.
        public const ushort MatchMetricsOpen = 220;
        public const ushort MatchMetricsOpened = 221;
        public const ushort MatchMetricsBatch = 222;
        public const ushort MatchMetricsBatchResult = 223;

        public const ushort GameFirst = 1000;
    }
}
