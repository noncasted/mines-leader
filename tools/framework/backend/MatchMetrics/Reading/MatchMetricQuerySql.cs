using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Shared;

namespace Game.MatchMetrics;

// The part of a query a filter adds over "series s JOIN streams t": the text depends only on the count of the keys, every
// name, tag and key is a parameter. Names and keys are checked by the rule of names before.
internal static class MatchMetricQuerySql
{
    public static string Where(NpgsqlCommand command, MatchMetricFilter filter)
    {
        Check(filter);

        var where = new List<string>();

        if (filter.Name != null)
        {
            where.Add("s.name = @name");
            command.Parameters.AddWithValue("name", filter.Name);
        }

        if (filter.UserId != null)
        {
            where.Add("t.user_id = @user");
            command.Parameters.AddWithValue("user", filter.UserId.Value);
        }

        if (filter.Tags.Count > 0)
        {
            where.Add("s.tags @> @tags");
            command.Parameters.Add(new NpgsqlParameter("tags", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(filter.Tags) });
        }

        return where.Count == 0 ? "" : " AND " + string.Join(" AND ", where);
    }

    // The columns g0 .. gN of the group: a tag of the series or the user of the stream.
    public static string GroupColumns(NpgsqlCommand command, MatchMetricFilter filter)
    {
        var columns = new List<string>();

        for (var i = 0; i < filter.GroupBy.Count; i++)
        {
            if (filter.GroupBy[i] == MatchMetricFilter.UserKey)
            {
                columns.Add($", t.user_id::text AS g{i}");
                continue;
            }

            columns.Add($", coalesce(s.tags ->> @g{i}, '') AS g{i}");
            command.Parameters.AddWithValue($"g{i}", filter.GroupBy[i]);
        }

        return string.Concat(columns);
    }

    public static IReadOnlyDictionary<string, string> ReadGroup(NpgsqlDataReader reader, MatchMetricFilter filter, int first)
    {
        var group = new Dictionary<string, string>();

        for (var i = 0; i < filter.GroupBy.Count; i++)
            group[filter.GroupBy[i]] = reader.GetString(first + i);

        return group;
    }

    public static void CheckName(string? name)
    {
        if (name != null && SharedMatchMetricNames.IsName(name) == false)
            throw new MatchMetricsQueryException($"The name '{name}' breaks the rule of names [a-z0-9._]");
    }

    private static void Check(MatchMetricFilter filter)
    {
        CheckName(filter.Name);

        foreach (var (key, value) in filter.Tags)
        {
            if (SharedMatchMetricNames.IsKey(key) == false || SharedMatchMetricNames.IsValue(value) == false)
                throw new MatchMetricsQueryException($"The tag '{key}={value}' breaks the rule of names");
        }

        if (filter.GroupBy.Count > SharedMatchMetricsLimits.MaxTags + 1 || filter.GroupBy.Distinct().Count() != filter.GroupBy.Count)
            throw new MatchMetricsQueryException($"groupBy: distinct keys, at most {SharedMatchMetricsLimits.MaxTags + 1}");

        foreach (var key in filter.GroupBy)
        {
            if (SharedMatchMetricNames.IsKey(key) == false)
                throw new MatchMetricsQueryException($"The group key '{key}' breaks the rule of names");
        }
    }
}
