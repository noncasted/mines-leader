using System.Data.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;

namespace Game.ClientTelemetry;

public static class ClientTelemetryReadEndpoints
{
    private const int _defaultSessions = 100;
    private const int _defaultRows = 1000;
    private const int _maxLimit = 5000;
    private const int _maxWaitSeconds = 30;

    private static readonly IReadOnlyDictionary<string, TelemetrySessionStatus> _statuses = new Dictionary<string, TelemetrySessionStatus>
    {
        ["live"] = TelemetrySessionStatus.Live,
        ["ended"] = TelemetrySessionStatus.Ended,
        ["lost"] = TelemetrySessionStatus.Lost
    };

    // The token stays in the game: MapClientTelemetryRead().RequireConsoleToken(). The ids of sessions, the devices and the texts of
    // errors are not public. Times of a window are in the milliseconds of the session, the "t" of the contract.
    public static RouteGroupBuilder MapClientTelemetryRead(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ClientTelemetryRoutes.Prefix);

        group.AddEndpointFilter(AnswerErrors);

        group.MapGet("/sessions", GetSessions);
        group.MapGet("/sessions/{id:guid}", GetSession);
        group.MapGet("/sessions/{id:guid}/summary", GetSummary);
        group.MapGet("/sessions/{id:guid}/samples", GetSamples);
        group.MapGet("/sessions/{id:guid}/marks", GetMarks);
        group.MapGet("/sessions/{id:guid}/errors", GetErrors);
        group.MapGet("/errors", GetErrorGroups);
        group.MapGet("/startups", GetStartups);
        group.MapGet("/startups/{id:guid}", GetStartup);

