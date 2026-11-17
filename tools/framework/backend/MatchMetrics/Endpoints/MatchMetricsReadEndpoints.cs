using System.Data.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Game.MatchMetrics;

// GET only. The token stays in the game: MapMatchMetricsRead().RequireConsoleToken(). The reader is AddMatchMetricsReader.
public static class MatchMetricsReadEndpoints
{
    public static RouteGroupBuilder MapMatchMetricsRead(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(MatchMetricsRoutes.Prefix);

        group.AddEndpointFilter(AnswerErrors);

        group.MapGet("/series", (IMatchMetricsReader reader, string? name) => Json(reader.GetSeries(name)));
        group.MapGet("/totals", GetTotals);
        group.MapGet("/matches/{matchId:guid}/streams", (IMatchMetricsReader reader, Guid matchId) => Json(reader.GetStreams(matchId)));
        group.MapGet("/matches/{matchId:guid}/series", GetPoints);
        group.MapGet("/matches/{matchId:guid}/totals", GetMatchTotals);
        group.MapGet("/matches/{matchId:guid}/events", GetEvents);

        return group;
    }

    // A bad query - 400 with its message; the database could not answer (Npgsql throws DbException) or timed out - 503, the
    // reader may repeat. Anything else is a bug of the server: it goes on to the host, 500.
    private static async ValueTask<object?> AnswerErrors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (MatchMetricsQueryException exception)
        {
            return Results.BadRequest(exception.Message);
        }
        catch (Exception exception) when (exception is DbException or TimeoutException)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(MatchMetricsReadEndpoints));
            logger.LogWarning(exception, "[MatchMetrics] A reading failed");

            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static Task<IResult> GetPoints(IMatchMetricsReader reader, HttpRequest request, Guid matchId, int? step, string? axis, long? from, long? to)
    {
        var query = new MatchMetricPointsQuery
        {
            Filter = MatchMetricsReadQuery.Filter(request),
            Step = MatchMetricsReadQuery.Step(step),
            Axis = MatchMetricsReadQuery.Axis(axis),
            From = from,
            To = to
        };

        return Json(reader.GetPoints(matchId, query));
    }

    private static Task<IResult> GetMatchTotals(IMatchMetricsReader reader, HttpRequest request, Guid matchId)
    {
        return Json(reader.GetTotals(new MatchMetricTotalsQuery { MatchId = matchId, Filter = MatchMetricsReadQuery.Filter(request) }));
    }

    private static Task<IResult> GetTotals(IMatchMetricsReader reader, HttpRequest request, DateTime? since, DateTime? until)
    {
        var query = new MatchMetricTotalsQuery
        {
            Filter = MatchMetricsReadQuery.Filter(request),
            Since = since?.ToUniversalTime(),
            Until = until?.ToUniversalTime()
        };

        return Json(reader.GetTotals(query));
    }

    private static Task<IResult> GetEvents(IMatchMetricsReader reader, HttpRequest request, Guid matchId, string? name, float? from, float? to)
    {
        var query = new MatchMetricEventsQuery { Name = name, UserId = MatchMetricsReadQuery.User(request), From = from, To = to };
        return Json(reader.GetEvents(matchId, query));
    }

    private static async Task<IResult> Json<T>(Task<T> value)
    {
        return Results.Json(await value, MatchMetricsReadJson.Options);
    }
}
