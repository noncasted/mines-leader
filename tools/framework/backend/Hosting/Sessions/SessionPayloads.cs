using Game.Lobbies;

namespace Game.Hosting;

// The requests of the meta to a game server over the message pipes (MessagePipeServiceRequestId of the server and
// the request type), answered by SessionEndpoints.
public static class SessionPayloads
{
    public class Create
    {
        // A match for exactly these users (a solo run): their places are reserved at the creation, the session
        // starts once all of them have joined.
        [GenerateSerializer]
        public class Request
        {
            [Id(0)] public required IReadOnlyList<Guid> UserIds { get; init; }
        }

        [GenerateSerializer]
        public class Response
        {
            [Id(0)] public required Guid SessionId { get; init; }
        }
    }

    public class Search
    {
        [GenerateSerializer]
        public class Request
        {
            [Id(0)] public required Guid UserId { get; init; }
        }

        [GenerateSerializer]
        public class Response
        {
            [Id(0)] public required Guid SessionId { get; init; }

            // The session is the lobby of the late players of a running match: the user enters the match from it.
            [Id(1)] public bool IsLobby { get; init; }
        }
    }

    public class LobbyCreate
    {
        [GenerateSerializer]
        public class Request
        {
            [Id(0)] public required Guid UserId { get; init; }
        }

        [GenerateSerializer]
        public class Response
        {
            [Id(0)] public required Guid SessionId { get; init; }
            [Id(1)] public required string Code { get; init; }
        }
    }

    public class LobbyJoin
    {
        [GenerateSerializer]
        public class Request
        {
            [Id(0)] public required Guid UserId { get; init; }

            // The lobby session that holds the code.
            [Id(1)] public required Guid SessionId { get; init; }

            // Not empty: the lobby has started this match - the user gets to the lobby of its late players.
            [Id(2)] public Guid MatchSessionId { get; init; }
            [Id(3)] public string Code { get; init; } = string.Empty;
        }

        [GenerateSerializer]
        public class Response
        {
            [Id(0)] public required LobbyJoinStatus Status { get; init; }

            // Ok only: the session to enter.
            [Id(1)] public Guid SessionId { get; init; }
        }
    }

    // A place in the running match of a lobby, for a late player.
    public class MatchJoin
    {
        [GenerateSerializer]
        public class Request
        {
            [Id(0)] public required Guid UserId { get; init; }
            [Id(1)] public required Guid LobbySessionId { get; init; }
            [Id(2)] public required Guid MatchSessionId { get; init; }
        }

        [GenerateSerializer]
        public class Response
        {
            [Id(0)] public required LobbyJoinStatus Status { get; init; }
        }
    }
}
