using Game.MatchMetrics;
using Meta.Matches;

namespace MetaGateway.Matches;

// The players of a match from the collector of the game metrics (CONTRACT.md): the totals and the events of every
// participant, the series of one of them by steps and as csv.
public static class MatchHistoryPlayerEndpoints
{
    public static void MapMatchHistoryPlayers(this RouteGroupBuilder group)
    {
        group.MapGet("/matches/{id:guid}/players", GetPlayers);
        group.MapGet("/matches/{id:guid}/players/{userId:guid}/series", GetSeries);
        group.MapGet("/matches/{id:guid}/players/{userId:guid}/series.csv", GetSeriesCsv);
    }

    private static async Task<IResult> GetPlayers(IMatchSummaryCollection summaries, IMatchMetricsReader reader, Guid id)
    {
        if (summaries.TryGetValue(id, out var match) == false || match.IsSetUp == false)
            return Results.NotFound();

        var cards = await new MatchPlayerCards(reader).Get(id, match);
        return MatchHistoryEndpoints.Json(cards);
    }

    private static async Task<IResult> GetSeries(
        IMatchSummaryCollection summaries,
        IMatchMetricsReader reader,
        Guid id,
        Guid userId,
        int? step,
        string? axis)
    {
        var request = await ReadSeries(summaries, reader, id, userId, step, axis);
        return request.Error ?? MatchHistoryEndpoints.Json(request.Series);
    }

    private static async Task<IResult> GetSeriesCsv(
        IMatchSummaryCollection summaries,
        IMatchMetricsReader reader,
        Guid id,
        Guid userId,
        int? step,
        string? axis)
    {
        var request = await ReadSeries(summaries, reader, id, userId, step, axis);
        return request.Error ?? Results.Text(MatchPlayerSeriesCsv.Write(request.Series!), "text/csv");
    }

    private static async Task<(IResult? Error, MatchPlayerSeries? Series)> ReadSeries(
        IMatchSummaryCollection summaries,
        IMatchMetricsReader reader,
        Guid id,
        Guid userId,
        int? step,
        string? axis)
    {
        if (summaries.TryGetValue(id, out var match) == false || match.IsPlayer(userId) == false)
            return (Results.NotFound(), null);

        var parsedStep = (MatchMetricStep)(step ?? (int)MatchMetricStep.TenSeconds);

        if (Enum.IsDefined(parsedStep) == false)
            return (Results.BadRequest("step 1, 10 or 60"), null);

        if (TryParseAxis(axis, out var parsedAxis) == false)
            return (Results.BadRequest("axis run or server"), null);

        var series = await new MatchPlayerSeriesReader(reader).Get(id, match, userId, parsedStep, parsedAxis);
        return (null, series);
    }

    private static bool TryParseAxis(string? axis, out MatchMetricAxis parsed)
    {
        parsed = MatchMetricAxis.Run;

        if (axis == null || axis == "run")
            return true;

        if (axis != "server")
            return false;

        parsed = MatchMetricAxis.Server;
        return true;
    }
}
