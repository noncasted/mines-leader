using MetaGateway.Matchmaking;
using MetaGateway.UserFlow;
using Microsoft.AspNetCore.Mvc;

namespace MetaGateway;

public static class MonitorEndpoints
{
    public static IEndpointRouteBuilder AddMonitorEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/monitor");

        group.MapGet("/connected-users", ListConnectedUsers);
        group.MapGet("/matchmaking", GetMatchmakingStats);

        return builder;
    }

    private static IReadOnlyList<ConnectedUserDto> ListConnectedUsers([FromServices] IConnectedUsers users)
    {
        return users.Entries.Values.Select(s => new ConnectedUserDto
        {
            UserId = s.UserId
        }).ToList();
    }

    private static MatchmakingStatsDto GetMatchmakingStats([FromServices] IMatchmaking matchmaking)
    {
        return matchmaking.GetStats();
    }
}

public record ConnectedUserDto
{
    public required Guid UserId { get; init; }
}
