using Common.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orleans.GrainReferences;

namespace Infrastructure.State;

public class GrainIdConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(GrainId);

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var id = (GrainId)value!;
        writer.WriteStartObject();
        writer.WritePropertyName("key");
        var key = $"{id.Type}:{id.Key}";
        writer.WriteValue(key);
        writer.WriteEndObject();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var json = JObject.Load(reader);
        var raw = json["key"]!.ToObject<string>()!;
        var split = raw.Split(':', count: 2);

        if (split.Length != 2)
            throw new JsonSerializationException($"[GrainIdConverter] Invalid GrainId format: '{raw}'");

        var grainId = GrainId.Create(split[0], split[1]);
        return grainId;
    }
}
