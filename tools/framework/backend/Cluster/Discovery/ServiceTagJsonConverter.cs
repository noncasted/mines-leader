using Newtonsoft.Json;

namespace Cluster.Discovery;

// Persisted state (service_discovery jsonb) holds the tag as a string. Rows written before the tags became
// strings hold the numeric value of the old enum; they are read into the names the enum had.
public class ServiceTagJsonConverter : JsonConverter<ServiceTag>
{
    private static readonly IReadOnlyDictionary<long, ServiceTag> _legacyEnumValues = new Dictionary<long, ServiceTag>
    {
        [100] = ServiceTag.Coordinator,
        [200] = new("Meta"),
        [300] = new("Game"),
        [400] = new("Silo"),
        [500] = new("Console"),
    };

    public override void WriteJson(JsonWriter writer, ServiceTag value, JsonSerializer serializer)
    {
        writer.WriteValue(value.Value);
    }

    public override ServiceTag ReadJson(
        JsonReader reader,
        Type objectType,
        ServiceTag existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case JsonToken.String:
                return new ServiceTag((string)reader.Value!);
            case JsonToken.Integer:
            {
                var number = Convert.ToInt64(reader.Value);

                if (_legacyEnumValues.TryGetValue(number, out var tag) == false)
                    throw new JsonSerializationException($"Unknown legacy service tag value {number}");

                return tag;
            }
            default:
                throw new JsonSerializationException($"Unexpected token {reader.TokenType} for a service tag");
        }
    }
}
