using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.MatchMetrics;

// The answers of the reading are for an agent: kinds as words, whatever the game sets for its own JSON.
internal static class MatchMetricsReadJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