        return group;
    }

    // The database could not answer (Npgsql throws DbException), a call timed out or the silo of a grain is gone - 503, the reader
    // may repeat. Anything else is a bug of the server: it goes on to the host, 500.
    private static async ValueTask<object?> AnswerErrors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception exception) when (exception is DbException or TimeoutException or OrleansMessageRejectionException)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ClientTelemetryReadEndpoints));
            logger.LogWarning(exception, "[ClientTelemetry] A reading failed");

            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> GetSessions(
        IClientTelemetryReader reader,
        string? status,
        string? platform,
        string? build,
        string[]? tag,
        string? metric,
        double? averageBelow,
        double? averageAbove,
        int? minErrors,
        Guid? startupId,
        Guid? previousId,
        int? offset,
        int? limit)
    {
        if (offset < 0 || !IsLimit(limit))
            return Results.BadRequest($"offset from 0, limit from 1 to {_maxLimit}");

        if (metric == null && (averageBelow != null || averageAbove != null))
            return Results.BadRequest("averageBelow and averageAbove go with metric");

        TelemetrySessionStatus? statusFilter = null;

        if (status != null)
        {
            if (!_statuses.TryGetValue(status, out var known))
                return Results.BadRequest("status is live, ended or lost");

            statusFilter = known;
        }

        var filter = new TelemetrySessionFilter
        {
            Status = statusFilter,
            Platform = platform,
            BuildVersion = build,
            Tags = tag ?? [],
            Metric = metric,
            AverageBelow = averageBelow,
            AverageAbove = averageAbove,
            MinErrors = minErrors,
            StartupId = startupId,
            PreviousId = previousId,
            Offset = offset ?? 0,
            Limit = limit ?? _defaultSessions
        };

        return Json(await reader.GetSessions(filter));
    }

    private static async Task<IResult> GetSession(IClientTelemetryReader reader, Guid id)
    {
        return await reader.GetSession(id) is { } session ? Json(session) : Results.NotFound();
    }

    private static async Task<IResult> GetSummary(IClientTelemetryReader reader, Guid id, long? from, long? to)
    {
        if (await reader.GetSession(id) is not { } started)
            return Results.NotFound();

        // Counted once at the close, the whole session needs no rows.
        if (from == null && to == null && started.Digest != null)
            return Json(started.Digest.Summary);

        var samples = await reader.GetSampleWindow(id, from ?? 0, to ?? long.MaxValue);

        // After the rows: the names of the rows are written with them, this list holds them all.
        var session = await reader.GetSession(id) ?? throw new InvalidOperationException($"The session {id} is gone");

        return Json(TelemetrySummaryCalculator.Calculate(session.Names, samples));
    }

    private static async Task<IResult> GetSamples(
        IClientTelemetryReader reader,
        Guid id,
        long? after,
        int? wait,
        int? limit,
        CancellationToken cancellation)
    {
        if (after < 0 || wait is < 0 or > _maxWaitSeconds || !IsLimit(limit))
            return Results.BadRequest($"after from 0, wait from 0 to {_maxWaitSeconds} seconds, limit from 1 to {_maxLimit}");

        if (await reader.GetSession(id) == null)
            return Results.NotFound();

        var afterSeq = after ?? 0;
        var samples = await TelemetrySamplesWait.Next(reader, id, afterSeq, limit ?? _defaultRows, TimeSpan.FromSeconds(wait ?? 0), cancellation);
        var session = await reader.GetSession(id) ?? throw new InvalidOperationException($"The session {id} is gone");
        var named = samples.Select(sample => TelemetryNamedSample.From(sample, session.Names)).ToArray();

        return Json(new TelemetryReadPage<TelemetryNamedSample>(session.Status, samples.Count > 0 ? samples[^1].Seq : afterSeq, named));
    }

    private static Task<IResult> GetMarks(IClientTelemetryReader reader, Guid id, long? after, int? limit)
    {
        return GetRows(reader, id, after, limit, reader.GetMarks, mark => mark.Seq);
    }

    private static Task<IResult> GetErrors(IClientTelemetryReader reader, Guid id, long? after, int? limit)
    {
        return GetRows(reader, id, after, limit, reader.GetErrors, error => error.Seq);
    }

    private static async Task<IResult> GetErrorGroups(IClientTelemetryReader reader, int? limit)
    {
        if (!IsLimit(limit))
            return Results.BadRequest($"limit from 1 to {_maxLimit}");

        return Json(await reader.GetErrorGroups(limit ?? _defaultSessions));
    }

    private static async Task<IResult> GetStartups(IClientTelemetryReader reader, int? limit)
    {
        if (!IsLimit(limit))
            return Results.BadRequest($"limit from 1 to {_maxLimit}");

        return Json(await reader.GetStartups(limit ?? _defaultSessions));
    }

    private static async Task<IResult> GetStartup(IClientTelemetryReader reader, Guid id)
    {
        if (await reader.GetStartup(id) is not { } startup)
            return Results.NotFound();

        var spans = await reader.GetStartupSpans(id);
        var sessions = await reader.GetSessions(new TelemetrySessionFilter { StartupId = id, Limit = _maxLimit });

        return Json(new TelemetryStartupPage(startup, TelemetryStartupAnalysis.Of(spans), sessions));
    }

    private static async Task<IResult> GetRows<T>(
        IClientTelemetryReader reader,
        Guid id,
        long? after,
        int? limit,
        Func<Guid, long, int, Task<IReadOnlyList<T>>> read,
        Func<T, long> seq)
    {
        if (after < 0 || !IsLimit(limit))
            return Results.BadRequest($"after from 0, limit from 1 to {_maxLimit}");

        if (await reader.GetSession(id) is not { } session)
            return Results.NotFound();

        var afterSeq = after ?? 0;
        var rows = await read(id, afterSeq, limit ?? _defaultRows);

        return Json(new TelemetryReadPage<T>(session.Status, rows.Count > 0 ? seq(rows[^1]) : afterSeq, rows));
    }

    private static bool IsLimit(int? limit)
    {
        return limit is null or >= 1 and <= _maxLimit;
    }

    private static IResult Json<T>(T value)
    {
        return Results.Json(value, ClientTelemetryReadJson.Options);
    }
}
