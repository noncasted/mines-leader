using System.Text.Json;
using System.Text.Json.Serialization;

namespace MetaGateway.Matches;

// camelCase names, enums as their C# names (LastManStandingTurnBased, Run) - the same text the query takes, an absent
// value as an explicit null.
public static class MatchHistoryJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
