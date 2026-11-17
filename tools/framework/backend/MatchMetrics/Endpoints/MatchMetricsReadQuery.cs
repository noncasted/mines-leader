using Microsoft.AspNetCore.Http;

namespace Game.MatchMetrics;

// The filter of a request: name, user, tag.<key>=<value>, groupBy=<key>,<key>. A value out of the rules throws
// MatchMetricsQueryException (400).
internal static class MatchMetricsReadQuery
{
    private const string _tagPrefix = "tag.";

    public static MatchMetricFilter Filter(HttpRequest request)
    {
        var query = request.Query;
        var tags = new Dictionary<string, string>();

        foreach (var (key, values) in query)
        {
            if (key.StartsWith(_tagPrefix, StringComparison.Ordinal) == false)
                continue;

            if (values.Count != 1)
                throw new MatchMetricsQueryException($"The tag {key} once");

            tags[key[_tagPrefix.Length..]] = values[0]!;
        }

        var groupBy = query["groupBy"].ToString();

        return new MatchMetricFilter
        {
            Name = One(request, "name"),
            UserId = User(request),
            Tags = tags,
            GroupBy = groupBy.Length == 0 ? Array.Empty<string>() : groupBy.Split(',', StringSplitOptions.TrimEntries)
        };
    }

    public static Guid? User(HttpRequest request)
    {
        return One(request, "user") is { } user ? Guid.TryParse(user, out var id) ? id : throw new MatchMetricsQueryException("user is a guid") : null;
    }

    public static string? One(HttpRequest request, string key)
    {
        var values = request.Query[key];
        return values.Count == 0 ? null : values[0];
    }

    public static MatchMetricStep Step(int? step)
    {
        return step switch
        {
            null or 1 => MatchMetricStep.Second,
            10 => MatchMetricStep.TenSeconds,
            60 => MatchMetricStep.Minute,
            _ => throw new MatchMetricsQueryException("step is 1, 10 or 60")
        };
    }

    public static MatchMetricAxis Axis(string? axis)
    {
        return axis switch
        {
            null or "run" => MatchMetricAxis.Run,
            "server" => MatchMetricAxis.Server,
            _ => throw new MatchMetricsQueryException("axis is run or server")
        };
    }
}
