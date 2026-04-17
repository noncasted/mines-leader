using Game.Global;
using Microsoft.AspNetCore.Mvc;

namespace GameGateway;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder AddSessionEndpoints(this IEndpointRouteBuilder builder)
    {
        var sessions = builder.MapGroup("/api/sessions");
        sessions.MapGet("/", ListActive);
        sessions.MapGet("/{sessionId}/players", ListPlayers);

        return builder;
    }

    private static IReadOnlyList<SessionOverviewDto> ListActive([FromServices] ISessionsCollection sessions)
    {
        return sessions.Entries.Values.Select(s => new SessionOverviewDto
        {
            Id = s.Id,
            Type = s.Type.ToString(),
            PlayerCount = s.Users.ToList().Count,
            CreatedAt = DateTime.UtcNow
        }).ToList();
    }

    private static IResult ListPlayers(
        Guid sessionId,
        [FromServices] ISessionsCollection sessions)
    {
        if (!sessions.Entries.TryGetValue(sessionId, out var session))
            return Results.NotFound($"Session {sessionId} not found");

        var snapshot = session.Users.ToList();

        IReadOnlyList<SessionPlayerDto> players = snapshot.Select(u => new SessionPlayerDto
        {
            Id = u.Id,
            Index = u.Index,
            IsBot = u.IsBot
        }).ToList();

        return Results.Ok(players);
    }
}

public record SessionOverviewDto
{
    public required Guid Id { get; init; }
    public required string Type { get; init; }
    public required int PlayerCount { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public record SessionPlayerDto
{
    public required Guid Id { get; init; }
    public required int Index { get; init; }
    public required bool IsBot { get; init; }
}