using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

// One page of the list. Platforms and tags: of all sessions, so a chosen filter does not take the other choices away.
public sealed record TelemetrySessionListPage(
    IReadOnlyList<TelemetrySessionInfo> Rows,
    int Page,
    int TotalPages,
    IReadOnlyList<string> Platforms,
    IReadOnlyList<string> Tags,
    TelemetrySessionTotals Totals)
{
    // The list changes under an open page: a page past the end becomes the last one.
    public static TelemetrySessionListPage Of(IReadOnlyList<TelemetrySessionInfo> all, TelemetrySessionQuery query, int pageSize)
    {
        var filter = new TelemetrySessionFilter { Status = query.Status, Platform = query.Platform, Tags = query.Tag == null ? [] : [query.Tag] };
        var matching = all.Where(filter.Matches).ToArray();

        var totalPages = Math.Max(1, (matching.Length + pageSize - 1) / pageSize);
        var page = Math.Min(query.Page, totalPages - 1);

        return new TelemetrySessionListPage(
            matching.Skip(page * pageSize).Take(pageSize).ToArray(),
            page,
            totalPages,
            all.Select(session => session.Device.Platform).OfType<string>().Distinct().Order(StringComparer.Ordinal).ToArray(),
            all.SelectMany(session => session.Tags).Distinct().Order(StringComparer.Ordinal).ToArray(),
            TelemetrySessionTotals.Of(matching));
    }
}
