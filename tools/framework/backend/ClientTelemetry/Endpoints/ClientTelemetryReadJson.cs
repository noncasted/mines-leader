using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.ClientTelemetry;

// The answers of the reading are for an agent: statuses and levels as words, whatever the game sets for its own JSON.
internal static class ClientTelemetryReadJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
