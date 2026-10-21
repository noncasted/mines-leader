using Meta.Users;
using Shared;

namespace Meta.Matches;

[GenerateSerializer]
public class LobbySearchResultUpdate : IProjectionPayload
{
    [Id(0)] public required Guid SessionId { get; init; }

    [Id(1)] public required string ServerUrl { get; init; }

    // S4: the entry of this user into the session (SessionTickets), one per participant.
    [Id(2)] public required string Ticket { get; init; }

    public INetworkContext ToContext()
    {
        return new SharedMatchmaking.LobbyResult()
        {
            SessionId = SessionId,
            ServerUrl = ServerUrl,
            Ticket = Ticket
        };
    }
}