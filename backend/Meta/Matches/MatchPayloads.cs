using Shared;

namespace Meta.Matches;

public class MatchPayloads
{
    public class Match
    {
        [GenerateSerializer]
        public class Request
        {
            [Id(0)] public required GameMatchType Type { get; init; }
        }

        [GenerateSerializer]
        public class Response
        {
            [Id(0)] public required Guid SessionId { get; init; }
        }
    }

    public class Lobby
    {
        [GenerateSerializer]
        public class Request
        {
            [Id(0)] public required SessionType Type { get; init; }
            [Id(1)] public required Guid UserId { get; init; }
        }

        [GenerateSerializer]
        public class Response
        {
            [Id(0)] public required Guid SessionId { get; init; }
        }
    }
}