using Game.MatchMetrics;
using Meta.Matches;
using Meta.Users;

namespace MetaGateway.Matches;

// The reading of the records of matches for an agent: CONTRACT.md next to this file. The token stays in Program.cs:
// MapMatchHistoryRead().RequireConsoleToken(). The lists come from the records in memory of the host
// (IMatchSummaryCollection), the players of a match from the collector of the game metrics (MatchHistoryPlayerEndpoints).
public static class MatchHistoryEndpoints
{
    public const string Prefix = "/api/match-history";

    private const int _maxMatches = 1000;

    public static RouteGroupBuilder MapMatchHistoryRead(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(Prefix);

        group.AddEndpointFilter(AnswerUnavailable);

        group.MapGet("/matches", GetMatches);
        group.MapGet("/matches/{id:guid}", GetMatch);
        group.MapGet("/players/{player}", GetPlayer);
        group.MapMatchHistoryPlayers();

        return group;
    }

    // The boundary of the collector of the game metrics: a query it refuses is the reader's fault (400), a failure of its
    // database is 503: the reader may repeat.
    private static async ValueTask<object?> AnswerUnavailable(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (MatchMetricsQueryException exception)
        {
            return Results.BadRequest(exception.Message);
        }
        catch (System.Data.Common.DbException exception)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                                .CreateLogger(typeof(MatchHistoryEndpoints).FullName!);
            logger.LogWarning(exception, "[Match] [History] A reading of the game metrics failed");

            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static IResult GetMatches(IMatchSummaryCollection summaries, [AsParameters] MatchHistoryListQuery query)
    {
        var error = query.FindError(_maxMatches, out var filter);

        if (error != null)
            return Results.BadRequest(error);

        return Json(filter.Apply(summaries).Select(entry => MatchHistoryRow.From(entry.Key, entry.Value)).ToArray());
    }

    private static IResult GetMatch(IMatchSummaryCollection summaries, Guid id)
    {
        if (summaries.TryGetValue(id, out var match) == false || match.IsSetUp == false)
            return Results.NotFound();

        return Json(MatchHistoryRow.From(id, match));
    }

    // The player is a meta id or the name of a user now (case ignored); a name of several users answers 409 with their ids.
    private static IResult GetPlayer(IMatchSummaryCollection summaries, IUserCollection users, string player, int? limit)
    {
        if (limit < 1 || limit > _maxMatches)
            return Results.BadRequest($"limit from 1 to {_maxMatches}");

        var found = FindUsers(users, player);

        if (found.Count == 0)
            return Results.NotFound();

        if (found.Count > 1)
            return Results.Conflict($"nickname '{player}' belongs to users {string.Join(", ", found.Select(user => user.UserId))}");

        var user = found[0];
        var filter = new MatchListFilter { PlayerId = user.UserId, Limit = limit ?? MatchListFilter.DefaultLimit };

        return Json(new MatchHistoryPlayer
        {
            UserId = user.UserId,
            Nickname = user.Name,
            Total = filter.Count(summaries.Values),
            Matches = filter.Apply(summaries).Select(entry => MatchHistoryRow.From(entry.Key, entry.Value)).ToArray()
        });
    }

    private static IReadOnlyList<UserState> FindUsers(IUserCollection users, string player)
    {
        if (Guid.TryParse(player, out var id) == true)
            return users.TryGetValue(id, out var user) == true ? [user] : [];

        return users.Values.Where(user => string.Equals(user.Name, player, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    internal static IResult Json<T>(T value)
    {
        return Results.Json(value, MatchHistoryJson.Options);
    }
}
